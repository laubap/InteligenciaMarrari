using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public sealed class BehaviorMemoryConsolidationServiceTests
{
    [Fact]
    public async Task ConsolidateAsync_CandidateImaturo_DevePersistirComoMemoriaCandidate()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            CreateService(
                fixture.Context);

        var candidate =
            CreateCandidate(
                groupIndex: 0,
                center: 40,
                occurrenceCount: 2);

        var result =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    candidate
                });

        var item =
            Assert.Single(
                result.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedCandidate,
            item.Outcome);

        Assert.NotNull(
            item.BehaviorId);

        Assert.True(
            item.BehaviorCode is not null &&
            item.BehaviorCode.StartsWith(
                "CAND-",
                StringComparison.Ordinal));

        var persisted =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            TagComportamentoIa.StatusCandidate,
            persisted.Status);

        Assert.Equal(
            item.BehaviorCode,
            persisted.Codigo);

        Assert.Equal(
            2,
            persisted.QuantidadeOcorrencias);

        Assert.Equal(
            1,
            await fixture.Context.TagsComportamentosSnapshotsIa
                .CountAsync());
    }

    [Fact]
    public async Task ConsolidateAsync_CandidateEmExecucoesSeparadas_DeveReutilizarMemoriaProvisoriaEManterIdempotencia()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            CreateService(
                fixture.Context);

        var first =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.0,
                        occurrenceCount: 1,
                        startDay: 1)
                });

        var firstItem =
            Assert.Single(
                first.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedCandidate,
            firstItem.Outcome);

        Assert.True(
            firstItem.Persistence!.CreatedBehavior);

        var provisionalCode =
            firstItem.BehaviorCode;

        var provisionalId =
            firstItem.BehaviorId;

        var second =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.1,
                        occurrenceCount: 1,
                        startDay: 2)
                });

        var secondItem =
            Assert.Single(
                second.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedCandidate,
            secondItem.Outcome);

        Assert.Equal(
            provisionalCode,
            secondItem.BehaviorCode);

        Assert.Equal(
            provisionalId,
            secondItem.BehaviorId);

        Assert.False(
            secondItem.Persistence!.CreatedBehavior);

        Assert.Equal(
            1,
            secondItem.Persistence.AddedOccurrences);

        Assert.True(
            secondItem.Persistence.AddedSnapshot);

        Assert.Equal(
            2,
            secondItem.Persistence.SnapshotVersion);

        var persisted =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            TagComportamentoIa.StatusCandidate,
            persisted.Status);

        Assert.Equal(
            2,
            persisted.QuantidadeOcorrencias);

        /*
         * Repetir exatamente a segunda janela deve continuar idempotente.
         */
        var repeated =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.1,
                        occurrenceCount: 1,
                        startDay: 2)
                });

        var repeatedItem =
            Assert.Single(
                repeated.Items);

        Assert.Equal(
            provisionalCode,
            repeatedItem.BehaviorCode);

        Assert.Equal(
            0,
            repeatedItem.Persistence!.AddedOccurrences);

        Assert.False(
            repeatedItem.Persistence.AddedSnapshot);

        Assert.Null(
            repeatedItem.Persistence.SnapshotVersion);

        Assert.Equal(
            2,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());

        Assert.Equal(
            2,
            await fixture.Context.TagsComportamentosSnapshotsIa
                .CountAsync());
    }

    [Fact]
    public async Task ConsolidateAsync_CandidateCumulativoMaduro_DevePromoverMesmoRegistroEManterIdempotenciaPosPromocao()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            CreateService(
                fixture.Context);

        /*
         * 1ª ocorrência: cria memória provisória CAND.
         */
        var first =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.0,
                        occurrenceCount: 1,
                        startDay: 1)
                });

        var firstItem =
            Assert.Single(
                first.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedCandidate,
            firstItem.Outcome);

        Assert.True(
            firstItem.BehaviorCode is not null &&
            firstItem.BehaviorCode.StartsWith(
                "CAND-",
                StringComparison.Ordinal));

        Assert.NotNull(
            firstItem.BehaviorId);

        var candidateCode =
            firstItem.BehaviorCode!;

        var behaviorId =
            firstItem.BehaviorId!.Value;

        /*
         * 2ª ocorrência: reutiliza o mesmo Candidate, mas ainda não atinge
         * o mínimo de 3 ocorrências para Known.
         */
        var second =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.1,
                        occurrenceCount: 1,
                        startDay: 2)
                });

        var secondItem =
            Assert.Single(
                second.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedCandidate,
            secondItem.Outcome);

        Assert.Equal(
            candidateCode,
            secondItem.BehaviorCode);

        Assert.Equal(
            behaviorId,
            secondItem.BehaviorId);

        Assert.Equal(
            2,
            secondItem.CandidateMemory!.CumulativeOccurrenceCount);

        /*
         * 3ª ocorrência: a memória acumulada agora possui 3 ocorrências
         * separadas e horizonte muito superior a 60 minutos.
         * Deve promover a MESMA linha para BEH/Known.
         */
        var third =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.05,
                        occurrenceCount: 1,
                        startDay: 3)
                });

        var promotedItem =
            Assert.Single(
                third.Items);

        Assert.Equal(
            BehaviorMemoryLifecycle.Known,
            promotedItem.Lifecycle);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PromotedCandidateToKnown,
            promotedItem.Outcome);

        Assert.Equal(
            "BEH-001",
            promotedItem.BehaviorCode);

        Assert.Equal(
            behaviorId,
            promotedItem.BehaviorId);

        Assert.NotNull(
            promotedItem.Promotion);

        Assert.True(
            promotedItem.Promotion!.Promoted);

        Assert.Equal(
            candidateCode,
            promotedItem.Promotion.PreviousBehaviorCode);

        Assert.Equal(
            "BEH-001",
            promotedItem.Promotion.BehaviorCode);

        Assert.Equal(
            behaviorId,
            promotedItem.Promotion.BehaviorId);

        Assert.Equal(
            1,
            third.PromotedCandidateCount);

        Assert.Equal(
            0,
            third.PersistedCandidateCount);

        var persistedAfterPromotion =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            behaviorId,
            persistedAfterPromotion.Id);

        Assert.Equal(
            "BEH-001",
            persistedAfterPromotion.Codigo);

        Assert.Equal(
            TagComportamentoIa.StatusKnown,
            persistedAfterPromotion.Status);

        Assert.Equal(
            TagComportamentoIa.OrigemAutomatic,
            persistedAfterPromotion.Origem);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            persistedAfterPromotion.Classificacao);

        Assert.Equal(
            3,
            persistedAfterPromotion.QuantidadeOcorrencias);

        Assert.Equal(
            3,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());

        Assert.Equal(
            3,
            await fixture.Context.TagsComportamentosSnapshotsIa
                .CountAsync());

        var versionsAfterPromotion =
            await fixture.Context.TagsComportamentosSnapshotsIa
                .OrderBy(x =>
                    x.Versao)
                .Select(x =>
                    x.Versao)
                .ToArrayAsync();

        Assert.Equal(
            new[]
            {
                1,
                2,
                3
            },
            versionsAfterPromotion);

        /*
         * Repetir a 3ª janela já depois da promoção deve entrar pela
         * reconciliação Known, e não voltar ao fluxo Candidate/promoção.
         *
         * Importante para este teste:
         * o FakeHistoricoService reconstrói as ocorrências persistidas e,
         * portanto, o snapshot v3 contém as métricas efetivamente
         * reconstruídas. Para testar apenas o roteamento pós-promoção e a
         * idempotência, a nova evidência usa essas mesmas métricas persistidas
         * no MESMO intervalo da terceira ocorrência.
         */
        var latestKnownSnapshot =
            await fixture.Context.TagsComportamentosSnapshotsIa
                .OrderByDescending(x =>
                    x.Versao)
                .FirstAsync();

        var repeated =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidateMatchingSnapshot(
                        groupIndex: 0,
                        startDay: 3,
                        latestKnownSnapshot)
                });

        var repeatedItem =
            Assert.Single(
                repeated.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedExistingBehavior,
            repeatedItem.Outcome);

        Assert.Equal(
            "BEH-001",
            repeatedItem.BehaviorCode);

        Assert.Equal(
            behaviorId,
            repeatedItem.BehaviorId);

        Assert.Null(
            repeatedItem.CandidateMemory);

        Assert.Null(
            repeatedItem.Promotion);

        Assert.NotNull(
            repeatedItem.Persistence);

        Assert.False(
            repeatedItem.Persistence!.CreatedBehavior);

        Assert.Equal(
            0,
            repeatedItem.Persistence.AddedOccurrences);

        Assert.False(
            repeatedItem.Persistence.AddedSnapshot);

        Assert.Null(
            repeatedItem.Persistence.SnapshotVersion);

        Assert.Equal(
            1,
            repeated.PersistedExistingCount);

        Assert.Equal(
            0,
            repeated.PromotedCandidateCount);

        Assert.Single(
            await fixture.Context.TagsComportamentosIa
                .ToListAsync());

        Assert.Equal(
            3,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());

        Assert.Equal(
            3,
            await fixture.Context.TagsComportamentosSnapshotsIa
                .CountAsync());
    }

    [Fact]
    public async Task ConsolidateAsync_FalhaNaPromocao_DeveReverterTerceiraEvidenciaCandidate()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            CreateService(
                fixture.Context);

        /*
         * 1ª ocorrência: cria a memória Candidate.
         */
        var first =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.0,
                        occurrenceCount: 1,
                        startDay: 1)
                });

        var firstItem =
            Assert.Single(
                first.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedCandidate,
            firstItem.Outcome);

        var candidateCode =
            firstItem.BehaviorCode!;

        var behaviorId =
            firstItem.BehaviorId!.Value;

        /*
         * 2ª ocorrência: reforça o mesmo Candidate, ainda sem promoção.
         */
        var second =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.1,
                        occurrenceCount: 1,
                        startDay: 2)
                });

        var secondItem =
            Assert.Single(
                second.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedCandidate,
            secondItem.Outcome);

        Assert.Equal(
            candidateCode,
            secondItem.BehaviorCode);

        Assert.Equal(
            behaviorId,
            secondItem.BehaviorId);

        Assert.Equal(
            2,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());

        Assert.Equal(
            2,
            await fixture.Context.TagsComportamentosSnapshotsIa
                .CountAsync());

        /*
         * Força falha APENAS na transição CAND-* -> BEH-*.
         *
         * Assim conseguimos provar se a persistência da 3ª evidência e a
         * promoção fazem parte da mesma unidade atômica.
         */
        await fixture.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_candidate_promotion
            BEFORE UPDATE OF Codigo ON TagsComportamentosIa
            WHEN NEW.Codigo LIKE 'BEH-%'
              AND OLD.Codigo LIKE 'CAND-%'
            BEGIN
                SELECT RAISE(ABORT, 'forced promotion failure');
            END;
            """);

        /*
         * 3ª ocorrência: torna a memória cumulativamente Known.
         * A promoção é rejeitada pelo trigger.
         */
        await Assert.ThrowsAsync<DbUpdateException>(
            () =>
                service.ConsolidateAsync(
                    "CLIENTE-A",
                    "TAG1",
                    new[]
                    {
                        CreateCandidate(
                            groupIndex: 0,
                            center: 40.05,
                            occurrenceCount: 1,
                            startDay: 3)
                    }));

        /*
         * Importante: consultar novamente o estado persistido, sem confiar
         * no ChangeTracker que participou da tentativa que falhou.
         */
        fixture.Context.ChangeTracker.Clear();

        var persisted =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync();

        Assert.Equal(
            behaviorId,
            persisted.Id);

        Assert.Equal(
            candidateCode,
            persisted.Codigo);

        Assert.Equal(
            TagComportamentoIa.StatusCandidate,
            persisted.Status);

        /*
         * Invariante de atomicidade:
         * se a promoção falhou, a 3ª evidência NÃO pode permanecer gravada.
         */
        Assert.Equal(
            2,
            persisted.QuantidadeOcorrencias);

        Assert.Equal(
            2,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());

        Assert.Equal(
            2,
            await fixture.Context.TagsComportamentosSnapshotsIa
                .CountAsync());

        var versions =
            await fixture.Context.TagsComportamentosSnapshotsIa
                .OrderBy(x =>
                    x.Versao)
                .Select(x =>
                    x.Versao)
                .ToArrayAsync();

        Assert.Equal(
            new[]
            {
                1,
                2
            },
            versions);
    }

    [Fact]
    public async Task ConsolidateAsync_DoisComportamentosNovos_DeveGerarIdentidadesDeterministicas()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        var service =
            CreateService(
                fixture.Context);

        var later =
            CreateCandidate(
                groupIndex: 9,
                center: 75,
                occurrenceCount: 3,
                startDay: 2);

        var earlier =
            CreateCandidate(
                groupIndex: 3,
                center: 40,
                occurrenceCount: 3,
                startDay: 1);

        var result =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    later,
                    earlier
                });

        Assert.Equal(
            2,
            result.PersistedNewCount);

        Assert.Equal(
            "BEH-001",
            result.Items[0].BehaviorCode);

        Assert.Equal(
            3,
            result.Items[0].GroupIndex);

        Assert.Equal(
            "BEH-002",
            result.Items[1].BehaviorCode);

        Assert.Equal(
            9,
            result.Items[1].GroupIndex);

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

    [Fact]
    public async Task ConsolidateAsync_MatchExistente_DeveReutilizarBeh()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await SeedBehaviorAsync(
            fixture.Context,
            code: "BEH-001",
            status: TagComportamentoIa.StatusKnown,
            mean: 40.05);

        var service =
            CreateService(
                fixture.Context);

        var result =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40,
                        occurrenceCount: 3,
                        startDay: 2)
                });

        var item =
            Assert.Single(
                result.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedExistingBehavior,
            item.Outcome);

        Assert.Equal(
            "BEH-001",
            item.BehaviorCode);

        Assert.Single(
            await fixture.Context.TagsComportamentosIa
                .ToListAsync());

        Assert.True(
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .AnyAsync());

        var latestSnapshot =
            await fixture.Context.TagsComportamentosSnapshotsIa
                .OrderByDescending(x =>
                    x.Versao)
                .FirstAsync();

        Assert.Equal(
            2,
            latestSnapshot.Versao);

        Assert.Equal(
            80,
            latestSnapshot.TotalRegistrosUsados);
    }

    [Fact]
    public async Task ConsolidateAsync_UmaNovaOcorrencia_DeveReforcarBehExistenteMesmoSemMaturidadeParaNovo()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await SeedBehaviorAsync(
            fixture.Context,
            code: "BEH-001",
            status: TagComportamentoIa.StatusKnown,
            mean: 40.05);

        var service =
            CreateService(
                fixture.Context);

        var result =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.0,
                        occurrenceCount: 1,
                        startDay: 2)
                });

        var item =
            Assert.Single(
                result.Items);

        Assert.Equal(
            BehaviorMemoryLifecycle.Candidate,
            item.Lifecycle);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedExistingBehavior,
            item.Outcome);

        Assert.Equal(
            "BEH-001",
            item.BehaviorCode);

        Assert.Equal(
            2,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());
    }

    [Fact]
    public async Task ConsolidateAsync_DeprecatedReservado_NovoComportamentoDevePularCodigo()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await SeedBehaviorAsync(
            fixture.Context,
            code: "BEH-001",
            status: TagComportamentoIa.StatusDeprecated,
            mean: 40);

        var service =
            CreateService(
                fixture.Context);

        var result =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 75,
                        occurrenceCount: 3,
                        startDay: 2)
                });

        var item =
            Assert.Single(
                result.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.PersistedNewBehavior,
            item.Outcome);

        Assert.Equal(
            "BEH-002",
            item.BehaviorCode);
    }

    [Fact]
    public async Task ConsolidateAsync_ReconciliacaoAmbigua_NaoDevePersistirDescoberta()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await SeedBehaviorAsync(
            fixture.Context,
            code: "BEH-001",
            status: TagComportamentoIa.StatusKnown,
            mean: 40.0);

        await SeedBehaviorAsync(
            fixture.Context,
            code: "BEH-002",
            status: TagComportamentoIa.StatusKnown,
            mean: 40.1);

        var initialOccurrences =
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync();

        var service =
            CreateService(
                fixture.Context);

        var result =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 0,
                        center: 40.05,
                        occurrenceCount: 3,
                        startDay: 2)
                });

        var item =
            Assert.Single(
                result.Items);

        Assert.Equal(
            BehaviorMemoryConsolidationOutcome.Ambiguous,
            item.Outcome);

        Assert.Equal(
            initialOccurrences,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());
    }

    [Fact]
    public async Task ConsolidateAsync_DoisGruposNoMesmoBehExistente_DeveBloquearFusao()
    {
        await using var fixture =
            await DatabaseFixture.CreateAsync();

        await SeedBehaviorAsync(
            fixture.Context,
            code: "BEH-001",
            status: TagComportamentoIa.StatusKnown,
            mean: 40.05);

        var initialOccurrences =
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync();

        var service =
            CreateService(
                fixture.Context);

        var result =
            await service.ConsolidateAsync(
                "CLIENTE-A",
                "TAG1",
                new[]
                {
                    CreateCandidate(
                        groupIndex: 1,
                        center: 39.9,
                        occurrenceCount: 3,
                        startDay: 2),

                    CreateCandidate(
                        groupIndex: 2,
                        center: 40.2,
                        occurrenceCount: 3,
                        startDay: 3)
                });

        Assert.Equal(
            2,
            result.Items.Count);

        Assert.All(
            result.Items,
            item =>
                Assert.Equal(
                    BehaviorMemoryConsolidationOutcome.ExistingBehaviorCollision,
                    item.Outcome));

        Assert.Equal(
            initialOccurrences,
            await fixture.Context.TagsComportamentosOcorrenciasIa
                .CountAsync());
    }

    private static BehaviorMemoryConsolidationService CreateService(
        AppDbContext context)
    {
        var planBuilder =
            new BehaviorMemoryPersistencePlanBuilder();

        var historicoService =
            new FakeHistoricoService();

        var statisticsCalculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var snapshotRebuildService =
            new BehaviorMemorySnapshotRebuildService(
                context,
                historicoService,
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
                snapshotRebuildService);

        return new BehaviorMemoryConsolidationService(
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
            snapshotRebuildService);
    }

    private static BehaviorDiscoveryCandidate CreateCandidateMatchingSnapshot(
        int groupIndex,
        int startDay,
        TagComportamentoSnapshotIa snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        var occurrence =
            CreateOccurrence(
                episode: 0,
                center: snapshot.Media,
                startDay: startDay)
            with
            {
                Media =
                    snapshot.Media,

                Mediana =
                    snapshot.Media,

                Minimo =
                    snapshot.Minimo,

                Maximo =
                    snapshot.Maximo,

                DesvioPadrao =
                    snapshot.DesvioPadrao,

                Amplitude =
                    snapshot.Amplitude,

                VariacaoMedia =
                    snapshot.VariacaoMedia,

                DeadbandRecomendado =
                    snapshot.DeadbandRecomendado
            };

        return new BehaviorDiscoveryCandidate
        {
            GroupIndex =
                groupIndex,

            OccurrenceCount =
                1,

            IsRecurrent =
                false,

            FirstOccurrenceStart =
                occurrence.Inicio,

            LastOccurrenceEnd =
                occurrence.Fim,

            RepresentativeProfile =
                new BehaviorCandidateRepresentativeProfile
                {
                    Media =
                        snapshot.Media,

                    Mediana =
                        snapshot.Media,

                    DesvioPadrao =
                        snapshot.DesvioPadrao,

                    IntervaloInterquartil =
                        occurrence.IntervaloInterquartil,

                    DeadbandRecomendado =
                        snapshot.DeadbandRecomendado,

                    RobustScale =
                        occurrence.ProfileFeatures!.RobustScale,

                    TypicalAbsoluteStep =
                        occurrence.ProfileFeatures.TypicalAbsoluteStep,

                    ChangeRatio =
                        occurrence.ProfileFeatures.ChangeRatio,

                    ReversalRatio =
                        occurrence.ProfileFeatures.ReversalRatio
                },

            Occurrences =
                new[]
                {
                    occurrence
                }
        };
    }

    private static BehaviorDiscoveryCandidate CreateCandidate(
        int groupIndex,
        double center,
        int occurrenceCount,
        int startDay = 1)
    {
        var occurrences =
            Enumerable.Range(
                    0,
                    occurrenceCount)
                .Select(index =>
                    CreateOccurrence(
                        index,
                        center +
                        (index * 0.05),
                        startDay))
                .ToArray();

        return new BehaviorDiscoveryCandidate
        {
            GroupIndex =
                groupIndex,

            OccurrenceCount =
                occurrenceCount,

            IsRecurrent =
                occurrenceCount >= 2,

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

                    IntervaloInterquartil =
                        0.6,

                    DeadbandRecomendado =
                        0.5,

                    RobustScale =
                        0.7,

                    TypicalAbsoluteStep =
                        0.5,

                    ChangeRatio =
                        1,

                    ReversalRatio =
                        1
                },

            Occurrences =
                occurrences
        };
    }

    private static BehaviorCandidateEpisodeCharacteristics CreateOccurrence(
        int episode,
        double center,
        int startDay)
    {
        var start =
            new DateTime(
                2026,
                9,
                startDay,
                0,
                0,
                0,
                DateTimeKind.Utc)
            .AddHours(
                episode * 2);

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
                                center +
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

            DuracaoMinutos =
                19,

            IsCharacterizable =
                true,

            OriginalSampleCount =
                20,

            UsedSampleCount =
                20,

            RemovedOutliers =
                0,

            Media =
                center,

            Mediana =
                center,

            Minimo =
                center - 1,

            Maximo =
                center + 1,

            DesvioPadrao =
                0.5,

            Amplitude =
                2,

            PrimeiroQuartil =
                center - 0.3,

            TerceiroQuartil =
                center + 0.3,

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

    private static async Task SeedBehaviorAsync(
        AppDbContext context,
        string code,
        string status,
        double mean)
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
                        1,
                        1,
                        0,
                        0,
                        DateTimeKind.Utc),

                QuantidadeOcorrencias =
                    1,

                TotalAmostras =
                    20,

                Confianca =
                    85
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
                    new DateTime(
                        2026,
                        8,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),

                DataHoraFim =
                    new DateTime(
                        2026,
                        8,
                        1,
                        0,
                        19,
                        0,
                        DateTimeKind.Utc),

                QuantidadeAmostras =
                    20,

                Confianca =
                    85,

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
                    mean,

                DesvioPadrao =
                    0.55,

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
                    20,

                TotalRegistrosUsados =
                    20,

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
                        1,
                        1,
                        0,
                        0,
                        DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();
    }

    private sealed class FakeHistoricoService :
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
            return Enumerable.Range(
                    0,
                    20)
                .Select(index =>
                    new LeituraBruta
                    {
                        DataHora =
                            inicio.AddMinutes(
                                index),

                        Valor =
                            (float)(
                                40.0 +
                                (
                                    index % 2 == 0
                                        ? -0.25
                                        : 0.25
                                ))
                    })
                .Where(x =>
                    x.DataHora <=
                    fim)
                .ToList();
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
