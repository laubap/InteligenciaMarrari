using IAApi.Services.AnomalyDetection;
using IAApi.IntelligenceLab.Observability;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using IAApi.Services.AnomalyDetection.Orchestration;

namespace IAApi.Tests;

public sealed class PredictionServiceBehaviorShadowWiringTests
{
    [Fact]
    public async Task BehaviorShadow_Presente_NaoDeveAlterarBaselineGlobalDoDetector()
    {
        await using var fixture =
            await TestFixture.CreateAsync();

        var context =
            fixture.Context;

        await SeedPredictionRequirementsAsync(
            context);

        var history =
            CreateDetectionHistory(
                new DateTime(
                    2026, 8, 21, 12, 0, 0,
                    DateTimeKind.Utc),
                40,
                center:
                    100);

        var captureDetector =
            new CaptureDetector();

        var behaviorProvider =
            CreateBehaviorProvider(
                context);

        var predictionService =
            CreatePredictionService(
                context,
                history,
                captureDetector,
                behaviorProvider);

        var request =
            new AnomaliaRequest
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Valor =
                    101,

                DataHora =
                    history[^1].DataHora
            };

        await Assert.ThrowsAsync<NullReferenceException>(
            async () =>
                await predictionService.Detectar(
                    request));

        Assert.NotNull(
            captureDetector.CapturedInput);

        var captured =
            captureDetector.CapturedInput!;

        Assert.Equal(
            100,
            captured.MediaHistorica);

        Assert.Equal(
            2,
            captured.DesvioPadraoHistorico);

        Assert.Equal(
            16,
            captured.VariacaoMinimaRelevante);

        Assert.Equal(
            94,
            captured.ConfiancaVariacaoMinima);

        Assert.Empty(
            await context.TagsComportamentosIa
                .AsNoTracking()
                .ToListAsync());

