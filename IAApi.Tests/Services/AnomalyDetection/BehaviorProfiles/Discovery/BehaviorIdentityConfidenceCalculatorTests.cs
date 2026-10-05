using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorIdentityConfidenceCalculatorTests
{
    [Fact]
    public void Calculate_OcorrenciasIdenticas_DeveRetornarConfianca100()
    {
        var calculator =
            new BehaviorIdentityConfidenceCalculator();

        var result =
            calculator.Calculate(
                new[]
                {
                    CreateOccurrence(0, 40.0),
                    CreateOccurrence(1, 40.0),
                    CreateOccurrence(2, 40.0)
                });

        Assert.True(
            result.CanEvaluate);

        Assert.Equal(
            100.0,
            result.Confidence,
            precision: 5);

        Assert.Equal(
            3,
            result.PairCount);

        Assert.All(
            result.Occurrences,
            occurrence =>
                Assert.Equal(
                    100.0,
                    occurrence.Confidence,
                    precision: 5));
    }

    [Fact]
    public void Calculate_OcorrenciasProximas_DeveRetornarConfiancaEntreZeroECem()
    {
        var calculator =
            new BehaviorIdentityConfidenceCalculator();

        var result =
            calculator.Calculate(
                new[]
                {
                    CreateOccurrence(0, 40.0),
                    CreateOccurrence(1, 40.2),
                    CreateOccurrence(2, 40.3)
                });

        Assert.True(
            result.CanEvaluate);

        Assert.InRange(
            result.Confidence,
            0.000001,
            99.999999);
    }

    [Fact]
    public void Calculate_ParMaisProximoDoLimite_DeveReduzirConfiancaDoGrupo()
    {
        var calculator =
            new BehaviorIdentityConfidenceCalculator();

        var close =
            calculator.Calculate(
                new[]
                {
                    CreateOccurrence(0, 40.0),
                    CreateOccurrence(1, 40.1)
                });

        var nearLimit =
            calculator.Calculate(
                new[]
                {
                    CreateOccurrence(0, 40.0),
                    CreateOccurrence(1, 40.6)
                });

        Assert.True(
            close.CanEvaluate);

        Assert.True(
            nearLimit.CanEvaluate);

        Assert.True(
            close.Confidence >
            nearLimit.Confidence);
    }

    [Fact]
    public void Calculate_GrupoComParIncompativel_DeveRetornarNaoAvaliavel()
    {
        var calculator =
            new BehaviorIdentityConfidenceCalculator();

        var result =
            calculator.Calculate(
                new[]
                {
                    CreateOccurrence(0, 40.0),
                    CreateOccurrence(1, 40.2),
                    CreateOccurrence(2, 75.0)
                });

        Assert.False(
            result.CanEvaluate);

        Assert.Equal(
            0,
            result.Confidence);
    }

    [Fact]
    public void Calculate_OrdemDasOcorrencias_NaoDeveAlterarConfianca()
    {
        var calculator =
            new BehaviorIdentityConfidenceCalculator();

        var first =
            CreateOccurrence(
                0,
                40.0);

        var second =
            CreateOccurrence(
                1,
                40.2);

        var third =
            CreateOccurrence(
                2,
                40.3);

        var chronological =
            calculator.Calculate(
                new[]
                {
                    first,
                    second,
                    third
                });

        var shuffled =
            calculator.Calculate(
                new[]
                {
                    third,
                    first,
                    second
                });

        Assert.True(
            chronological.CanEvaluate);

        Assert.True(
            shuffled.CanEvaluate);

        Assert.Equal(
            chronological.Confidence,
            shuffled.Confidence,
            precision: 10);
    }

    private static BehaviorCandidateEpisodeCharacteristics
        CreateOccurrence(
            int episode,
            double median)
    {
        var start =
            new DateTime(
                2026,
                9,
                1,
                episode,
                0,
                0,
                DateTimeKind.Utc);

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
                    30),

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
