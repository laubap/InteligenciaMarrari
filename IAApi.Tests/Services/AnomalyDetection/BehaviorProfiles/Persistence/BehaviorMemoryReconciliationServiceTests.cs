using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public sealed class BehaviorMemoryReconciliationServiceTests
{
    [Fact]
    public async Task ReconcileAsync_SemMemoria_DeveIndicarNovoComportamento()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.NewBehavior,
            result.Outcome);

        Assert.Null(
            result.MatchedBehaviorCode);

        Assert.Empty(
            result.ReservedIdentities);
    }

    [Fact]
    public async Task ReconcileAsync_UmKnownCompativel_DeveReutilizarIdentidade()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await AddBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            mean: 40.1,
            version: 1);

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.ExistingBehavior,
            result.Outcome);

        Assert.Equal(
            "BEH-001",
            result.MatchedBehaviorCode);

        Assert.Contains(
            "BEH-001",
            result.ReservedIdentities);
    }

    [Fact]
    public async Task ReconcileAsync_CentroDistante_DeveIndicarNovoComportamento()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await AddBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            mean: 75.0,
            version: 1);

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.NewBehavior,
            result.Outcome);

        var comparison =
            Assert.Single(
                result.Comparisons);

        Assert.False(
            comparison.IsCompatible);

        Assert.False(
            comparison.CenterStable);
    }

    [Fact]
    public async Task ReconcileAsync_DoisKnownCompativeis_DeveRetornarAmbiguo()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await AddBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            mean: 40.0,
            version: 1);

        await AddBehaviorAsync(
            fixture.Context,
            "BEH-002",
            TagComportamentoIa.StatusKnown,
            mean: 40.1,
            version: 1);

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.05));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.Ambiguous,
            result.Outcome);

        Assert.Null(
            result.MatchedBehaviorId);

        Assert.Equal(
            2,
            result.Comparisons.Count(x =>
                x.IsCompatible));
    }

    [Fact]
    public async Task ReconcileAsync_Deprecated_NaoDeveSerMatchMasCodigoPermaneceReservado()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await AddBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusDeprecated,
            mean: 40.0,
            version: 1);

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.NewBehavior,
            result.Outcome);

        Assert.Empty(
            result.Comparisons);

        Assert.Contains(
            "BEH-001",
            result.ReservedIdentities);
    }

    [Fact]
    public async Task ReconcileAsync_DeveUsarSnapshotMaisRecente()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await AddBehaviorAsync(
                fixture.Context,
                "BEH-001",
                TagComportamentoIa.StatusKnown,
                mean: 75.0,
                version: 1);

        fixture.Context.TagsComportamentosSnapshotsIa.Add(
            CreateSnapshot(
                behavior.Id,
                version: 2,
                mean: 40.1));

        await fixture.Context.SaveChangesAsync();

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.ExistingBehavior,
            result.Outcome);

        var comparison =
            Assert.Single(
                result.Comparisons);

        Assert.Equal(
            2,
            comparison.SnapshotVersion);
    }

    [Fact]
    public async Task ReconcileAsync_SnapshotLegadoSemAssinaturaRica_DeveUsarFallback()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await AddBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            mean: 40.05,
            version: 1);

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.ExistingBehavior,
            result.Outcome);

        var comparison =
            Assert.Single(
                result.Comparisons);

        Assert.False(
            comparison.UsedRichIdentity);

        Assert.Null(
            comparison.DistributionStable);

        Assert.Null(
            comparison.DynamicsStable);

        Assert.Contains(
            "fallback legado",
            comparison.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReconcileAsync_AssinaturaRicaCompativel_DeveReutilizarIdentidade()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await AddBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            mean: 40.05,
            version: 1,
            richIdentity: true);

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.ExistingBehavior,
            result.Outcome);

        Assert.Equal(
            "BEH-001",
            result.MatchedBehaviorCode);

        var comparison =
            Assert.Single(
                result.Comparisons);

        Assert.True(
            comparison.UsedRichIdentity);

        Assert.True(
            comparison.CenterStable);

        Assert.True(
            comparison.ScaleStable);

        Assert.True(
            comparison.DistributionStable);

        Assert.True(
            comparison.DynamicsStable);

        Assert.NotNull(
            comparison.MedianDelta);

        Assert.NotNull(
            comparison.RobustScaleRatio);

        Assert.NotNull(
            comparison.TypicalAbsoluteStepRatio);
    }

    [Fact]
    public async Task ReconcileAsync_LegadoCompativelMasDistribuicaoRicaIncompativel_DeveIndicarNovo()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var behavior =
            await AddBehaviorAsync(
                fixture.Context,
                "BEH-001",
                TagComportamentoIa.StatusKnown,
                mean: 40.05,
                version: 1,
                richIdentity: true);

        var snapshot =
            await fixture.Context
                .TagsComportamentosSnapshotsIa
                .SingleAsync(x =>
                    x.TagComportamentoIaId ==
                        behavior.Id);

        /*
         * Média, desvio, variação e deadband continuam compatíveis.
         * Apenas a forma da distribuição rica é incompatível.
         */
        snapshot.PrimeiroQuartil =
            35.0;

        snapshot.TerceiroQuartil =
            45.0;

        snapshot.IntervaloInterquartil =
            10.0;

        await fixture.Context
            .SaveChangesAsync();

        var service =
            new BehaviorMemoryReconciliationService(
                fixture.Context);

        var result =
            await service.ReconcileAsync(
                "CLIENTE-A",
                "TAG1",
                CreateEvaluation(
                    40.0));

        Assert.Equal(
            BehaviorMemoryReconciliationOutcome.NewBehavior,
            result.Outcome);

        var comparison =
            Assert.Single(
                result.Comparisons);

        Assert.True(
            comparison.UsedRichIdentity);

        Assert.True(
            comparison.CenterStable);

        Assert.True(
            comparison.ScaleStable);

        Assert.True(
            comparison.VariationStable);

        Assert.True(
            comparison.DeadbandStable);

        Assert.False(
            comparison.DistributionStable);

        Assert.True(
            comparison.DynamicsStable);

        Assert.Contains(
            "distribuição",
            comparison.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    private static BehaviorMemoryLifecycleEvaluation CreateEvaluation(
        double center)
    {
        var occurrences =
            Enumerable.Range(
                    0,
                    3)
                .Select(index =>
                    CreateOccurrence(
                        index,
                        center +
                        (index * 0.05)))
                .ToArray();

        var candidate =
            new BehaviorDiscoveryCandidate
            {
                GroupIndex =
                    0,

                OccurrenceCount =
                    occurrences.Length,

                IsRecurrent =
                    true,

                FirstOccurrenceStart =
                    occurrences.Min(x =>
                        x.Inicio),

                LastOccurrenceEnd =
                    occurrences.Max(x =>
                        x.Fim),

                RepresentativeProfile =
                    new BehaviorCandidateRepresentativeProfile
                    {
                        Media =
                            center,

                        Mediana =
                            center,

                        DesvioPadrao =
                            0.5,

                        DeadbandRecomendado =
                            0.5
                    },

                Occurrences =
                    occurrences
            };

        return new BehaviorMemoryLifecycleEvaluation
        {
            GroupIndex =
                0,

            Lifecycle =
                BehaviorMemoryLifecycle.Known,

            EligibleForStableIdentity =
                true,

            OccurrenceCount =
                occurrences.Length,

            EvidenceSpanMinutes =
                300,

            RecurrenceSatisfied =
                true,

            OccurrenceThresholdSatisfied =
                true,

            EvidenceSpanSatisfied =
                true,

            Candidate =
                candidate
        };
    }

    private static BehaviorCandidateEpisodeCharacteristics CreateOccurrence(
        int episode,
        double mean)
    {
        var start =
            new DateTime(
                2026,
                9,
                1,
                episode * 2,
                0,
                0,
                DateTimeKind.Utc);

        var history =
            Enumerable.Range(
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
                                mean +
                                (
                                    index % 2 == 0
                                        ? -0.25
                                        : 0.25
                                ))
                    })
                .ToArray();

        return new BehaviorCandidateEpisodeCharacteristics
        {
            SourceSegmentIndex =
                0,

            EpisodeIndex =
                episode,

            Inicio =
                start,

            Fim =
                start.AddMinutes(
                    19),

            IsCharacterizable =
                true,

            OriginalSampleCount =
                20,

            UsedSampleCount =
                20,

            RemovedOutliers =
                0,

            Media =
                mean,

            Mediana =
                mean,

            Minimo =
                mean - 1,

            Maximo =
                mean + 1,

            DesvioPadrao =
                0.5,

            Amplitude =
                2,

            PrimeiroQuartil =
                mean - 0.3,

            TerceiroQuartil =
                mean + 0.3,

            IntervaloInterquartil =
                0.6,

            VariacaoMedia =
                0.25,

            PercentualZeros =
                0,

            DeadbandRecomendado =
                0.5,

            ConfiancaDeadband =
                90,

            HistoricoLimpo =
                history,

            ProfileFeatures =
                new BehaviorProfileFeatures
                {
                    RobustScale =
                        0.7,

                    TypicalAbsoluteStep =
                        0.5,

                    ChangeRatio =
                        1,

                    ReversalRatio =
                        1
                }
        };
    }

    private static async Task<TagComportamentoIa> AddBehaviorAsync(
        AppDbContext context,
        string code,
        string status,
        double mean,
        int version,
        bool richIdentity = false)
    {
        var behavior =
            new TagComportamentoIa
            {
                ClienteId =
                    "CLIENTE-A",

                TagName =
                    "TAG1",

                Codigo =
                    code,

                Status =
                    status,

                Origem =
                    TagComportamentoIa.OrigemAutomatic,

                Classificacao =
                    TagComportamentoIa.ClassificacaoUndefined,

                PrimeiraObservacao =
                    new DateTime(
                        2026,
                        8,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),

                UltimaObservacao =
                    new DateTime(
                        2026,
                        8,
                        2,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),

                QuantidadeOcorrencias =
                    3,

                TotalAmostras =
                    60,

                Confianca =
                    85
            };

        context.TagsComportamentosIa.Add(
            behavior);

        await context.SaveChangesAsync();

        context.TagsComportamentosSnapshotsIa.Add(
            CreateSnapshot(
                behavior.Id,
                version,
                mean,
                richIdentity));

        await context.SaveChangesAsync();

        return behavior;
    }

    private static TagComportamentoSnapshotIa CreateSnapshot(
        int behaviorId,
        int version,
        double mean,
        bool richIdentity = false)
    {
        return new TagComportamentoSnapshotIa
        {
            TagComportamentoIaId =
                behaviorId,

            Versao =
                version,

            Media =
                mean,

            DesvioPadrao =
                0.55,

            Mediana =
                richIdentity
                    ? mean
                    : null,

            PrimeiroQuartil =
                richIdentity
                    ? mean - 0.3
                    : null,

            TerceiroQuartil =
                richIdentity
                    ? mean + 0.3
                    : null,

            IntervaloInterquartil =
                richIdentity
                    ? 0.6
                    : null,

            RobustScale =
                richIdentity
                    ? 0.7
                    : null,

            TypicalAbsoluteStep =
                richIdentity
                    ? 0.5
                    : null,

            Minimo =
                mean - 1,

            Maximo =
                mean + 1,

            Amplitude =
                2,

            PercentualZeros =
                0,

            VariacaoMedia =
                0.25,

            QuantidadePicos =
                0,

            TotalRegistrosHistorico =
                60,

            TotalRegistrosUsados =
                60,

            TotalOutliersRemovidos =
                0,

            DeadbandRecomendado =
                0.5,

            ConfiancaDeadband =
                90,

            PeriodoInicio =
                new DateTime(
                    2026,
                    8,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),

            PeriodoFim =
                new DateTime(
                    2026,
                    8,
                    2,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc)
        };
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
