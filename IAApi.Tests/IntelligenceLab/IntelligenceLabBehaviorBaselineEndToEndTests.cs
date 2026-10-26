using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IAApi.Tests.IntelligenceLab;

/// <summary>
/// Intelligence Lab 2.2G.2, primeira etapa: exercita o ciclo completo de
/// previsão com SQLite isolado, detectores estatísticos reais e somente a
/// fronteira de aquisição de histórico simulada.
///
/// Não representa a configuração de produção completa: a decisão funcional
/// avançada permanece desabilitada, Isolation Forest e multivariada não são
/// registrados, e as opções hospedadas não são carregadas por esta fixture.
/// </summary>
public sealed class IntelligenceLabBehaviorBaselineEndToEndTests
{
    private const string ClienteId = "IL-BASELINE";
    private const string TagName = "BOMBA.PRESSAO";

    private static readonly DateTime ReadingTime =
        new(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc);

    private static readonly string[] StatisticalDetectorNames =
    [
        "Z-Score",
        "Mediana e MAD",
        "EWMA",
        "Taxa de variação",
        "Persistência",
        "Mudança de patamar"
    ];

    [Fact]
    public async Task GlobalBaseline_StableHistory_CompletesRealPredictionWithoutCreatingBehaviorMemory()
    {
        var history = CreateStableHistory(center: 100);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);

        var run = await ExecutePredictionAsync(
            fixture.Context,
            history);

