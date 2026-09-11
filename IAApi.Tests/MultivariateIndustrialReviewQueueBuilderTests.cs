using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class MultivariateIndustrialReviewQueueBuilderTests
{
    private readonly MultivariateIndustrialReviewQueueBuilder
        _builder = new();

    [Fact]
    public void SemRevisoes_DeveRetornarTodosComoPending()
    {
        var result =
            _builder.Build(
                Qualification(),
                []);

        Assert.Equal(
            2,
            result.TotalEvents);

        Assert.Equal(
            2,
            result.PendingCount);

        Assert.Equal(
            0,
            result.ReviewedCount);

        Assert.Equal(
            0,
            result.ReviewCompletionRate);

        Assert.All(
            result.Events,
            item =>
                Assert.Equal(
                    MultivariateIndustrialReviewQueueStates.Pending,
                    item.ReviewState));
    }

    [Fact]
    public void DeveCombinarRevisaoInconclusiveComEventoCorreto()
    {
        var result =
            _builder.Build(
                Qualification(),
                [
                    Review(
                        20,
                        MultivariateIndustrialEvidenceWindows.Holdout,
                        MultivariateIndustrialReviewStatuses.Inconclusive)
                ]);

        Assert.Equal(
            1,
            result.PendingCount);

        Assert.Equal(
            1,
            result.ReviewedCount);

        Assert.Equal(
            1,
            result.InconclusiveCount);

        Assert.Equal(
            0.5,
            result.ReviewCompletionRate,
            6);

        var reviewed =
            result.Events
                .Single(item =>
                    item.MinuteIndex == 20);

        Assert.Equal(
            MultivariateIndustrialReviewStatuses.Inconclusive,
            reviewed.ReviewState);

        Assert.Equal(
            "Engenharia",
            reviewed.Reviewer);

        Assert.Equal(
            "REL-20",
            reviewed.EvidenceReference);
    }

    [Fact]
    public void DeveCalcularContadoresDeRevisoesDecisivas()
    {
        var qualification =
            Qualification(
                industrialValidationConfirmed:
                    true,
                eligibleForExplicitApproval:
                    true,
                status:
                    MultivariateOperationalQualificationStatuses
                        .QualifiedForExplicitApproval);

        var result =
            _builder.Build(
                qualification,
                [
                    Review(
                        10,
                        MultivariateIndustrialEvidenceWindows.Reference,
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent),

                    Review(
                        20,
                        MultivariateIndustrialEvidenceWindows.Holdout,
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation)
                ]);

        Assert.Equal(
            0,
            result.PendingCount);

        Assert.Equal(
            2,
            result.ReviewedCount);

        Assert.Equal(
            2,
            result.DecisiveReviewCount);

        Assert.Equal(
            1,
            result.ConfirmedIndustrialEventCount);

        Assert.Equal(
            1,
            result.NormalOperationCount);

        Assert.Equal(
            1,
            result.ReviewCompletionRate,
            6);

        Assert.True(
            result.IndustrialValidationConfirmed);

        Assert.True(
            result.EligibleForExplicitApproval);
    }

    [Fact]
    public void RevisaoDeOutroModelo_NaoDeveSerCombinada()
    {
        var review =
            Review(
                20,
                MultivariateIndustrialEvidenceWindows.Holdout,
                MultivariateIndustrialReviewStatuses
                    .ConfirmedIndustrialEvent);

        review.ModelVersion =
            2;

        var result =
            _builder.Build(
                Qualification(),
                [review]);

        Assert.Equal(
            2,
            result.PendingCount);

        Assert.All(
            result.Events,
            item =>
                Assert.Equal(
                    MultivariateIndustrialReviewQueueStates.Pending,
                    item.ReviewState));
    }

    private static OperationalMultivariateQualificationRunResult
        Qualification(
            bool industrialValidationConfirmed = false,
            bool eligibleForExplicitApproval = false,
            string? status = null)
    {
        var modelTrainedAtUtc =
            new DateTime(
                2026,
                9,
                3,
                19,
                7,
                2,
                DateTimeKind.Utc);

        return new OperationalMultivariateQualificationRunResult
        {
            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            ModelVersion =
                1,

            ModelAlgorithm =
                "OnlineMeanCovariance",

            ModelTrainedAtUtc =
                modelTrainedAtUtc,

            Threshold =
                75,

            ReferenciaInicio =
                new DateTime(
                    2026,
                    9,
                    1),

            ReferenciaFim =
                new DateTime(
                    2026,
                    9,
                    3,
                    23,
                    59,
                    0),

            HoldoutInicio =
                new DateTime(
                    2026,
                    9,
                    4),

            HoldoutFim =
                new DateTime(
                    2026,
                    9,
                    4,
                    17,
                    0,
                    0),

            IndustrialValidationEvidence =
                new MultivariateIndustrialValidationEvidence
                {
                    ClienteId =
                        "PK7",

                    GroupName =
                        "GRUPO-AB",

                    ModelVersion =
                        1,

                    ModelTrainedAtUtc =
                        modelTrainedAtUtc,

                    Threshold =
                        75,

                    EvidenceEventCount =
                        2,

                    EvidenceIsCompleteForObservedAnomalies =
                        true,

                    Events =
                    [
                        Event(
                            10,
                            MultivariateIndustrialEvidenceWindows
                                .Reference,
                            new DateTime(
                                2026,
                                9,
                                1,
                                8,
                                0,
                                0)),

                        Event(
                            20,
                            MultivariateIndustrialEvidenceWindows
                                .Holdout,
                            new DateTime(
                                2026,
                                9,
                                4,
                                12,
                                40,
                                0))
                    ]
                },

            IndustrialReviewAssessment =
                new MultivariateIndustrialReviewAssessmentResult
                {
                    IndustrialValidationConfirmed =
                        industrialValidationConfirmed
                },

            Qualification =
                new MultivariateOperationalQualificationResult
                {
                    Status =
                        status ??
                        MultivariateOperationalQualificationStatuses
                            .TechnicallyQualifiedPendingIndustrialValidation,

                    EligibleForExplicitApproval =
                        eligibleForExplicitApproval
                }
        };
    }

    private static MultivariateIndustrialEvidenceEvent Event(
        long minuteIndex,
        string sourceWindow,
        DateTime eventDateTime)
    {
        return new MultivariateIndustrialEvidenceEvent
        {
            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            MinuteIndex =
                minuteIndex,

            EventDateTime =
                eventDateTime,

            SourceWindow =
                sourceWindow,

            IsAnomaly =
                true,

            Score =
                75.2,

            Threshold =
                75,

            MahalanobisSquared =
                5.5,

            EvidenceType =
                MultivariateEvidenceTypes.JointDeviation,

            EvidenceReason =
                "Joint deviation.",

            DominantTag =
                "TAG-A",

            DominantSquaredZShare =
                0.52,

            MaximumAbsoluteCorrelation =
                0.0017,

            RelationshipDirectionContradicted =
                false,

            Tags =
                ["TAG-A", "TAG-B"],

            Contributions =
            [
                new MultivariateIndustrialEvidenceTag
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
                        2,

                    IndividualDeviationShare =
                        0.8
                },

                new MultivariateIndustrialEvidenceTag
                {
                    TagName =
                        "TAG-B",

                    ValorAtual =
                        21,

                    MediaTreinamento =
                        20,

                    DesvioPadraoTreinamento =
                        1,

                    ZScoreAbsoluto =
                        1,

                    IndividualDeviationShare =
                        0.2
                }
            ]
        };
    }

    private static MultivariateIndustrialEventReviewIa Review(
        long minuteIndex,
        string sourceWindow,
        string status)
    {
        return new MultivariateIndustrialEventReviewIa
        {
            Id =
                minuteIndex,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            MinuteIndex =
                minuteIndex,

            SourceWindow =
                sourceWindow,

            ModelVersion =
                1,

            ModelTrainedAtUtc =
                new DateTime(
                    2026,
                    9,
                    3,
                    19,
                    7,
                    2,
                    DateTimeKind.Utc),

            Threshold =
                75,

            ReviewStatus =
                status,

            Reviewer =
                "Engenharia",

            Notes =
                "Revisão teste.",

            EvidenceReference =
                $"REL-{minuteIndex}",

            ReviewedAtUtc =
                new DateTime(
                    2026,
                    9,
                    10,
                    19,
                    0,
                    0,
                    DateTimeKind.Utc)
        };
    }
}
