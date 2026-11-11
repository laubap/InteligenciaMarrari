using System.Text.Json;
using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using IAApi.Services.Diagnostics;

namespace IAApi.Tests.IntelligenceLab;

/// <summary>
/// Exercita a cadeia real PredictionService -> Ensemble -> runtime ->
/// finalizacao/persistencia -> captura passiva com bancos isolados.
/// Sem Bridge fisico, LLM remoto, DI de producao ou alteracao do runtime.
/// </summary>
public sealed class PredictionServiceObservabilityEquivalenceTests
{
    private static readonly DateTime ReadingTime =
        new(2026, 10, 23, 14, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PerformanceDiagnostics_MantemRespostaPersistenciaEQuantidadeDeConsultas(bool failingLogger)
    {
        var logger = new PerfTestLogger { Throw = failingLogger };
        var off = await ExecuteAsync(Mode.Off);
        var on = await ExecuteAsync(Mode.Off,
            new PerformanceDiagnostics("Development", "1", logger));
        AssertEquivalent(off, on);
        Assert.Equal(1, on.DetectorInvocations);
        Assert.Equal(2, off.HistoryCalls);
        Assert.Equal(2, on.HistoryCalls); // Preparation and value trend still fetch independently.
        if (failingLogger) return;
        Assert.Contains(logger.Events, e => Equals(e["Stage"], "Preparation.History"));
        Assert.Contains(logger.Events, e => Equals(e["Stage"], "ValueTrend.History"));
        var total = Assert.Single(logger.Events, e => Equals(e["Stage"], "Prediction.Total"));
        Assert.Equal("Succeeded", total["Status"]);
        Assert.All(logger.Events, e => Assert.Equal(total["TraceId"], e["TraceId"]));
        var directChildren = logger.Events.Where(e => Equals(e["ParentSpanId"], total["SpanId"]));
        Assert.True(directChildren.Sum(e => (double)e["DurationMs"]!) <= (double)total["DurationMs"]!);
    }

    [Fact]
    public async Task ObservabilidadeLigada_MantemRespostaHistoricoEDetector()
    {
        var off = await ExecuteAsync(Mode.Off);
        var on = await ExecuteAsync(Mode.On);

        AssertEquivalent(off, on);
        Assert.Equal(1, off.DetectorInvocations);
        Assert.Equal(1, on.DetectorInvocations);
        Assert.Equal(0, off.DiagnosticCount);
        Assert.Equal(1, on.DiagnosticCount);
        Assert.Equal(0, off.CaptureAttempts);
        Assert.Equal(1, on.CaptureAttempts);
        Assert.Equal(1, on.StoredTotal);
        Assert.Equal(0, on.CaptureFailures);
        Assert.Equal(DiagnosticCompletenessStatus.Partial, on.Completeness);
        Assert.NotNull(on.DiagnosticOriginalScore);
        Assert.Equal((double)on.Score, on.DiagnosticOriginalScore.Value, precision: 5);
        Assert.NotNull(on.DiagnosticFinalAnomaly);
        Assert.Equal(on.FinalAnomaly, on.DiagnosticFinalAnomaly.Value);
    }

    [Fact]
    public async Task FalhaNoColetor_NaoMudaRespostaHistoricoOuExecucoes()
    {
        var off = await ExecuteAsync(Mode.Off);
        var failing = await ExecuteAsync(Mode.Throwing);

        AssertEquivalent(off, failing);
        Assert.Equal(1, failing.DetectorInvocations);
        Assert.Equal(0, failing.DiagnosticCount);
        Assert.Equal(1, failing.CaptureAttempts);
        Assert.Equal(0, failing.StoredTotal);
        Assert.Equal(1, failing.CaptureFailures);
    }

    private static void AssertEquivalent(RunSnapshot expected, RunSnapshot actual)
    {
        Assert.Equal(expected.Score, actual.Score);
        Assert.Equal(expected.FinalAnomaly, actual.FinalAnomaly);
        Assert.Equal(expected.ResponseDetectorsJson, actual.ResponseDetectorsJson);
        Assert.Equal(expected.ResponseSummaryJson, actual.ResponseSummaryJson);
        Assert.Equal(expected.HistoryCount, actual.HistoryCount);
        Assert.Equal(1, actual.HistoryCount);
        Assert.Equal(expected.HistoryScore, actual.HistoryScore);
        Assert.Equal(expected.HistoryAnomaly, actual.HistoryAnomaly);
        Assert.Equal(expected.HistorySource, actual.HistorySource);
        Assert.Equal(expected.HistoryLegacyAnomaly, actual.HistoryLegacyAnomaly);
        Assert.Equal(expected.HistoryReasonsJson, actual.HistoryReasonsJson);
    }

    private static async Task<RunSnapshot> ExecuteAsync(Mode mode, PerformanceDiagnostics? performance = null)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        const string client = "LAB-TEST";
        const string tag = "BOMBA_VAZAO";
        db.TagsIaConfig.Add(new TagIaConfig
        {
            ClienteId = client, TagName = tag, IaAtiva = true
        });
        db.PerfisIa.Add(new TagPerfilIa
        {
            ClienteId = client, TagName = tag,
            Media = 100, DesvioPadrao = 2,
            VariacaoMinimaRelevante = 5,
            ConfiancaDeadband = 1,
            DataTreinamento = ReadingTime.AddDays(-1)
        });
        db.TagsContextoIa.Add(new TagContextoIa
        {
            ClienteId = client, TagName = tag,
            TipoRepresentacao = "continuous"
        });
        await db.SaveChangesAsync();

        var readings = Enumerable.Range(0, 40)
            .Select(i => new LeituraBruta
            {
                DataHora = ReadingTime.AddMinutes(i - 41),
                Valor = (float)(99 + (i % 5) * 0.53)
            }).ToList();
        var history = new FixedHistory(readings);
        var detector = new NormalDetector();
        var ensemble = new EnsembleDetectionService(
            new IAnomalyDetector[] { detector },
            Options.Create(new AnomalyDetectionOptions()),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());

        var interpretation = new PredictionInterpretationService(
            new AnomalyInterpretationContextBuilder(new TagContextoIaService(db)),
            new DeterministicInterpretation());
        var runtimeContext = new RuntimeFunctionalDecisionContextProvider(
            new RuntimeFunctionalDecisionStateStore(),
            Options.Create(new AnomalyDetectionOptions()));
        var runtimeDecision = new PredictionRuntimeDecisionService(
            new RuntimeFunctionalDecisionCandidateFactory(),
            new RuntimeAnomalyDecisionResolver(),
            new RuntimeAnomalyDecisionOptions { Enabled = false },
            new RuntimeDecisionMetrics());
        var decision = new PredictionDecisionOrchestrationService(
            ensemble, interpretation, runtimeContext, runtimeDecision, performanceDiagnostics: performance);
        var postDecision = new PredictionPostDecisionAnalysisService(
            new AnaliseRelacionadasService(db, null!),
            new PredictionRiskAnalysisService(
                new RiskScoreService(), new ExplicacaoRiscoService()),
            new PredictionTrendAnalysisService(
                new TendenciaRiscoService(db), new TendenciaValorService(history, performance), performance),
            new FeedbackAnomaliaAnaliseService(db), performanceDiagnostics: performance);
        var finalization = new PredictionFinalizationService(
            new AnomaliaHistoricoService(db),
            new PredictionResponseBuilder(
                new PredictionDependencyMapper(new InterpretacaoDependenciasService())));

        var buffer = new BoundedDiagnosticBuffer(10, enabled: true);
        IIntelligenceLabDiagnosticCollector? collector = mode switch
        {
            Mode.On => new InMemoryDiagnosticCollector(buffer),
            Mode.Throwing => new FailingCollector(),
            _ => null
        };
        var version = mode == Mode.Off ? null :
            new DiagnosticVersionMetadata("test-api", "test-build", "test-commit", "test-config");
        var service = new PredictionService(
            new PredictionContextLoader(db, new ProcessoAnaliseService(db)),
            postDecision, finalization, decision,
            new PredictionDetectionPreparationService(history, performanceDiagnostics: performance),
            multivariateShadowRuntimeCoordinator: null,
            diagnosticCollector: collector,
            diagnosticVersion: version,
            diagnosticHealth: mode == Mode.Off ? null : buffer.Health,
            performanceDiagnostics: performance);

        var response = await service.Detectar(new AnomaliaRequest
        {
            ClienteId = client, TagName = tag,
            Valor = 100, DataHora = ReadingTime
        });
        var historical = await db.AnomaliasDetectadas.AsNoTracking().ToListAsync();
        var saved = Assert.Single(historical);
        var diagnostics = buffer.Snapshot();
        var health = buffer.Health.Snapshot(buffer);
        var captured = mode == Mode.On ? Assert.Single(diagnostics) : null;

        return new RunSnapshot(
            response.Score, response.EhAnomalia,
            JsonSerializer.Serialize(response.Detectores),
            JsonSerializer.Serialize(response.Resumo),
            historical.Count, saved.Score, saved.EhAnomalia,
            saved.RuntimeDecisionSource,
            saved.LegacyEhAnomalia,
            saved.RuntimeDecisionReasonsJson,
            detector.Invocations, diagnostics.Count,
            captured?.Completeness.Status,
            captured?.Ensemble?.OriginalScore,
            captured?.RuntimeDecision?.FinalAnomaly,
            health.CaptureAttempts, health.StoredTotal, health.CaptureFailures, history.Calls);
    }

