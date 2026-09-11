using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class MultivariateHistoricalBacktestAnalyzerTests
{
    private readonly MultivariateHistoricalBacktestAnalyzer
        _analyzer = new();

    [Fact]
    public void DeveContarNormaisAnomaliasETaxa()
    {
        var results =
            new[]
            {
                Result(1, 20, false),
                Result(2, 40, false),
                Result(3, 80, true),
                Result(4, 90, true)
            };

        var summary =
            _analyzer.Analisar(
                results);

        Assert.Equal(
            4,
            summary.ApplicableResults);

        Assert.Equal(
            2,
            summary.AnomalyCount);

        Assert.Equal(
            2,
            summary.NormalCount);

        Assert.Equal(
            0.5,
            summary.AnomalyRate,
            6);
    }

    [Fact]
    public void NaoAplicavel_NaoDeveEntrarNaDistribuicao()
    {
        var results =
            new[]
            {
                Result(1, 10, false),
                Result(2, 90, true),
                NotApplicable(
                    3,
                    "Modelo ausente")
            };

        var summary =
            _analyzer.Analisar(
                results);

        Assert.Equal(
            3,
            summary.TotalResults);

        Assert.Equal(
            2,
            summary.ApplicableResults);

        Assert.Equal(
            1,
            summary.NotApplicableResults);

        Assert.Equal(
            2,
            summary.ScoreDistribution.Count);

        Assert.Single(
            summary.NonApplicableReasons);

        Assert.Equal(
            "Modelo ausente",
            summary.NonApplicableReasons[0].Reason);
    }

    [Fact]
    public void DeveCalcularPercentisComInterpolacao()
    {
        var results =
            new[]
            {
                Result(1, 0, false),
                Result(2, 25, false),
                Result(3, 50, false),
                Result(4, 75, true),
                Result(5, 100, true)
            };

        var summary =
            _analyzer.Analisar(
                results);

        Assert.Equal(
            50,
            summary.ScoreDistribution.P50,
            6);

        Assert.Equal(
            75,
            summary.ScoreDistribution.P75,
            6);

        Assert.Equal(
            100,
            summary.ScoreDistribution.Maximum,
            6);
    }

    [Fact]
    public void DeveCalcularSensibilidadePorThreshold()
    {
        var results =
            new[]
            {
                Result(1, 49, false),
                Result(2, 50, false),
                Result(3, 74.9, false),
                Result(4, 75, true),
                Result(5, 95, true)
            };

        var summary =
            _analyzer.Analisar(
                results,
                thresholds:
                    new[]
                    {
                        50.0,
                        75.0,
                        90.0
                    });

        var at50 =
            Assert.Single(
                summary.ThresholdSensitivity,
                item =>
                    item.Threshold == 50);

        var at75 =
            Assert.Single(
                summary.ThresholdSensitivity,
                item =>
                    item.Threshold == 75);

        var at90 =
            Assert.Single(
                summary.ThresholdSensitivity,
                item =>
                    item.Threshold == 90);

        Assert.Equal(
            4,
            at50.EventCount);

        Assert.Equal(
            2,
            at75.EventCount);

        Assert.Equal(
            1,
            at90.EventCount);
    }

    [Fact]
    public void TopEvents_DevemSerOrdenadosPorScore()
    {
        var summary =
            _analyzer.Analisar(
                new[]
                {
                    Result(1, 60, false),
                    Result(2, 99, true),
                    Result(3, 80, true)
                },
                topEventsLimit:
                    2);

        Assert.Equal(
            2,
            summary.TopEvents.Count);

        Assert.Equal(
            99,
            summary.TopEvents[0].Score,
            6);

        Assert.Equal(
            80,
            summary.TopEvents[1].Score,
            6);
    }

    [Fact]
    public void DeveMedirMaiorSequenciaDeMinutosAnomalos()
    {
        var summary =
            _analyzer.Analisar(
                new[]
                {
                    Result(10, 80, true),
                    Result(11, 82, true),
                    Result(12, 83, true),
                    Result(20, 85, true),
                    Result(21, 86, true),
                    Result(30, 10, false)
                });

        Assert.Equal(
            5,
            summary.DistinctAnomalyMinutes);

        Assert.Equal(
            3,
            summary.LongestConsecutiveAnomalyRunMinutes);
    }

    [Fact]
    public void TopEvents_DevemPreservarContribuicoesDasTags()
    {
        var result =
            Result(
                100,
                90,
                true);

        result.Contribuicoes =
        [
            new MultivariateTagContribution
            {
                TagName =
                    "TAG-A",

                ValorAtual =
                    15,

                MediaTreinamento =
                    10,

                DesvioPadraoTreinamento =
                    2,

                ZScoreAbsoluto =
                    2.5
            },
            new MultivariateTagContribution
            {
                TagName =
                    "TAG-B",

                ValorAtual =
                    22,

                MediaTreinamento =
                    20,

                DesvioPadraoTreinamento =
                    1,

                ZScoreAbsoluto =
                    2.0
            }
        ];

        var summary =
            _analyzer.Analisar(
                [result],
                topEventsLimit:
                    1);

        var top =
            Assert.Single(
                summary.TopEvents);

        Assert.Equal(
            2,
            top.Contribuicoes.Count);

        Assert.Equal(
            "TAG-A",
            top.Contribuicoes[0].TagName);

        Assert.Equal(
            2.5,
            top.Contribuicoes[0].ZScoreAbsoluto,
            6);

        Assert.Equal(
            "TAG-B",
            top.Contribuicoes[1].TagName);
    }

    [Fact]
    public void TopEvents_ComModelo_DeveClassificarJointDeviation()
    {
        var result =
            Result(
                100,
                80,
                true);

        result.Contribuicoes =
        [
            new MultivariateTagContribution
            {
                TagName =
                    "TAG1",

                ValorAtual =
                    8.3,

                MediaTreinamento =
                    10,

                DesvioPadraoTreinamento =
                    1,

                ZScoreAbsoluto =
                    1.7
            },
            new MultivariateTagContribution
            {
                TagName =
                    "TAG2",

                ValorAtual =
                    21.6,

                MediaTreinamento =
                    20,

                DesvioPadraoTreinamento =
                    1,

                ZScoreAbsoluto =
                    1.6
            }
        ];

        result.TagsUtilizadas =
            ["TAG1", "TAG2"];

        var model =
            new MultivariateGroupModel
            {
                Version =
                    1,

                ClienteId =
                    "PK7",

                GroupName =
                    "GRUPO",

                TrainedAtUtc =
                    new DateTime(
                        2026,
                        9,
                        3,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc),

                Algorithm =
                    "OnlineMeanCovariance",

                SampleCount =
                    1000,

                MinimumAvailableTags =
                    2,

                MaxLagMinutes =
                    2,

                Tags =
                    ["TAG1", "TAG2"],

                Means =
                    [10, 20],

                StandardDeviations =
                    [1, 1],

                CovarianceMatrix =
                [
                    [1, 0.0017],
                    [0.0017, 1]
                ],

                CorrelationMatrix =
                [
                    [1, 0.0017],
                    [0.0017, 1]
                ]
            };

        var summary =
            _analyzer.Analisar(
                [result],
                model:
                    model);

        var top =
            Assert.Single(
                summary.TopEvents);

        Assert.Equal(
            MultivariateEvidenceTypes
                .JointDeviation,
            top.EvidenceType);

        Assert.NotNull(
            top.MaximumAbsoluteCorrelation);

        Assert.Equal(
            0.0017,
            top.MaximumAbsoluteCorrelation!.Value,
            6);

        Assert.NotNull(
            top.DominantSquaredZShare);
    }

    private static MultivariateDetectionResult Result(
        long minuteIndex,
        double score,
        bool anomaly)
    {
        return new MultivariateDetectionResult
        {
            Aplicavel =
                true,

            EhAnomalia =
                anomaly,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO",

            MinuteIndex =
                minuteIndex,

            ScoreNormalizado =
                score,

            LimiteScore =
                75,

            MahalanobisSquared =
                score / 10.0,

            TagsUtilizadas =
                ["TAG1", "TAG2"]
        };
    }

    private static MultivariateDetectionResult NotApplicable(
        long minuteIndex,
        string reason)
    {
        return new MultivariateDetectionResult
        {
            Aplicavel =
                false,

            EhAnomalia =
                false,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO",

            MinuteIndex =
                minuteIndex,

            Explicacao =
                reason
        };
    }
}
