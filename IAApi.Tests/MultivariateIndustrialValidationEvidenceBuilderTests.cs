using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class MultivariateIndustrialValidationEvidenceBuilderTests
{
    private readonly MultivariateIndustrialValidationEvidenceBuilder
        _builder = new();

    [Fact]
    public void DeveReconstruirDataHoraDoMinuteIndexSemInventarFuso()
    {
        var minute =
            new DateTime(
                2026,
                9,
                4,
                15,
                55,
                0,
                DateTimeKind.Unspecified);

        var result =
            _builder.Build(
                Model(),
                Summary(
                    anomalyCount:
                        0),
                Summary(
                    anomalyCount:
                        1,
                    Event(
                        minute,
                        isAnomaly:
                            true)),
                75);

        var evidence =
            Assert.Single(
                result.Events);

        Assert.Equal(
            minute,
            evidence.EventDateTime);

        Assert.Equal(
            DateTimeKind.Unspecified,
            evidence.EventDateTime!.Value.Kind);

        Assert.Equal(
            MultivariateIndustrialEvidenceWindows
                .Holdout,
            evidence.SourceWindow);
    }

    [Fact]
    public void DeveCalcularParticipacaoIndividualPorZQuadrado()
    {
        var result =
            _builder.Build(
                Model(),
                Summary(
                    anomalyCount:
                        0),
                Summary(
                    anomalyCount:
                        1,
                    Event(
                        new DateTime(
                            2026,
                            9,
                            4,
                            10,
                            0,
                            0),
                        isAnomaly:
                            true,
                        z1:
                            2,
                        z2:
                            1)),
                75);

        var evidence =
            Assert.Single(
                result.Events);

        Assert.Equal(
            2,
            evidence.Contributions.Count);

        var tagA =
            evidence.Contributions
                .Single(item =>
                    item.TagName ==
                    "TAG-A");

        var tagB =
            evidence.Contributions
                .Single(item =>
                    item.TagName ==
                    "TAG-B");

        Assert.Equal(
            0.8,
            tagA.IndividualDeviationShare!.Value,
            6);

        Assert.Equal(
            0.2,
            tagB.IndividualDeviationShare!.Value,
            6);
    }

    [Fact]
    public void DeveIncluirSomenteEventosAnomalosParaRevisaoIndustrial()
    {
        var result =
            _builder.Build(
                Model(),
                Summary(
                    anomalyCount:
                        1,
                    Event(
                        new DateTime(
                            2026,
                            9,
                            1,
                            8,
                            0,
                            0),
                        isAnomaly:
                            true),
                    Event(
                        new DateTime(
                            2026,
                            9,
                            1,
                            9,
                            0,
                            0),
                        isAnomaly:
                            false)),
                Summary(
                    anomalyCount:
                        0),
                75);

        var evidence =
            Assert.Single(
                result.Events);

        Assert.True(
            evidence.IsAnomaly);

        Assert.Equal(
            MultivariateIndustrialEvidenceWindows
                .Reference,
            evidence.SourceWindow);
    }

    [Fact]
    public void DeveSinalizarQuandoTopEventsNaoCobremTodasAsAnomalias()
    {
        var result =
            _builder.Build(
                Model(),
                Summary(
                    anomalyCount:
                        3,
                    Event(
                        new DateTime(
                            2026,
                            9,
                            1,
                            8,
                            0,
                            0),
                        isAnomaly:
                            true)),
                Summary(
                    anomalyCount:
                        2,
                    Event(
                        new DateTime(
                            2026,
                            9,
                            4,
                            8,
                            0,
                            0),
                        isAnomaly:
                            true)),
                75);

        Assert.Equal(
            5,
            result.ReferenceAnomalyCount +
            result.HoldoutAnomalyCount);

        Assert.Equal(
            2,
            result.EvidenceEventCount);

        Assert.False(
            result.EvidenceIsCompleteForObservedAnomalies);
    }

    private static MultivariateGroupModel Model()
    {
        return new MultivariateGroupModel
        {
            Version =
                1,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            TrainedAtUtc =
                new DateTime(
                    2026,
                    9,
                    3,
                    19,
                    7,
                    2,
                    DateTimeKind.Utc),

            Algorithm =
                "OnlineMeanCovariance",

            SampleCount =
                1403,

            MinimumAvailableTags =
                2,

            MaxLagMinutes =
                2,

            Tags =
                ["TAG-A", "TAG-B"],

            Means =
                [10, 20],

            StandardDeviations =
                [1, 1],

            CovarianceMatrix =
            [
                [1, 0],
                [0, 1]
            ],

            CorrelationMatrix =
            [
                [1, 0],
                [0, 1]
            ]
        };
    }

    private static MultivariateHistoricalBacktestSummary
        Summary(
            int anomalyCount,
            params MultivariateBacktestEvent[] events)
    {
        return new MultivariateHistoricalBacktestSummary
        {
            TotalResults =
                Math.Max(
                    anomalyCount,
                    events.Length),

            ApplicableResults =
                Math.Max(
                    anomalyCount,
                    events.Length),

            AnomalyCount =
                anomalyCount,

            NormalCount =
                Math.Max(
                    0,
                    events.Length -
                    anomalyCount),

            TopEvents =
                events
        };
    }

    private static MultivariateBacktestEvent
        Event(
            DateTime dateTime,
            bool isAnomaly,
            double z1 = 1.7,
            double z2 = 1.6)
    {
        var minuteIndex =
            dateTime.Ticks /
            TimeSpan.TicksPerMinute;

        return new MultivariateBacktestEvent
        {
            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            MinuteIndex =
                minuteIndex,

            IsAnomaly =
                isAnomaly,

            Score =
                isAnomaly
                    ? 75.2
                    : 74.9,

            Threshold =
                75,

            MahalanobisSquared =
                5.5,

            Tags =
                ["TAG-A", "TAG-B"],

            EvidenceType =
                MultivariateEvidenceTypes
                    .JointDeviation,

            EvidenceReason =
                "Joint deviation test.",

            DominantTag =
                "TAG-A",

            DominantSquaredZShare =
                0.52,

            MaximumAbsoluteCorrelation =
                0.0017,

            RelationshipCorrelation =
                0.0017,

            RelationshipDirectionContradicted =
                false,

            Contribuicoes =
            [
                new MultivariateTagContribution
                {
                    TagName =
                        "TAG-A",

                    ValorAtual =
                        12,

                    MediaTreinamento =
                        10,

                    DesvioPadraoTreinamento =
                        1,

                    ZScoreAbsoluto =
                        z1
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
                        z2
                }
            ]
        };
    }
}