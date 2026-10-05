using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public sealed class BehaviorCandidatePromotionConcurrencyTests
{
    [Fact]
    public async Task PromoteToKnownAsync_IdentidadeJaReservada_DeveBloquearSemAlterarCandidate()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var candidate =
            await SeedBehaviorAsync(
                fixture.Context,
                "CAND-TESTE-A",
                TagComportamentoIa.StatusCandidate);

        await SeedBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusKnown);

        var service =
            CreateService(
                fixture.Context);

        var exception =
            await Assert.ThrowsAsync<BehaviorStableIdentityConflictException>(
                () =>
                    service.PromoteToKnownAsync(
                        candidate.Id,
                        "CLIENTE-A",
                        "TAG1",
                        "BEH-001",
                        CreateIdentityConfidence()));

        Assert.Equal(
            "BEH-001",
            exception.BehaviorCode);

        fixture.Context.ChangeTracker.Clear();

        var persistedCandidate =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync(x =>
                    x.Id == candidate.Id);

        Assert.Equal(
            "CAND-TESTE-A",
            persistedCandidate.Codigo);

        Assert.Equal(
            TagComportamentoIa.StatusCandidate,
            persistedCandidate.Status);

        Assert.Equal(
            TagComportamentoIa.OrigemAutomatic,
            persistedCandidate.Origem);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            persistedCandidate.Classificacao);
    }

    [Fact]
    public async Task PromoteToKnownAsync_CandidateJaPromovidoPorOutraExecucao_DeveAceitarIdentidadePersistida()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var candidate =
            await SeedBehaviorAsync(
                fixture.Context,
                "CAND-TESTE-B",
                TagComportamentoIa.StatusCandidate);

        /*
         * Simula outra requisição vencendo a corrida entre a proposta local
         * e a tentativa atual de promoção.
         */
        candidate.Codigo =
            "BEH-002";

        candidate.Status =
            TagComportamentoIa.StatusKnown;

        await fixture.Context.SaveChangesAsync();

        fixture.Context.ChangeTracker.Clear();

        var service =
            CreateService(
                fixture.Context);

        var result =
            await service.PromoteToKnownAsync(
                candidate.Id,
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                CreateIdentityConfidence());

        Assert.False(
            result.Promoted);

        Assert.Equal(
            candidate.Id,
            result.BehaviorId);

        Assert.Equal(
            "BEH-002",
            result.BehaviorCode);

        Assert.Contains(
            "outra execução",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);

        var persisted =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync(x =>
                    x.Id == candidate.Id);

        Assert.Equal(
            "BEH-002",
            persisted.Codigo);

        Assert.Equal(
            TagComportamentoIa.StatusKnown,
            persisted.Status);

        Assert.Single(
            await fixture.Context.TagsComportamentosIa
                .ToListAsync());
    }

    [Fact]
    public async Task PromoteToNextAvailableKnownAsync_ReservaObsoleta_DeveTentarProximoBeh()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var candidate =
            await SeedBehaviorAsync(
                fixture.Context,
                "CAND-TESTE-C",
                TagComportamentoIa.StatusCandidate);

        /*
         * Simula a condição de corrida:
         * a execução atual ainda acredita que nenhuma identidade está
         * reservada, mas outra execução já confirmou BEH-001 no banco.
         */
        await SeedBehaviorAsync(
            fixture.Context,
            "BEH-001",
            TagComportamentoIa.StatusKnown);

        fixture.Context.ChangeTracker.Clear();

        var service =
            CreateService(
                fixture.Context);

        var lifecycle =
            CreateKnownLifecycleEvaluation();

        var result =
            await service.PromoteToNextAvailableKnownAsync(
                candidate.Id,
                "CLIENTE-A",
                "TAG1",
                lifecycle,
                CreateIdentityConfidence(),
                initiallyReservedIdentities:
                    Array.Empty<string>());

        Assert.True(
            result.Promoted);

        Assert.Equal(
            candidate.Id,
            result.BehaviorId);

        Assert.Equal(
            "BEH-002",
            result.BehaviorCode);

        fixture.Context.ChangeTracker.Clear();

        var promotedCandidate =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync(x =>
                    x.Id == candidate.Id);

        Assert.Equal(
            "BEH-002",
            promotedCandidate.Codigo);

        Assert.Equal(
            TagComportamentoIa.StatusKnown,
            promotedCandidate.Status);

        var codes =
            await fixture.Context.TagsComportamentosIa
                .OrderBy(x =>
                    x.Codigo)
                .Select(x =>
                    x.Codigo)
                .ToArrayAsync();

        Assert.Equal(
            new[]
            {
                "BEH-001",
                "BEH-002"
            },
            codes);
    }

    private static BehaviorMemoryLifecycleEvaluation
        CreateKnownLifecycleEvaluation()
    {
        var firstOccurrence =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        return new BehaviorMemoryLifecycleEvaluation
        {
            GroupIndex =
                0,

            Lifecycle =
                BehaviorMemoryLifecycle.Known,

            EligibleForStableIdentity =
                true,

            OccurrenceCount =
                3,

            EvidenceSpanMinutes =
                120,

            RecurrenceSatisfied =
                true,

            OccurrenceThresholdSatisfied =
                true,

            EvidenceSpanSatisfied =
                true,

            Candidate =
                new BehaviorDiscoveryCandidate
                {
                    GroupIndex =
                        0,

                    OccurrenceCount =
                        3,

                    IsRecurrent =
                        true,

                    FirstOccurrenceStart =
                        firstOccurrence,

                    LastOccurrenceEnd =
                        firstOccurrence.AddHours(
                            2)
                },

            Reason =
                "Lifecycle Known válido para teste de reserva concorrente."
        };
    }

    private static BehaviorCandidateMemoryService CreateService(
        AppDbContext context)
    {
        var planBuilder =
            new BehaviorMemoryPersistencePlanBuilder();

        var statisticsCalculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var snapshotRebuildService =
            new BehaviorMemorySnapshotRebuildService(
                context,
                new EmptyHistoricoService(),
                statisticsCalculator,
                planBuilder);

        return new BehaviorCandidateMemoryService(
            context,
            new BehaviorCandidateEpisodeSimilarityComparer(),
            new BehaviorIdentityConfidenceCalculator(),
            planBuilder,
            new BehaviorMemoryPersistenceService(
                context),
            snapshotRebuildService);
    }

    private static BehaviorIdentityConfidenceResult CreateIdentityConfidence()
    {
        return new BehaviorIdentityConfidenceResult
        {
            CanEvaluate =
                true,

            Confidence =
                88,

            WeakestPairConfidence =
                88,

            Reason =
                "Confiança válida para teste de promoção."
        };
    }

    private static async Task<TagComportamentoIa> SeedBehaviorAsync(
        AppDbContext context,
        string code,
        string status)
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
                        9,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),

                UltimaObservacao =
                    new DateTime(
                        2026,
                        9,
                        1,
                        2,
                        0,
                        0,
                        DateTimeKind.Utc),

                QuantidadeOcorrencias =
                    3,

                TotalAmostras =
                    60,

                Confianca =
                    80,

                DataCriacao =
                    new DateTime(
                        2026,
                        10,
                        2,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc)
            };

        context.TagsComportamentosIa.Add(
            behavior);

        await context.SaveChangesAsync();

        return behavior;
    }

    private sealed class EmptyHistoricoService :
        IHistoricoService
    {
        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return new List<LeituraBruta>();
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return new List<LeituraBruta>();
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
