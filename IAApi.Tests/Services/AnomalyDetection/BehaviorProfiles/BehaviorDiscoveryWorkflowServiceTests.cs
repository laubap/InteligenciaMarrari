using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles;

public sealed class BehaviorDiscoveryWorkflowServiceTests
{
    [Fact]
    public async Task ExecuteAsync_HistoricoVazio_DeveRetornarFluxoVazioSemPersistir()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var historian =
            new FakeHistoricoService(
                Array.Empty<LeituraBruta>());

        var workflow =
            CreateWorkflow(
                fixture.Context,
                historian);

        var result =
            await workflow.ExecuteAsync(
                "CLIENTE-A",
                "TAG1",
                Utc(2026, 9, 1, 0, 0),
                Utc(2026, 9, 2, 0, 0));

        Assert.Equal(
            0,
            result.Collection.QuantidadeLeituras);

        Assert.Empty(
            result.Segments);

        Assert.Empty(
            result.Candidates);

        Assert.Empty(
            result.Consolidation.Items);

        Assert.Empty(
            await fixture.Context.TagsComportamentosIa
                .ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_TresOcorrenciasSemelhantesSeparadasPorGap_DeveCriarBeh001()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var history =
            CreateEpisode(
                Utc(2026, 9, 1, 0, 0),
                40.0)
            .Concat(
                CreateEpisode(
                    Utc(2026, 9, 1, 2, 0),
                    40.0))
            .Concat(
                CreateEpisode(
                    Utc(2026, 9, 1, 4, 0),
                    40.0))
            .ToArray();

        var historian =
            new FakeHistoricoService(
                history);

        var workflow =
            CreateWorkflow(
                fixture.Context,
                historian);

        var result =
            await workflow.ExecuteAsync(
                "CLIENTE-A",
                "TAG1",
                Utc(2026, 9, 1, 0, 0),
                Utc(2026, 9, 1, 5, 0));

        Assert.Equal(
            3,
            result.Segmentation.QuantidadeSegmentos);

        Assert.Equal(
            3,
            result.TotalEpisodes);

        Assert.Equal(
            3,
            result.TotalCharacterizableEpisodes);

        var candidate =
            Assert.Single(
                result.Candidates);

        Assert.True(
            candidate.IsRecurrent);

        Assert.Equal(
            3,
            candidate.OccurrenceCount);

        var consolidation =
            Assert.Single(
                result.Consolidation.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedNewBehavior,
            consolidation.Outcome);

        Assert.Equal(
            "BEH-001",
            consolidation.BehaviorCode);

        var behavior =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            "BEH-001",
            behavior.Codigo);

