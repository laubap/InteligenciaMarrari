using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorMemoryPersistencePlanBuilderTests
{
    [Fact]
    public void Build_KnownElegivel_DeveMapearComportamentoPersistente()
    {
        var builder =
            new BehaviorMemoryPersistencePlanBuilder();

        var fixture =
            CreateFixture();

        var createdAt =
            new DateTime(
                2026,
                10,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        var plan =
            builder.Build(
                "CLIENTE",
                "TAG.TESTE",
                fixture.Proposal,
                fixture.Confidence,
                snapshotVersion: 1,
                createdAtUtc: createdAt);

        Assert.Equal(
            "CLIENTE",
            plan.Behavior.ClienteId);

        Assert.Equal(
            "TAG.TESTE",
            plan.Behavior.TagName);

        Assert.Equal(
            "BEH-001",
            plan.Behavior.Codigo);

        Assert.Equal(
            TagComportamentoIa.StatusKnown,
            plan.Behavior.Status);

        Assert.Equal(
            TagComportamentoIa.OrigemAutomatic,
            plan.Behavior.Origem);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            plan.Behavior.Classificacao);

        Assert.Equal(
            3,
            plan.Behavior.QuantidadeOcorrencias);

        Assert.Equal(
            60,
            plan.Behavior.TotalAmostras);

        Assert.Equal(
            createdAt,
            plan.Behavior.DataCriacao);
    }

    [Fact]
    public void Build_DeveMapearConfiancaIndividualDasOcorrencias()
    {
        var builder =
            new BehaviorMemoryPersistencePlanBuilder();

        var fixture =
            CreateFixture();

        var plan =
            builder.Build(
                "CLIENTE",
                "TAG.TESTE",
                fixture.Proposal,
                fixture.Confidence);

        Assert.Equal(
            3,
            plan.Occurrences.Count);

        Assert.Equal(
            91,
            plan.Occurrences[0].Confianca,
            precision: 5);

        Assert.Equal(
            82,
            plan.Occurrences[1].Confianca,
            precision: 5);

        Assert.Equal(
            73,
            plan.Occurrences[2].Confianca,
            precision: 5);

        Assert.All(
            plan.Occurrences,
            occurrence =>
            {
                Assert.True(
                    occurrence.UtilizadaNoAprendizado);

                Assert.Equal(
                    20,
                    occurrence.QuantidadeAmostras);

                Assert.Equal(
                    0,
                    occurrence.TagComportamentoIaId);
            });
    }

    [Fact]
    public void Build_SnapshotNaoDeveCriarVariacaoEntreFronteirasDeEpisodios()
    {
        var builder =
            new BehaviorMemoryPersistencePlanBuilder();

        var fixture =
            CreateFixture(
                medians:
                    new[]
                    {
                        40.0,
                        40.2,
                        40.4
                    },
                variation:
                    0.25);

        var plan =
            builder.Build(
                "CLIENTE",
                "TAG.TESTE",
                fixture.Proposal,
                fixture.Confidence);

        /*
         * Se os episódios fossem concatenados ingenuamente,
         * haveria saltos artificiais nas fronteiras.
         *
         * O snapshot deve preservar somente a variação intraepisódio.
         */
        Assert.Equal(
            0.25,
            plan.Snapshot.VariacaoMedia,
            precision: 5);

        Assert.Equal(
            60,
            plan.Snapshot.TotalRegistrosUsados);

        Assert.Equal(
            1,
            plan.Snapshot.Versao);

        Assert.Equal(
            0,
            plan.Snapshot.TagComportamentoIaId);
    }

    [Fact]
    public void Build_SnapshotDeveUsarMediaPonderadaEDispersaoCombinada()
    {
        var builder =
            new BehaviorMemoryPersistencePlanBuilder();

        var fixture =
            CreateFixture(
                medians:
                    new[]
                    {
                        40.0,
                        40.2,
                        40.4
                    });

        var plan =
            builder.Build(
                "CLIENTE",
                "TAG.TESTE",
                fixture.Proposal,
                fixture.Confidence);

        Assert.Equal(
            40.2,
            plan.Snapshot.Media,
            precision: 5);

        Assert.True(
            plan.Snapshot.DesvioPadrao >
            0.5);

        Assert.Equal(
            39.0,
            plan.Snapshot.Minimo,
            precision: 5);

        Assert.Equal(
            41.4,
            plan.Snapshot.Maximo,
            precision: 5);

        Assert.Equal(
            2.4,
            plan.Snapshot.Amplitude,
            precision: 5);
    }

    [Fact]
    public void Build_ProposalSemIdentidadeEstavel_DeveFalhar()
    {
        var builder =
            new BehaviorMemoryPersistencePlanBuilder();

        var fixture =
            CreateFixture();

        var invalid =
            fixture.Proposal with
            {
                HasStableIdentityProposal =
                    false,

                ProposedIdentity =
                    null
            };

        Assert.Throws<InvalidOperationException>(
            () =>
                builder.Build(
                    "CLIENTE",
                    "TAG.TESTE",
                    invalid,
                    fixture.Confidence));
    }

    private static (
        BehaviorIdentityProposal Proposal,
        BehaviorIdentityConfidenceResult Confidence)
        CreateFixture(
            double[]? medians = null,
            double variation = 0.25)
    {
        medians ??=
            new[]
            {
                40.0,
                40.2,
                40.4
            };

        var occurrences =
            medians
                .Select(
                    (median, index) =>
                        CreateOccurrence(
                            index,
                            median,
                            variation))
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
                        Mediana =
                            40.2
                    },

                Occurrences =
                    occurrences
            };

        var evaluation =
            new BehaviorMemoryLifecycleEvaluation
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
                    150,

                RecurrenceSatisfied =
                    true,

                OccurrenceThresholdSatisfied =
                    true,

                EvidenceSpanSatisfied =
                    true,

                Candidate =
                    candidate
            };

        var proposal =
            new BehaviorIdentityProposal
            {
                GroupIndex =
                    0,

                Lifecycle =
                    BehaviorMemoryLifecycle.Known,

                HasStableIdentityProposal =
                    true,

                ProposedIdentity =
                    "BEH-001",

                Evaluation =
                    evaluation
            };

        var confidence =
            new BehaviorIdentityConfidenceResult
            {
                CanEvaluate =
                    true,

                Confidence =
                    73,

                PairCount =
                    3,

                WeakestPairConfidence =
                    73,

                Occurrences =
                    new[]
                    {
                        new BehaviorOccurrenceIdentityConfidence
                        {
                            SourceSegmentIndex = 0,
                            EpisodeIndex = 0,
                            Confidence = 91
                        },
                        new BehaviorOccurrenceIdentityConfidence
                        {
                            SourceSegmentIndex = 0,
                            EpisodeIndex = 1,
                            Confidence = 82
                        },
                        new BehaviorOccurrenceIdentityConfidence
                        {
                            SourceSegmentIndex = 0,
                            EpisodeIndex = 2,
                            Confidence = 73
                        }
                    }
            };

        return (
            proposal,
            confidence);
    }

    private static BehaviorCandidateEpisodeCharacteristics
        CreateOccurrence(
            int episode,
            double median,
            double variation)
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
                                median +
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
                median,

            Mediana =
                median,

            Minimo =
                median - 1.0,

            Maximo =
                median + 1.0,

            DesvioPadrao =
                0.5,

            Amplitude =
                2.0,

            PrimeiroQuartil =
                median - 0.3,

            TerceiroQuartil =
                median + 0.3,

            IntervaloInterquartil =
                0.6,

            VariacaoMedia =
                variation,

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
}