        Assert.Empty(
            await context.TagsComportamentosSnapshotsIa
                .AsNoTracking()
                .ToListAsync());
    }

    [Fact]
    public async Task BehaviorShadow_KnownNormalReconhecido_DeveAplicarBaselineComportamentalNoDetector()
    {
        await using var fixture =
            await TestFixture.CreateAsync();

        var context =
            fixture.Context;

        await SeedPredictionRequirementsAsync(
            context);

        /*
         * O comportamento conhecido fica propositalmente muito distante
         * do perfil global:
         *
         * Global = média 100 / desvio 2 / deadband 16.
         * BEH    = histórico estável em torno de 40.
         *
         * Assim provamos que, quando Known + Normal é reconhecido,
         * o DetectorInput deixa de usar o baseline global e passa
         * a receber exatamente o baseline comportamental resolvido.
         */
        var behaviorHistory =
            CreateStableBehaviorHistory(
                new DateTime(
                    2026, 10, 1, 10, 0, 0),
                40,
                center:
                    40);

        await SeedKnownNormalBehaviorAsync(
            context,
            behaviorHistory);

        var behaviorCountBefore =
            await context.TagsComportamentosIa
                .CountAsync();

        var snapshotCountBefore =
            await context
                .TagsComportamentosSnapshotsIa
                .CountAsync();

        var behaviorProvider =
            CreateBehaviorProvider(
                context);

        /*
         * Confirma primeiro que o cenário realmente é um
         * Known + Normal reconhecido e autorizado para baseline.
         */
        var resolution =
            await behaviorProvider.ResolveAsync(
                "CLIENTE-A",
                "TAG1",
                behaviorHistory,
                behaviorHistory[^1].DataHora);

        Assert.True(
            resolution.Recognized);

        Assert.True(
            resolution.CanApplyBaseline);

        Assert.Equal(
            "BEH-001",
            resolution.BehaviorCode);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoNormal,
            resolution.BehaviorClassification);

        Assert.NotNull(
            resolution.Media);

        Assert.InRange(
            resolution.Media!.Value,
            39,
            41);

        /*
         * Agora o mesmo provider entra no PredictionService.
         *
         * Nesta etapa o Known + Normal já possui autoridade
         * operacional quando nenhum regime explícito foi aplicado.
         */
        var captureDetector =
            new CaptureDetector();

        var predictionService =
            CreatePredictionService(
                context,
                behaviorHistory,
                captureDetector,
                behaviorProvider);

        var request =
            new AnomaliaRequest
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Valor =
                    40,

                DataHora =
                    behaviorHistory[^1].DataHora
            };

        var preparationResult =
            await new PredictionDetectionPreparationService(
                    new FakeHistoricoService(
                        behaviorHistory.ToList()),
                    operationalRegimeBaselineProvider: null,
                    behaviorRuntimeBaselineProvider:
                        behaviorProvider)
                .PrepararAsync(
                    request,
                    await context.PerfisIa.SingleAsync(),
                    "Analogica",
                    request.DataHora);

        Assert.NotNull(
            preparationResult.BaselineEfetivo);

        Assert.Equal(
            BaselineDiagnosticKind.BehaviorMemory,
            preparationResult.BaselineEfetivo!.Kind);

        Assert.True(
            preparationResult.BaselineEfetivo.Applied);

        Assert.Equal(
            resolution.BehaviorCode,
            preparationResult.BaselineEfetivo.BehaviorCode);

        Assert.Equal(
            resolution.SnapshotVersion,
            preparationResult.BaselineEfetivo.BehaviorSnapshotVersion);

        Assert.Equal(
            resolution.Media!.Value,
            preparationResult.BaselineEfetivo.Mean,
            precision: 6);

        Assert.Equal(
            preparationResult.EntradaDetector.Historico.Count,
            preparationResult.BaselineEfetivo.SampleCount);

        await Assert.ThrowsAsync<NullReferenceException>(
            async () =>
                await predictionService.Detectar(
                    request));

        Assert.NotNull(
            captureDetector.CapturedInput);

        var captured =
            captureDetector.CapturedInput!;

        /*
         * CONTRATO OPERACIONAL B3.25:
         *
         * O BEH existe, é Known, é Normal e CanApplyBaseline=true.
         * Sem regime explícito, o DetectorInput deve receber exatamente
         * o baseline comportamental resolvido pelo provider.
         */
        Assert.NotNull(
            resolution.Media);

        Assert.NotNull(
            resolution.DesvioPadrao);

        Assert.NotNull(
            resolution.DeadbandRecomendado);

        Assert.NotNull(
            resolution.ConfiancaDeadband);

        Assert.Equal(
            resolution.Media!.Value,
            captured.MediaHistorica,
            6);

        Assert.Equal(
            resolution.DesvioPadrao!.Value,
            captured.DesvioPadraoHistorico,
            6);

        Assert.Equal(
            resolution.DeadbandRecomendado!.Value,
            captured.VariacaoMinimaRelevante,
            6);

        Assert.Equal(
            resolution.ConfiancaDeadband!.Value,
            captured.ConfiancaVariacaoMinima,
            6);

        /*
         * Proteção contra falso positivo:
         * o perfil global é 100/2/16/94 e não pode continuar sendo
         * o baseline do detector neste cenário.
         */
        Assert.NotEqual(
            100,
            captured.MediaHistorica);

        /*
         * O histórico do detector também passa a ser o episódio
         * comportamental retornado pelo provider.
         */
        Assert.Equal(
            resolution.History.Count,
            captured.Historico.Count);

        Assert.Equal(
            resolution.History[0].DataHora,
            captured.Historico[0].DataHora);

        Assert.Equal(
            resolution.History[^1].DataHora,
            captured.Historico[^1].DataHora);

        /*
         * O runtime B3.25 continua estritamente read-only.
         */
        Assert.Equal(
            behaviorCountBefore,
            await context.TagsComportamentosIa
                .CountAsync());

        Assert.Equal(
            snapshotCountBefore,
            await context
                .TagsComportamentosSnapshotsIa
                .CountAsync());
    }

    private static async Task SeedPredictionRequirementsAsync(
        AppDbContext context)
    {
        context.TagsIaConfig.Add(
            new TagIaConfig
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                IaAtiva =
                    true
            });

        context.PerfisIa.Add(
            new TagPerfilIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Media =
                    100,

                DesvioPadrao =
                    2,

                VariacaoMinimaRelevante =
                    16,

                DeadbandSugerido =
                    16,

                ConfiancaDeadband =
                    94,

                VariacaoMinimaCalculadaAutomaticamente =
                    true,

                DataTreinamento =
                    new DateTime(
                        2026, 8, 21, 10, 0, 0,
                        DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedKnownNormalBehaviorAsync(
        AppDbContext context,
        IReadOnlyList<LeituraBruta> history)
    {
        var behavior =
            new TagComportamentoIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Codigo =
                    "BEH-001",

                Status =
                    TagComportamentoIa.StatusKnown,

                Origem =
                    TagComportamentoIa.OrigemAutomatic,

                Classificacao =
                    TagComportamentoIa.ClassificacaoNormal,

                PrimeiraObservacao =
                    history[0].DataHora,

                UltimaObservacao =
                    history[^1].DataHora,

                QuantidadeOcorrencias =
                    1,

                TotalAmostras =
                    history.Count,

                Confianca =
                    90,

                DataCriacao =
                    DateTime.UtcNow
            };

        context.TagsComportamentosIa.Add(
            behavior);

        await context.SaveChangesAsync();

        /*
         * Produzimos o snapshot usando os mesmos componentes
         * produtivos do B3.25, evitando estatísticas artificiais.
         */
        var statistics =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var characterizer =
            new BehaviorCandidateEpisodeCharacterizer(
                statistics);

        var episode =
            new BehaviorCandidateEpisode
            {
                SourceSegmentIndex =
                    0,

                EpisodeIndex =
                    0,

                StartReadingIndex =
                    0,

                EndReadingIndex =
                    history.Count - 1,

                Inicio =
                    history[0].DataHora,

                Fim =
                    history[^1].DataHora,

                Historico =
                    history
            };

        var characteristics =
            characterizer.Characterize(
                episode);

        Assert.True(
            characteristics.IsCharacterizable);

        var snapshot =
            new BehaviorMemoryPersistencePlanBuilder()
                .BuildSnapshotPreview(
                    new[]
                    {
                        characteristics
                    },
                    version:
                        1,
                    createdAtUtc:
                        DateTime.UtcNow);

        snapshot.TagComportamentoIaId =
            behavior.Id;

        context.TagsComportamentosSnapshotsIa.Add(
            snapshot);

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
    }

    private static BehaviorRuntimeBaselineProvider
        CreateBehaviorProvider(
            AppDbContext context)
    {
        /*
         * Aqui instanciamos o provider diretamente, usando os mesmos
         * componentes produtivos já usados nos testes específicos do B3.25.
         *
         * Isso evita um falso problema de lifetime do container DI:
         * BehaviorRuntimeBaselineProvider é Scoped e não deve ser resolvido
         * diretamente do root ServiceProvider.
         */
        var statistics =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var characterizer =
            new BehaviorCandidateEpisodeCharacterizer(
                statistics);

        var planBuilder =
            new BehaviorMemoryPersistencePlanBuilder();

        var reconciliation =
            new BehaviorMemoryReconciliationService(
                context,
                planBuilder);

        return new BehaviorRuntimeBaselineProvider(
            context,
            new BehaviorHistorySegmentationService(),
            new BehaviorChangePointDetector(),
            new BehaviorCandidateEpisodeBuilder(),
            characterizer,
            reconciliation);
    }

    private static PredictionService
        CreatePredictionService(
            AppDbContext context,
            IReadOnlyList<LeituraBruta> history,
            CaptureDetector captureDetector,
            BehaviorRuntimeBaselineProvider behaviorProvider)
    {
        var historicoService =
            new FakeHistoricoService(
                history.ToList());

        var ensemble =
            new EnsembleDetectionService(
                new IAnomalyDetector[]
                {
                    captureDetector
                },
                Options.Create(
                    new AnomalyDetectionOptions()),
                new TagBehaviorProfileResolver(),
                new DetectorApplicabilityPolicy());

        var processoAnaliseService =
            new ProcessoAnaliseService(
                context);

                var predictionContextLoader =
    new PredictionContextLoader(
        context,
        processoAnaliseService);

var predictionDetectionPreparationService =
    new PredictionDetectionPreparationService(
        historicoService,
        operationalRegimeBaselineProvider: null,
        behaviorRuntimeBaselineProvider:
            behaviorProvider);

var predictionDecisionOrchestrationService =
    new PredictionDecisionOrchestrationService(
        ensemble,
        null!,
        null!,
        null!);

return new PredictionService(
    predictionContextLoader,
    null!,
    null!,
    predictionDecisionOrchestrationService,
    predictionDetectionPreparationService,
    null!);
    }

    private static LeituraBruta[]
        CreateDetectionHistory(
            DateTime start,
            int count,
            double center)
    {
        return Enumerable
            .Range(
                0,
                count)
            .Select(index =>
                new LeituraBruta
                {
                    DataHora =
                        start.AddMinutes(
                            index),

                    Valor =
                        Convert.ToSingle(
                            center +
                            (index * 0.27) +
                            ((index % 3) * 0.013))
                })
            .ToArray();
    }

    private static LeituraBruta[]
        CreateStableBehaviorHistory(
            DateTime start,
            int count,
            double center)
    {
        var offsets =
            new[]
            {
                0.00,
                0.12,
                -0.08,
                0.18,
                -0.14,
                0.05,
                0.16,
                -0.11,
                0.09,
                -0.04
            };

        return Enumerable
            .Range(
                0,
                count)
            .Select(index =>
                new LeituraBruta
                {
                    DataHora =
                        start.AddMinutes(
                            index),

                    Valor =
                        Convert.ToSingle(
                            center +
                            offsets[
                                index %
                                offsets.Length])
                })
            .ToArray();
    }

    private sealed class CaptureDetector :
        IAnomalyDetector
    {
        public string Nome =>
            "CaptureDetector";

        public double Peso =>
            1.0;

        public DetectorInput? CapturedInput
        {
            get;
            private set;
        }

        public DetectorResult Detectar(
            DetectorInput input)
        {
            CapturedInput =
                input;

            throw new InvalidOperationException(
                "Fim intencional do detector capturador.");
        }
    }

    private sealed class FakeHistoricoService :
        IHistoricoService
    {
        private readonly List<LeituraBruta>
            _history;

        public FakeHistoricoService(
            List<LeituraBruta> history)
        {
            _history =
                history;
        }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return _history;
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return _history
                .Where(x =>
                    x.DataHora >= inicio &&
                    x.DataHora <= fim)
                .ToList();
        }
    }

    private sealed class TestFixture :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext context)
        {
            _connection =
                connection;

            Context =
                context;
        }

        public AppDbContext Context { get; }

        public static async Task<TestFixture>
            CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(
                        connection)
                    .Options;

            var context =
                new AppDbContext(
                    options);

            await context.Database
                .EnsureCreatedAsync();

            return new TestFixture(
                connection,
                context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();

            await _connection.DisposeAsync();
        }
    }
}