    private enum Mode { Off, On, Throwing }

    private sealed record RunSnapshot(
        double Score,
        bool FinalAnomaly,
        string ResponseDetectorsJson,
        string ResponseSummaryJson,
        int HistoryCount,
        double HistoryScore,
        bool HistoryAnomaly,
        string? HistorySource,
        bool? HistoryLegacyAnomaly,
        string? HistoryReasonsJson,
        int DetectorInvocations,
        int DiagnosticCount,
        DiagnosticCompletenessStatus? Completeness,
        double? DiagnosticOriginalScore,
        bool? DiagnosticFinalAnomaly,
        long CaptureAttempts,
        long StoredTotal,
        long CaptureFailures,
        int HistoryCalls);

    private sealed class NormalDetector : IAnomalyDetector
    {
        public string Nome => "NormalDetector";
        public double Peso => 1;
        public int Invocations { get; private set; }
        public DetectorResult Detectar(DetectorInput input)
        {
            Invocations++;
            return new DetectorResult
            {
                Aplicavel = true, EhAnomalia = false,
                Score = 0, ScoreNormalizado = 0, Peso = Peso,
                Detector = Nome, Explicacao = "Normal", Media = input.MediaHistorica,
                DesvioPadrao = input.DesvioPadraoHistorico
            };
        }
    }

    private sealed class FixedHistory(List<LeituraBruta> values) : IHistoricoService
    {
        public int Calls { get; private set; }
        public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag)
        { Calls++; return values.ToList(); }
        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId, string tagName, DateTime inicio, DateTime fim)
            => values.Where(x => x.DataHora >= inicio && x.DataHora <= fim).ToList();
    }

    private sealed class DeterministicInterpretation : IAnomalyInterpretationService
    {
        public Task<AnomalyInterpretationResult> InterpretarAsync(
            AnomalyInterpretationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AnomalyInterpretationResult
            {
                Resumo = "Teste", InterpretacaoTecnica = "Teste",
                Severidade = "Normal", ModeloUtilizado = "Teste"
            });
    }

    private sealed class FailingCollector : IIntelligenceLabDiagnosticCollector
    {
        public IIntelligenceLabDiagnosticSession Begin(
            DiagnosticContext context, DiagnosticVersionMetadata version)
            => throw new InvalidOperationException("Falha artificial da telemetria");
    }
}