        Assert.NotNull(run.Response);
        Assert.Equal(ClienteId, run.Response.ClienteId);
        Assert.Equal(TagName, run.Response.TagName);
        Assert.True(double.IsFinite(run.Response.Score));
        Assert.NotNull(run.Response.Interpretacao);

        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.Global, run.Diagnostic.Baseline!.Kind);
        Assert.True(run.Diagnostic.Baseline.Applied);
        Assert.Equal(100, run.Diagnostic.Baseline.Mean, precision: 6);
        Assert.Equal(2, run.Diagnostic.Baseline.StandardDeviation, precision: 6);
        Assert.Null(run.Diagnostic.Baseline.BehaviorCode);
        Assert.Null(run.Diagnostic.Baseline.BehaviorSnapshotVersion);

        AssertRealStatisticalDetectorsExecuted(run);

        var persisted = await fixture.Context.AnomaliasDetectadas
            .AsNoTracking()
            .ToListAsync();

        var operationalResult = Assert.Single(persisted);
        Assert.Equal(ClienteId, operationalResult.ClienteId);
        Assert.Equal(TagName, operationalResult.TagName);
        Assert.Equal(run.Response.EhAnomalia, operationalResult.EhAnomalia);
        Assert.Equal((double)run.Response.Score, operationalResult.Score, precision: 5);

        Assert.Empty(await fixture.Context.TagsComportamentosIa.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Context.TagsComportamentosSnapshotsIa.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task KnownNormalBehavior_AppliesBehaviorBaselineToDetectorAndPreservesMemory()
    {
        var behaviorHistory = CreateStableHistory(center: 40);

        await using var fixture = await ScenarioFixture.CreateAsync();
        await SeedPredictionRequirementsAsync(fixture.Context);
        var snapshot = await SeedKnownNormalBehaviorAsync(fixture.Context, behaviorHistory);

        var memoryBefore = await CaptureBehaviorMemoryAsync(fixture.Context);

        var historySource = new InMemoryHistorySource(behaviorHistory);
        var behaviorProvider = CreateBehaviorProvider(fixture.Context);
        var preparation = CreatePreparationService(
            fixture.Context,
            historySource,
            behaviorProvider);

        // Confere diretamente o contrato que será consumido pelo serviço de
        // produção: reconhecimento, baseline e o DetectorInput resultante.
        var prepared = await preparation.PrepararAsync(
            CreateRequest(behaviorHistory),
            await fixture.Context.PerfisIa.SingleAsync(),
            "continuous",
            ReadingTime);

        Assert.True(prepared.ComportamentoReconhecido);
        Assert.True(prepared.BaselineComportamentalAplicado);
        Assert.Equal("BEH-001", prepared.CodigoComportamentoReconhecido);
        Assert.Equal(TagComportamentoIa.ClassificacaoNormal, prepared.ClassificacaoComportamentoReconhecido);
        Assert.NotNull(prepared.BaselineEfetivo);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, prepared.BaselineEfetivo!.Kind);
        Assert.Equal("BEH-001", prepared.BaselineEfetivo.BehaviorCode);
        Assert.Equal(snapshot.Versao, prepared.BaselineEfetivo.BehaviorSnapshotVersion);
        Assert.Equal(snapshot.Media, prepared.EntradaDetector.MediaHistorica, precision: 6);
        Assert.Equal(snapshot.DesvioPadrao, prepared.EntradaDetector.DesvioPadraoHistorico, precision: 6);
        Assert.Equal(snapshot.DeadbandRecomendado, prepared.EntradaDetector.VariacaoMinimaRelevante, precision: 6);
        Assert.Equal(snapshot.ConfiancaDeadband, prepared.EntradaDetector.ConfiancaVariacaoMinima, precision: 6);

        var globalProfile = await fixture.Context.PerfisIa.SingleAsync();
        Assert.NotEqual(globalProfile.Media, prepared.EntradaDetector.MediaHistorica);
        Assert.NotEqual(globalProfile.DesvioPadrao, prepared.EntradaDetector.DesvioPadraoHistorico);
        Assert.NotEqual(globalProfile.VariacaoMinimaRelevante, prepared.EntradaDetector.VariacaoMinimaRelevante);

        var run = await ExecutePredictionAsync(
            fixture.Context,
            behaviorHistory,
            historySource,
            behaviorProvider,
            preparation);

        Assert.NotNull(run.Response);
        Assert.Equal(ClienteId, run.Response.ClienteId);
        Assert.Equal(TagName, run.Response.TagName);
        Assert.True(double.IsFinite(run.Response.Score));
        Assert.NotNull(run.Response.Interpretacao);
        Assert.Equal(snapshot.Media, run.Response.Perfil.MediaHistorica, precision: 6);

        Assert.NotNull(run.Diagnostic.Baseline);
        Assert.Equal(BaselineDiagnosticKind.BehaviorMemory, run.Diagnostic.Baseline!.Kind);
        Assert.True(run.Diagnostic.Baseline.Applied);
        Assert.Equal("BEH-001", run.Diagnostic.Baseline.BehaviorCode);
        Assert.Equal(snapshot.Versao, run.Diagnostic.Baseline.BehaviorSnapshotVersion);
        Assert.Equal(snapshot.Media, run.Diagnostic.Baseline.Mean, precision: 6);
        Assert.Equal(behaviorHistory.Length, run.Diagnostic.Baseline.SampleCount);
        Assert.NotEqual(globalProfile.Media, run.Diagnostic.Baseline.Mean);
        Assert.NotEqual(globalProfile.DesvioPadrao, run.Diagnostic.Baseline.StandardDeviation);
        Assert.NotEqual(globalProfile.VariacaoMinimaRelevante, run.Diagnostic.Baseline.Deadband);

        AssertRealStatisticalDetectorsExecuted(run);

        var persisted = await fixture.Context.AnomaliasDetectadas
            .AsNoTracking()
            .ToListAsync();

        var operationalResult = Assert.Single(persisted);
        Assert.Equal(ClienteId, operationalResult.ClienteId);
        Assert.Equal(TagName, operationalResult.TagName);
        Assert.Equal(run.Response.EhAnomalia, operationalResult.EhAnomalia);
        Assert.Equal((double)run.Response.Score, operationalResult.Score, precision: 5);

        // O provider B3.25 é read-only inclusive quando consultado mais de
        // uma vez (preparação explícita e previsão completa). A comparação
        // cobre a identidade, o lifecycle e todas as estatísticas persistidas
        // no snapshot, não apenas a quantidade de registros.
        Assert.Equal(memoryBefore, await CaptureBehaviorMemoryAsync(fixture.Context));
    }

    private static async Task<PredictionRun> ExecutePredictionAsync(
        AppDbContext context,
        IReadOnlyList<LeituraBruta> history,
        InMemoryHistorySource? historySource = null,
        BehaviorRuntimeBaselineProvider? behaviorProvider = null,
        PredictionDetectionPreparationService? preparation = null)
    {
        historySource ??= new InMemoryHistorySource(history);
        behaviorProvider ??= CreateBehaviorProvider(context);
        preparation ??= CreatePreparationService(context, historySource, behaviorProvider);

        var options = new AnomalyDetectionOptions();
        var ensemble = new EnsembleDetectionService(
            CreateRealStatisticalDetectors(options),
            Options.Create(options),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());

        var interpretation = new PredictionInterpretationService(
            new AnomalyInterpretationContextBuilder(new TagContextoIaService(context)),
            new RuleBasedAnomalyInterpretationService());

        var runtimeContext = new RuntimeFunctionalDecisionContextProvider(
            new RuntimeFunctionalDecisionStateStore(),
            Options.Create(options));

        var runtimeDecision = new PredictionRuntimeDecisionService(
            new RuntimeFunctionalDecisionCandidateFactory(),
            new RuntimeAnomalyDecisionResolver(),
            new RuntimeAnomalyDecisionOptions { Enabled = false },
            new RuntimeDecisionMetrics());

        var decision = new PredictionDecisionOrchestrationService(
            ensemble,
            interpretation,
            runtimeContext,
            runtimeDecision);

        // O contrato atual depende da classe concreta BridgeValorAtualService,
        // sem interface nem método virtual para um fake. A fronteira é segura
        // neste cenário porque não há dependências cadastradas: o serviço real
        // retorna antes de chamar BuscarValoresAtuais. Se esse fluxo mudar, a
        // chamada tentará localhost e o teste falhará; esta fixture não deve
        // ser entendida como cobertura do Bridge.
        Assert.Empty(context.TagDependenciasIa.AsNoTracking().ToList());
        var postDecision = new PredictionPostDecisionAnalysisService(
            new AnaliseRelacionadasService(context, new BridgeValorAtualService()),
            new PredictionRiskAnalysisService(
                new RiskScoreService(),
                new ExplicacaoRiscoService()),
            new PredictionTrendAnalysisService(
                new TendenciaRiscoService(context),
                new TendenciaValorService(historySource)),
            new FeedbackAnomaliaAnaliseService(context));

        var finalization = new PredictionFinalizationService(
            new AnomaliaHistoricoService(context),
            new PredictionResponseBuilder(
                new PredictionDependencyMapper(
                    new InterpretacaoDependenciasService())));

        var buffer = new BoundedDiagnosticBuffer(capacity: 10, enabled: true);
        var service = new PredictionService(
            new PredictionContextLoader(context, new ProcessoAnaliseService(context)),
            postDecision,
            finalization,
            decision,
            preparation,
            multivariateShadowRuntimeCoordinator: null,
            diagnosticCollector: new InMemoryDiagnosticCollector(buffer),
            diagnosticVersion: new DiagnosticVersionMetadata(
                "intelligence-lab-2.2G.2",
                "test-build",
                "test-commit",
                "production-defaults"),
            diagnosticHealth: buffer.Health);

        var response = await service.Detectar(CreateRequest(history));
        var diagnostic = Assert.Single(buffer.Snapshot());

        return new PredictionRun(response, diagnostic);
    }

    private static PredictionDetectionPreparationService CreatePreparationService(
        AppDbContext context,
        IHistoricoService historySource,
        BehaviorRuntimeBaselineProvider behaviorProvider)
    {
        var regimeProvider = new OperationalRegimeBaselineProvider(
            context,
            historySource,
            new OperationalRegimeBaselineService(
                new OperationalRegimeHistoryResolver(),
                new TagProfileStatisticsCalculator(
                    new HistoricoLimpezaService())));

        return new PredictionDetectionPreparationService(
            historySource,
            regimeProvider,
            behaviorProvider);
    }

    private static BehaviorRuntimeBaselineProvider CreateBehaviorProvider(AppDbContext context)
    {
        var statistics = new TagProfileStatisticsCalculator(new HistoricoLimpezaService());
        var characterizer = new BehaviorCandidateEpisodeCharacterizer(statistics);
        var planBuilder = new BehaviorMemoryPersistencePlanBuilder();

        return new BehaviorRuntimeBaselineProvider(
            context,
            new BehaviorHistorySegmentationService(),
            new BehaviorChangePointDetector(),
            new BehaviorCandidateEpisodeBuilder(),
            characterizer,
            new BehaviorMemoryReconciliationService(context, planBuilder));
    }

    private static IAnomalyDetector[] CreateRealStatisticalDetectors(
        AnomalyDetectionOptions options) =>
    [
        new StatisticalDetector(options),
        new MadDetector(options),
        new EwmaDetector(options),
        new RateOfChangeDetector(options),
        new PersistenceDetector(options),
        new LevelShiftDetector(options)
    ];

    private static void AssertRealStatisticalDetectorsExecuted(PredictionRun run)
    {
        Assert.Equal(6, run.Response.Detectores.Count);
        Assert.Equal(6, run.Diagnostic.Detectors.Count);
        Assert.Equal(6, run.Diagnostic.Ensemble!.RegisteredDetectorCount);

        foreach (var name in StatisticalDetectorNames)
        {
            Assert.Contains(run.Response.Detectores, detector => detector.Nome == name && detector.Aplicavel);
            Assert.Contains(run.Diagnostic.Detectors, detector =>
                detector.Name == name && detector.Status == DetectorDiagnosticStatus.Executed);
        }
    }

    private static async Task SeedPredictionRequirementsAsync(AppDbContext context)
    {
        context.TagsIaConfig.Add(new TagIaConfig
        {
            ClienteId = ClienteId,
            TagName = TagName,
            IaAtiva = true
        });

        // O perfil global permanece propositalmente distante do BEH do
        // segundo cenário para tornar observável a troca real de baseline.
        context.PerfisIa.Add(new TagPerfilIa
        {
            ClienteId = ClienteId,
            TagName = TagName,
            Media = 100,
            DesvioPadrao = 2,
            VariacaoMinimaRelevante = 5,
            DeadbandSugerido = 5,
            ConfiancaDeadband = 94,
            VariacaoMinimaCalculadaAutomaticamente = true,
            DataTreinamento = ReadingTime.AddDays(-1)
        });

        context.TagsContextoIa.Add(new TagContextoIa
        {
            ClienteId = ClienteId,
            TagName = TagName,
            TipoRepresentacao = "continuous"
        });

        await context.SaveChangesAsync();
    }

    private static async Task<TagComportamentoSnapshotIa> SeedKnownNormalBehaviorAsync(
        AppDbContext context,
        IReadOnlyList<LeituraBruta> history)
    {
        var behavior = new TagComportamentoIa
        {
            ClienteId = ClienteId,
            TagName = TagName,
            Codigo = "BEH-001",
            Status = TagComportamentoIa.StatusKnown,
            Origem = TagComportamentoIa.OrigemAutomatic,
            Classificacao = TagComportamentoIa.ClassificacaoNormal,
            PrimeiraObservacao = history[0].DataHora,
            UltimaObservacao = history[^1].DataHora,
            QuantidadeOcorrencias = 1,
            TotalAmostras = history.Count,
            Confianca = 90,
            DataCriacao = DateTime.UtcNow
        };

        context.TagsComportamentosIa.Add(behavior);
        await context.SaveChangesAsync();

        var characterizer = new BehaviorCandidateEpisodeCharacterizer(
            new TagProfileStatisticsCalculator(new HistoricoLimpezaService()));
        var characteristics = characterizer.Characterize(new BehaviorCandidateEpisode
        {
            SourceSegmentIndex = 0,
            EpisodeIndex = 0,
            StartReadingIndex = 0,
            EndReadingIndex = history.Count - 1,
            Inicio = history[0].DataHora,
            Fim = history[^1].DataHora,
            Historico = history
        });

        Assert.True(characteristics.IsCharacterizable, characteristics.Reason);

        var snapshot = new BehaviorMemoryPersistencePlanBuilder().BuildSnapshotPreview(
            [characteristics],
            version: 1,
            createdAtUtc: DateTime.UtcNow);
        snapshot.TagComportamentoIaId = behavior.Id;

        context.TagsComportamentosSnapshotsIa.Add(snapshot);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return snapshot;
    }

    private static async Task<BehaviorMemoryState> CaptureBehaviorMemoryAsync(
        AppDbContext context)
    {
        var behavior = await context.TagsComportamentosIa
            .AsNoTracking()
            .SingleAsync();
        var snapshot = await context.TagsComportamentosSnapshotsIa
            .AsNoTracking()
            .SingleAsync();

        return new BehaviorMemoryState(
            behavior.Id,
            behavior.ClienteId,
            behavior.TagName,
            behavior.Codigo,
            behavior.Status,
            behavior.Origem,
            behavior.Classificacao,
            behavior.PrimeiraObservacao,
            behavior.UltimaObservacao,
            behavior.QuantidadeOcorrencias,
            behavior.TotalAmostras,
            behavior.Confianca,
            behavior.DataCriacao,
            behavior.DataAtualizacao,
            snapshot.Id,
            snapshot.TagComportamentoIaId,
            snapshot.Versao,
            snapshot.Media,
            snapshot.DesvioPadrao,
            snapshot.Mediana,
            snapshot.PrimeiroQuartil,
            snapshot.TerceiroQuartil,
            snapshot.IntervaloInterquartil,
            snapshot.RobustScale,
            snapshot.TypicalAbsoluteStep,
            snapshot.Minimo,
            snapshot.Maximo,
            snapshot.Amplitude,
            snapshot.PercentualZeros,
            snapshot.VariacaoMedia,
            snapshot.QuantidadePicos,
            snapshot.TotalRegistrosHistorico,
            snapshot.TotalRegistrosUsados,
            snapshot.TotalOutliersRemovidos,
            snapshot.DeadbandRecomendado,
            snapshot.ConfiancaDeadband,
            snapshot.JustificativaDeadband,
            snapshot.PeriodoInicio,
            snapshot.PeriodoFim,
            snapshot.DataCriacao);
    }

    private static AnomaliaRequest CreateRequest(IReadOnlyList<LeituraBruta> history) => new()
    {
        ClienteId = ClienteId,
        TagName = TagName,
        Valor = history[^1].Valor,
        DataHora = ReadingTime
    };

    private static LeituraBruta[] CreateStableHistory(double center)
    {
        double[] offsets = [0.00, 0.12, -0.08, 0.18, -0.14, 0.05, 0.16, -0.11, 0.09, -0.04];

        return Enumerable.Range(0, 40)
            .Select(index => new LeituraBruta
            {
                DataHora = ReadingTime.AddMinutes(index - 40),
                Valor = Convert.ToSingle(center + offsets[index % offsets.Length])
            })
            .ToArray();
    }

    private sealed record PredictionRun(
        AnomaliaResponse Response,
        IntelligenceLabReadingDiagnostic Diagnostic);

    private sealed record BehaviorMemoryState(
        int BehaviorId,
        string ClienteId,
        string TagName,
        string Codigo,
        string Status,
        string Origem,
        string Classificacao,
        DateTime PrimeiraObservacao,
        DateTime UltimaObservacao,
        int QuantidadeOcorrencias,
        long TotalAmostras,
        double Confianca,
        DateTime DataCriacao,
        DateTime? DataAtualizacao,
        int SnapshotId,
        int SnapshotBehaviorId,
        int SnapshotVersao,
        double SnapshotMedia,
        double SnapshotDesvioPadrao,
        double? SnapshotMediana,
        double? SnapshotPrimeiroQuartil,
        double? SnapshotTerceiroQuartil,
        double? SnapshotIntervaloInterquartil,
        double? SnapshotRobustScale,
        double? SnapshotTypicalAbsoluteStep,
        double SnapshotMinimo,
        double SnapshotMaximo,
        double SnapshotAmplitude,
        double SnapshotPercentualZeros,
        double SnapshotVariacaoMedia,
        int SnapshotQuantidadePicos,
        int SnapshotTotalRegistrosHistorico,
        int SnapshotTotalRegistrosUsados,
        int SnapshotTotalOutliersRemovidos,
        double SnapshotDeadbandRecomendado,
        double SnapshotConfiancaDeadband,
        string? SnapshotJustificativaDeadband,
        DateTime SnapshotPeriodoInicio,
        DateTime SnapshotPeriodoFim,
        DateTime SnapshotDataCriacao);

    private sealed class InMemoryHistorySource(IReadOnlyList<LeituraBruta> readings) : IHistoricoService
    {
        public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag) =>
            readings.Select(Clone).ToList();

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim) =>
            readings.Where(item => item.DataHora >= inicio && item.DataHora <= fim)
                .Select(Clone)
                .ToList();

        private static LeituraBruta Clone(LeituraBruta reading) => new()
        {
            DataHora = reading.DataHora,
            Valor = reading.Valor
        };
    }

    private sealed class ScenarioFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ScenarioFixture(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<ScenarioFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await context.Database.EnsureCreatedAsync();

            return new ScenarioFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
