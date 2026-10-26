using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Observability;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests.IntelligenceLab;

/// <summary>
/// Intelligence Lab - fase 2B, primeiro recorte.
/// Exercita o Isolation Forest real no Ensemble e verifica a captura passiva.
/// Nao modifica producao, nao toca nos modelos reais e nao depende do Bridge.
/// Estes testes NAO representam ainda um E2E completo do PredictionService.
/// </summary>
public sealed class IntelligenceLabIsolationForestObservabilityTests
{
    private static readonly DateTime ReadingTime =
        new(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SemModelo_IsolationForestFicaNaoAplicavel_EODiagnosticoNaoInventaScore()
    {
        using var fixture = new ModelFixture();
        var input = CreateInput(fixture.ClienteId, fixture.TagName);

        var ensembleResult = CreateEnsemble(fixture.CreateDetector()).Detectar(input);
        var detail = Assert.Single(ensembleResult.DetalhesDetectores);

        Assert.Equal("Isolation Forest", detail.Nome);
        Assert.Equal(DetectorDiagnosticStatus.NotApplicable, detail.ExecutionStatus);
        Assert.False(detail.Aplicavel);
        Assert.Contains("Não existe modelo Isolation Forest treinado", detail.Explicacao);
        Assert.Equal(0, ensembleResult.Metricas["quantidadeDetectoresAplicaveis"]);

        var diagnostic = Capture(ensembleResult, input);
        var detector = Assert.Single(diagnostic.Detectors);
        Assert.Equal(DetectorDiagnosticStatus.NotApplicable, detector.Status);
        Assert.Null(detector.OriginalScore);
        Assert.Null(detector.NormalizedScore);
        Assert.False(detector.VotedAnomaly);
        Assert.Empty(detector.Metrics);
    }

    [Fact]
    public async Task ModeloTreinado_IsolationForestExecutaNoEnsemble_EMetricasChegamAoDiagnostico()
    {
        using var fixture = new ModelFixture();
        var input = CreateInput(fixture.ClienteId, fixture.TagName);
        var options = fixture.Options;
        var extractor = new IsolationForestFeatureExtractor();

        // Treino local deterministico pelo componente real, sem banco ou endpoint.
        var features = extractor.ExtrairTreinamento(input, options.FeatureWindowSize);
        var samples = features
            .Select(feature => (IReadOnlyList<double>)feature.ParaVetor().ToArray())
            .ToList();
        Assert.NotEmpty(samples);

        var model = new IsolationForestModel(
            quantidadeArvores: options.TreeCount,
            tamanhoAmostra: options.SampleSize,
            sementeAleatoria: options.RandomSeed);
        model.Treinar(samples);

        var snapshot = model.ExportarSnapshot(
            fixture.ClienteId,
            fixture.TagName,
            options.FeatureWindowSize,
            options.AnomalyThreshold,
            ReadingTime.AddDays(-1));
        snapshot.VersaoModelo = 3;
        snapshot.QuantidadePontosTreinamento = input.Historico.Count;
        await fixture.Persistence.SalvarAsync(snapshot);

        var ensembleResult = CreateEnsemble(fixture.CreateDetector()).Detectar(input);
        var detail = Assert.Single(ensembleResult.DetalhesDetectores);

        Assert.Equal("Isolation Forest", detail.Nome);
        Assert.Equal(DetectorDiagnosticStatus.Executed, detail.ExecutionStatus);
        Assert.True(detail.Aplicavel);
        Assert.True(double.IsFinite(detail.ScoreOriginal));
        Assert.InRange(detail.ScoreNormalizado, 0, 100);
        Assert.Equal(options.Weight, detail.Peso, precision: 6);
        Assert.Equal(options.AnomalyThreshold, detail.Metricas["threshold"], precision: 6);
        Assert.Equal(options.TreeCount, detail.Metricas["quantidadeArvores"]);
        Assert.Equal(3, detail.Metricas["versaoModelo"]);
        Assert.Equal(input.Historico.Count, detail.Metricas["quantidadePontosTreinamento"]);
        Assert.Equal(1, ensembleResult.Metricas["quantidadeDetectoresAplicaveis"]);

        var diagnostic = Capture(ensembleResult, input);
        var detector = Assert.Single(diagnostic.Detectors);
        Assert.Equal(DetectorDiagnosticStatus.Executed, detector.Status);
        Assert.Equal(detail.ScoreOriginal, detector.OriginalScore);
        Assert.Equal(detail.ScoreNormalizado, detector.NormalizedScore);
        Assert.Equal(detail.VotouAnomalia, detector.VotedAnomaly);
        Assert.Equal(options.AnomalyThreshold, detector.Metrics["threshold"], precision: 6);
        Assert.Equal(options.TreeCount, detector.Metrics["quantidadeArvores"]);
        Assert.Equal(3, detector.Metrics["versaoModelo"]);
        Assert.NotNull(diagnostic.Ensemble);
        Assert.Equal(1, diagnostic.Ensemble!.ApplicableDetectorCount);
        Assert.Equal(ensembleResult.EhAnomalia, diagnostic.Ensemble.LegacyAnomaly);

        // O threshold e preservado em Metrics. Na versao analisada,
        // DetectorDiagnostic.Threshold permanece null; melhorar isso numa
        // etapa futura sem prender o teste ao formato parcial atual.
    }

    private static EnsembleDetectionService CreateEnsemble(IAnomalyDetector detector) =>
        new(
            new[] { detector },
            Options.Create(new AnomalyDetectionOptions()),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());

    private static DetectorInput CreateInput(string client, string tag)
    {
        var history = Enumerable.Range(0, 96)
            .Select(i => new HistoricoDetectorPoint
            {
                DataHora = ReadingTime.AddMinutes(i - 96),
                Valor = 70 + 2 * Math.Sin(i / 6.0) + 0.35 * Math.Cos(i / 3.0)
            })
            .ToArray();

        return new DetectorInput
        {
            ClienteId = client,
            TagName = tag,
            TipoTag = "continuous",
            ValorAtual = 70.5,
            DataHoraAtual = ReadingTime,
            MediaHistorica = history.Average(point => point.Valor),
            DesvioPadraoHistorico = 2,
            VariacaoMinimaRelevante = 0.1,
            ConfiancaVariacaoMinima = 1,
            Historico = history
        };
    }

    private static IntelligenceLabReadingDiagnostic Capture(
        DetectorResult ensembleResult,
        DetectorInput input)
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var decision = new RuntimeAnomalyDecision(
            LegacyFinalAnomaly: ensembleResult.EhAnomalia,
            EmitAnomaly: ensembleResult.EhAnomalia,
            EmitNewEvidence: ensembleResult.EhAnomalia,
            UsedLegacyFallback: true,
            Source: "legacy",
            Reasons: []);

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer),
            new DiagnosticVersionMetadata("lab", "isolated-test", "not-committed", "if-test"),
            new AnomaliaRequest
            {
                ClienteId = input.ClienteId,
                TagName = input.TagName,
                Valor = (float)input.ValorAtual,
                DataHora = ReadingTime
            },
            ReadingTime,
            null,
            ensembleResult,
            decision,
            TimeSpan.FromMilliseconds(2),
            DateTimeOffset.UtcNow);

        return Assert.Single(buffer.Snapshot());
    }

    private sealed class ModelFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "IAApi-IntelligenceLab-IsolationForest", Guid.NewGuid().ToString("N"));

        public string ClienteId { get; } = "IL-IF-" + Guid.NewGuid().ToString("N");
        public string TagName { get; } = "BOMBA.PRESSAO";

        public IsolationForestOptions Options { get; } = new()
        {
            Weight = 0.25,
            TreeCount = 16,
            SampleSize = 32,
            MinimumHistoryPoints = 50,
            FeatureWindowSize = 10,
            AnomalyThreshold = 0.62,
            RandomSeed = 42
        };

        public IsolationForestPersistenceService Persistence { get; }

        public ModelFixture()
        {
            Directory.CreateDirectory(_root);
            Persistence = new IsolationForestPersistenceService(new TestEnvironment(_root));
        }

        public IsolationForestDetector CreateDetector() =>
            new(OptionsFactory(), new IsolationForestFeatureExtractor(), Persistence);

        private IOptions<IsolationForestOptions> OptionsFactory() =>
            Microsoft.Extensions.Options.Options.Create(Options);

        public void Dispose()
        {
            IsolationForestDetector.RemoverDoCache(ClienteId, TagName);
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "IAApi.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