        Assert.Equal(
            3,
            behavior.QuantidadeOcorrencias);
    }

    [Fact]
    public async Task ExecuteAsync_MudancaPersistenteNoMesmoSegmento_DeveSepararEpisodios()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var start =
            Utc(
                2026, 9, 1, 0, 0);

        var first =
            Enumerable.Range(
                    0,
                    40)
                .Select(index =>
                    new LeituraBruta
                    {
                        DataHora =
                            start.AddMinutes(
                                index),

                        Valor =
                            (float)(
                                40.0 +
                                (
                                    index % 2 == 0
                                        ? -0.20
                                        : 0.20
                                ))
                    });

        var second =
            Enumerable.Range(
                    40,
                    40)
                .Select(index =>
                    new LeituraBruta
                    {
                        DataHora =
                            start.AddMinutes(
                                index),

                        Valor =
                            (float)(
                                75.0 +
                                (
                                    index % 2 == 0
                                        ? -0.20
                                        : 0.20
                                ))
                    });

        var historian =
            new FakeHistoricoService(
                first.Concat(
                    second));

        var workflow =
            CreateWorkflow(
                fixture.Context,
                historian);

        var result =
            await workflow.ExecuteAsync(
                "CLIENTE-A",
                "TAG1",
                start,
                start.AddMinutes(
                    80));

        var segment =
            Assert.Single(
                result.Segments);

        Assert.NotEmpty(
            segment.ChangePoints);

        Assert.Equal(
            2,
            segment.Episodes.Count);

        Assert.Equal(
            2,
            result.Candidates.Count);

        Assert.All(
            result.Consolidation.Items,
            item =>
            {
                Assert.Equal(
                    BehaviorMemoryConsolidationOutcome.PersistedCandidate,
                    item.Outcome);

                Assert.True(
                    item.BehaviorCode is not null &&
                    item.BehaviorCode.StartsWith(
                        "CAND-",
                        StringComparison.Ordinal));
            });

        var persistedCandidates =
            await fixture.Context.TagsComportamentosIa
                .OrderBy(x =>
                    x.Id)
                .ToArrayAsync();

        Assert.Equal(
            2,
            persistedCandidates.Length);

        Assert.All(
            persistedCandidates,
            behavior =>
                Assert.Equal(
                    TagComportamentoIa.StatusCandidate,
                    behavior.Status));
    }

    [Fact]
    public async Task ExecuteAsync_UmaNovaOcorrencia_DeveReforcarBehExistente()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var oldStart =
            Utc(
                2026, 8, 1, 0, 0);

        await SeedKnownBehaviorAsync(
            fixture.Context,
            oldStart);

        var newStart =
            Utc(
                2026, 9, 1, 0, 0);

        var allHistory =
            CreateEpisode(
                oldStart,
                40.0)
            .Concat(
                CreateEpisode(
                    newStart,
                    40.0))
            .ToArray();

        var historian =
            new FakeHistoricoService(
                allHistory);

        var workflow =
            CreateWorkflow(
                fixture.Context,
                historian);

        var result =
            await workflow.ExecuteAsync(
                "CLIENTE-A",
                "TAG1",
                newStart,
                newStart.AddMinutes(
                    20));

        var candidate =
            Assert.Single(
                result.Candidates);

        Assert.False(
            candidate.IsRecurrent);

        var consolidation =
            Assert.Single(
                result.Consolidation.Items);

        Assert.Equal(
            BehaviorMemoryLifecycle.Candidate,
            consolidation.Lifecycle);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedExistingBehavior,
            consolidation.Outcome);

        Assert.Equal(
            "BEH-001",
            consolidation.BehaviorCode);

        var behavior =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            2,
            behavior.QuantidadeOcorrencias);

        var latestSnapshot =
            await fixture.Context.TagsComportamentosSnapshotsIa
                .OrderByDescending(x =>
                    x.Versao)
                .FirstAsync();

        Assert.Equal(
            2,
            latestSnapshot.Versao);

        Assert.Equal(
            40,
            latestSnapshot.TotalRegistrosUsados);
    }

    private static BehaviorDiscoveryWorkflowService CreateWorkflow(
        AppDbContext context,
        IHistoricoService historian)
    {
        var statisticsCalculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var planBuilder =
            new BehaviorMemoryPersistencePlanBuilder();

        var snapshotRebuild =
            new BehaviorMemorySnapshotRebuildService(
                context,
                historian,
                statisticsCalculator,
                planBuilder);

        var comparer =
            new BehaviorCandidateEpisodeSimilarityComparer();

        var confidenceCalculator =
            new BehaviorIdentityConfidenceCalculator();

        var persistenceService =
            new BehaviorMemoryPersistenceService(
                context);

        var candidateMemoryService =
            new BehaviorCandidateMemoryService(
                context,
                comparer,
                confidenceCalculator,
                planBuilder,
                persistenceService,
                snapshotRebuild);

        var consolidation =
            new BehaviorMemoryConsolidationService(
                context,
                new BehaviorMemoryLifecyclePolicy(),
                confidenceCalculator,
                new BehaviorMemoryReconciliationService(
                    context,
                    planBuilder),
                candidateMemoryService,
                new BehaviorIdentityProposalService(),
                planBuilder,
                persistenceService,
                snapshotRebuild);

        return new BehaviorDiscoveryWorkflowService(
            new BehaviorHistoryCollectionService(
                historian),
            new BehaviorHistorySegmentationService(),
            new BehaviorChangePointDetector(),
            new BehaviorCandidateEpisodeBuilder(),
            new BehaviorCandidateEpisodeCharacterizer(
                statisticsCalculator),
            new BehaviorCandidateEpisodeSimilarityGrouper(
                new BehaviorCandidateEpisodeSimilarityComparer()),
            new BehaviorDiscoveryCandidateFactory(),
            consolidation);
    }

    private static IReadOnlyList<LeituraBruta> CreateEpisode(
        DateTime start,
        double center)
    {
        return Enumerable.Range(
                0,
                20)
            .Select(index =>
                new LeituraBruta
                {
                    DataHora =
                        start.AddMinutes(
                            index),

                    Valor =
                        (float)(
                            center +
                            (
                                index % 2 == 0
                                    ? -0.25
                                    : 0.25
                            ))
                })
            .ToArray();
    }

    private static async Task SeedKnownBehaviorAsync(
        AppDbContext context,
        DateTime occurrenceStart)
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
                    TagComportamentoIa.ClassificacaoUndefined,

                PrimeiraObservacao =
                    occurrenceStart,

                UltimaObservacao =
                    occurrenceStart.AddMinutes(
                        19),

                QuantidadeOcorrencias =
                    1,

                TotalAmostras =
                    20,

                Confianca =
                    90
            };

        context.TagsComportamentosIa.Add(
            behavior);

        await context.SaveChangesAsync();

        context.TagsComportamentosOcorrenciasIa.Add(
            new TagComportamentoOcorrenciaIa
            {
                TagComportamentoIaId =
                    behavior.Id,

                DataHoraInicio =
                    occurrenceStart,

                DataHoraFim =
                    occurrenceStart.AddMinutes(
                        19),

                QuantidadeAmostras =
                    20,

                Confianca =
                    90,

                UtilizadaNoAprendizado =
                    true
            });

        context.TagsComportamentosSnapshotsIa.Add(
            new TagComportamentoSnapshotIa
            {
                TagComportamentoIaId =
                    behavior.Id,

                Versao =
                    1,

                Media =
                    40.0,

                DesvioPadrao =
                    0.25,

                Minimo =
                    39.75,

                Maximo =
                    40.25,

                Amplitude =
                    0.50,

                PercentualZeros =
                    0,

                VariacaoMedia =
                    0.50,

                QuantidadePicos =
                    0,

                TotalRegistrosHistorico =
                    20,

                TotalRegistrosUsados =
                    20,

                TotalOutliersRemovidos =
                    0,

                DeadbandRecomendado =
                    0.50,

                ConfiancaDeadband =
                    100,

                PeriodoInicio =
                    occurrenceStart,

                PeriodoFim =
                    occurrenceStart.AddMinutes(
                        19)
            });

        await context.SaveChangesAsync();
    }

    private static DateTime Utc(
        int year,
        int month,
        int day,
        int hour,
        int minute)
    {
        return new DateTime(
            year,
            month,
            day,
            hour,
            minute,
            0,
            DateTimeKind.Utc);
    }

    private sealed class FakeHistoricoService :
        IHistoricoService
    {
        private readonly IReadOnlyList<LeituraBruta> _history;

        public FakeHistoricoService(
            IEnumerable<LeituraBruta> history)
        {
            _history =
                history
                    .OrderBy(x =>
                        x.DataHora)
                    .ToArray();
        }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return _history
                .Select(Clone)
                .ToList();
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return _history
                .Where(x =>
                    x.DataHora >=
                        inicio &&
                    x.DataHora <=
                        fim)
                .Select(Clone)
                .ToList();
        }

        private static LeituraBruta Clone(
            LeituraBruta source)
        {
            return new LeituraBruta
            {
                DataHora =
                    source.DataHora,

                Valor =
                    source.Valor
            };
        }
    }

    private sealed class DatabaseFixture :
        IAsyncDisposable
    {
        private DatabaseFixture(
            SqliteConnection connection,
            AppDbContext context)
        {
            Connection =
                connection;

            Context =
                context;
        }

        public SqliteConnection Connection { get; }

        public AppDbContext Context { get; }

        public static async Task<DatabaseFixture> CreateAsync()
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

            return new DatabaseFixture(
                connection,
                context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
