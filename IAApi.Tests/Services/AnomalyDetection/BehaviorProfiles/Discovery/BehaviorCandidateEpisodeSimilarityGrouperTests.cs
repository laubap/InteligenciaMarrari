using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorCandidateEpisodeSimilarityGrouperTests
{
    [Fact]
    public void Group_EpisodiosRecorrentes_DeveAgruparOcorrenciasSemelhantes()
    {
        var grouper =
            CreateGrouper();

        var episodes =
            new[]
            {
                CreateCharacteristics(
                    segment: 0,
                    episode: 0,
                    median: 40.0),

                CreateCharacteristics(
                    segment: 0,
                    episode: 1,
                    median: 75.0),

                CreateCharacteristics(
                    segment: 0,
                    episode: 2,
                    median: 40.2)
            };

        var result =
            grouper.Group(
                episodes);

        Assert.Equal(
            2,
            result.Groups.Count);

        Assert.Equal(
            2,
            result.Groups[0].OccurrenceCount);

        Assert.Equal(
            new[] { 0, 2 },
            result.Groups[0]
                .Members
                .Select(x => x.EpisodeIndex));

        Assert.Single(
            result.Groups[1].Members);

        Assert.Equal(
            1,
            result.Groups[1].Members[0].EpisodeIndex);

        Assert.Empty(
            result.ExcludedEpisodes);
    }

    [Fact]
    public void Group_NaoDeveUsarEncadeamentoTransitivo()
    {
        var grouper =
            CreateGrouper();

        /*
         * floor = 0.7:
         *
         * A=40.0 e B=40.6 -> semelhantes
         * B=40.6 e C=41.2 -> semelhantes
         * A=40.0 e C=41.2 -> NÃO semelhantes
         *
         * C não pode entrar no grupo A/B apenas por parecer com B.
         */
        var episodes =
            new[]
            {
                CreateCharacteristics(
                    segment: 0,
                    episode: 0,
                    median: 40.0),

                CreateCharacteristics(
                    segment: 0,
                    episode: 1,
                    median: 40.6),

                CreateCharacteristics(
                    segment: 0,
                    episode: 2,
                    median: 41.2)
            };

        var result =
            grouper.Group(
                episodes);

        Assert.Equal(
            2,
            result.Groups.Count);

        Assert.Equal(
            new[] { 0, 1 },
            result.Groups[0]
                .Members
                .Select(x => x.EpisodeIndex));

        Assert.Single(
            result.Groups[1].Members);

        Assert.Equal(
            2,
            result.Groups[1].Members[0].EpisodeIndex);
    }

    [Fact]
    public void Group_EpisodioNaoCaracterizavel_DeveSerExcluido()
    {
        var grouper =
            CreateGrouper();

        var valid =
            CreateCharacteristics(
                segment: 0,
                episode: 0,
                median: 40);

        var invalid =
            CreateCharacteristics(
                segment: 0,
                episode: 1,
                median: 40) with
            {
                IsCharacterizable =
                    false
            };

        var result =
            grouper.Group(
                new[]
                {
                    valid,
                    invalid
                });

        Assert.Single(
            result.Groups);

        Assert.Single(
            result.Groups[0].Members);

        Assert.Single(
            result.ExcludedEpisodes);

        Assert.Equal(
            1,
            result.ExcludedEpisodes[0].EpisodeIndex);
    }

    [Fact]
    public void Group_EpisodioSemProfileFeatures_DeveSerExcluido()
    {
        var grouper =
            CreateGrouper();

        var invalid =
            CreateCharacteristics(
                segment: 0,
                episode: 0,
                median: 40) with
            {
                ProfileFeatures =
                    null
            };

        var result =
            grouper.Group(
                new[]
                {
                    invalid
                });

        Assert.Empty(
            result.Groups);

        Assert.Single(
            result.ExcludedEpisodes);
    }

    [Fact]
    public void Group_EntradaForaDeOrdem_DeveProduzirAgrupamentoDeterministico()
    {
        var grouper =
            CreateGrouper();

        var first =
            CreateCharacteristics(
                segment: 0,
                episode: 0,
                median: 40.0);

        var second =
            CreateCharacteristics(
                segment: 0,
                episode: 1,
                median: 75.0);

        var third =
            CreateCharacteristics(
                segment: 0,
                episode: 2,
                median: 40.2);

        var result =
            grouper.Group(
                new[]
                {
                    third,
                    second,
                    first
                });

        Assert.Equal(
            2,
            result.Groups.Count);

        Assert.Equal(
            new[] { 0, 2 },
            result.Groups[0]
                .Members
                .Select(x => x.EpisodeIndex));

        Assert.Equal(
            1,
            result.Groups[1]
                .Members
                .Single()
                .EpisodeIndex);
    }

    private static BehaviorCandidateEpisodeSimilarityGrouper
        CreateGrouper()
    {
        return new BehaviorCandidateEpisodeSimilarityGrouper(
            new BehaviorCandidateEpisodeSimilarityComparer());
    }

    private static BehaviorCandidateEpisodeCharacteristics
        CreateCharacteristics(
            int segment,
            int episode,
            double median)
    {
        return new BehaviorCandidateEpisodeCharacteristics
        {
            SourceSegmentIndex =
                segment,

            EpisodeIndex =
                episode,

            Inicio =
                new DateTime(
                    2026,
                    9,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc)
                .AddHours(
                    episode),

            IsCharacterizable =
                true,

            Media =
                median,

            Mediana =
                median,

            DesvioPadrao =
                0.5,

            PrimeiroQuartil =
                median - 0.3,

            TerceiroQuartil =
                median + 0.3,

            IntervaloInterquartil =
                0.6,

            DeadbandRecomendado =
                0.2,

            ProfileFeatures =
                new BehaviorProfileFeatures
                {
                    RobustScale =
                        0.7,

                    TypicalAbsoluteStep =
                        0.2,

                    ChangeRatio =
                        0.70,

                    ReversalRatio =
                        0.45
                }
        };
    }
}
