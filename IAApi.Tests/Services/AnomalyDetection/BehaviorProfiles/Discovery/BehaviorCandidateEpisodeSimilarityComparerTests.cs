using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorCandidateEpisodeSimilarityComparerTests
{
    [Fact]
    public void Compare_EpisodiosParecidos_DeveRetornarSimilar()
    {
        var comparer =
            new BehaviorCandidateEpisodeSimilarityComparer();

        var first =
            CreateCharacteristics(
                median: 40.0,
                mean: 40.0,
                stdDev: 0.5,
                robustScale: 0.7,
                q1: 39.7,
                q3: 40.3,
                typicalStep: 0.20);

        var second =
            CreateCharacteristics(
                median: 40.2,
                mean: 40.2,
                stdDev: 0.6,
                robustScale: 0.8,
                q1: 39.9,
                q3: 40.5,
                typicalStep: 0.25);

        var result =
            comparer.Compare(
                first,
                second);

        Assert.True(
            result.CanCompare);

        Assert.True(
            result.IsSimilar);

        Assert.True(
            result.CenterStable);

        Assert.True(
            result.ScaleStable);

        Assert.True(
            result.DistributionStable);

        Assert.True(
            result.DynamicsStable);
    }

    [Fact]
    public void Compare_PatamaresDiferentes_DeveRetornarNaoSimilar()
    {
        var comparer =
            new BehaviorCandidateEpisodeSimilarityComparer();

        var first =
            CreateCharacteristics(
                median: 40,
                mean: 40,
                stdDev: 0.5,
                robustScale: 0.7,
                q1: 39.7,
                q3: 40.3,
                typicalStep: 0.2);

        var second =
            CreateCharacteristics(
                median: 75,
                mean: 75,
                stdDev: 0.5,
                robustScale: 0.7,
                q1: 74.7,
                q3: 75.3,
                typicalStep: 0.2);

        var result =
            comparer.Compare(
                first,
                second);

        Assert.True(
            result.CanCompare);

        Assert.False(
            result.IsSimilar);

        Assert.False(
            result.CenterStable);

        Assert.Contains(
            "centro incompatível",
            result.Reason);
    }

    [Fact]
    public void Compare_MesmoCentroComEscalasMuitoDiferentes_DeveRetornarNaoSimilar()
    {
        var comparer =
            new BehaviorCandidateEpisodeSimilarityComparer();

        var first =
            CreateCharacteristics(
                median: 40,
                mean: 40,
                stdDev: 0.5,
                robustScale: 0.5,
                q1: 39.6,
                q3: 40.4,
                typicalStep: 0.2,
                deadband: 0.1);

        var second =
            CreateCharacteristics(
                median: 40,
                mean: 40,
                stdDev: 3.0,
                robustScale: 3.0,
                q1: 39.7,
                q3: 40.3,
                typicalStep: 0.2,
                deadband: 0.1);

        var result =
            comparer.Compare(
                first,
                second);

        Assert.False(
            result.IsSimilar);

        Assert.True(
            result.CenterStable);

        Assert.False(
            result.ScaleStable);
    }

    [Fact]
    public void Compare_PassoTipicoMuitoDiferente_DeveRetornarNaoSimilar()
    {
        var comparer =
            new BehaviorCandidateEpisodeSimilarityComparer();

        var first =
            CreateCharacteristics(
                median: 40,
                mean: 40,
                stdDev: 0.5,
                robustScale: 0.7,
                q1: 39.7,
                q3: 40.3,
                typicalStep: 0.1);

        var second =
            CreateCharacteristics(
                median: 40,
                mean: 40,
                stdDev: 0.5,
                robustScale: 0.7,
                q1: 39.7,
                q3: 40.3,
                typicalStep: 1.0);

        var result =
            comparer.Compare(
                first,
                second);

        Assert.False(
            result.IsSimilar);

        Assert.True(
            result.CenterStable);

        Assert.True(
            result.ScaleStable);

        Assert.True(
            result.DistributionStable);

        Assert.False(
            result.DynamicsStable);
    }

    [Fact]
    public void Compare_EpisodioNaoCaracterizavel_DeveRetornarNaoComparavel()
    {
        var comparer =
            new BehaviorCandidateEpisodeSimilarityComparer();

        var first =
            CreateCharacteristics(
                median: 40,
                mean: 40,
                stdDev: 0.5,
                robustScale: 0.7,
                q1: 39.7,
                q3: 40.3,
                typicalStep: 0.2);

        var second =
            CreateCharacteristics(
                median: 40,
                mean: 40,
                stdDev: 0.5,
                robustScale: 0.7,
                q1: 39.7,
                q3: 40.3,
                typicalStep: 0.2) with
            {
                IsCharacterizable =
                    false
            };

        var result =
            comparer.Compare(
                first,
                second);

        Assert.False(
            result.CanCompare);

        Assert.False(
            result.IsSimilar);
    }

    private static BehaviorCandidateEpisodeCharacteristics
        CreateCharacteristics(
            double median,
            double mean,
            double stdDev,
            double robustScale,
            double q1,
            double q3,
            double typicalStep,
            double deadband = 0.2)
    {
        return new BehaviorCandidateEpisodeCharacteristics
        {
            IsCharacterizable =
                true,

            Media =
                mean,

            Mediana =
                median,

            DesvioPadrao =
                stdDev,

            PrimeiroQuartil =
                q1,

            TerceiroQuartil =
                q3,

            IntervaloInterquartil =
                q3 - q1,

            DeadbandRecomendado =
                deadband,

            ProfileFeatures =
                new BehaviorProfileFeatures
                {
                    RobustScale =
                        robustScale,

                    TypicalAbsoluteStep =
                        typicalStep,

                    ChangeRatio =
                        0.70,

                    ReversalRatio =
                        0.45
                }
        };
    }
}
