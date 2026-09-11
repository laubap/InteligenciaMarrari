using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class MultivariateIndustrialReviewAssessmentServiceTests
{
    private readonly MultivariateIndustrialReviewAssessmentService
        _service = new();

    [Fact]
    public void SemRevisoes_DeveBloquear()
    {
        var result =
            _service.Assess(
                Evidence(),
                []);

        Assert.False(
            result.IndustrialValidationConfirmed);

        Assert.False(
            result.AllExpectedEventsReviewed);

        Assert.Contains(
            result.Blockers,
            blocker =>
                blocker.StartsWith(
                    "RevisaoAusente:",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TodasRevisadasMasUmaInconclusiva_DeveBloquear()
    {
        var result =
            _service.Assess(
                Evidence(),
                [
                    Review(
                        10,
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent),
                    Review(
                        20,
                        MultivariateIndustrialReviewStatuses
                            .Inconclusive)
                ]);

        Assert.False(
            result.IndustrialValidationConfirmed);

        Assert.True(
            result.AllExpectedEventsReviewed);

        Assert.Equal(
            1,
            result.InconclusiveCount);

        Assert.Contains(
            "ExistemRevisoesInconclusivas",
            result.Blockers);
    }

    [Fact]
    public void TodasOperacaoNormal_DeveBloquearPorFaltaDeEventoIndustrialConfirmado()
    {
        var result =
            _service.Assess(
                Evidence(),
                [
                    Review(
                        10,
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation),
                    Review(
                        20,
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation)
                ]);

        Assert.False(
            result.IndustrialValidationConfirmed);

        Assert.Contains(
            "NenhumEventoIndustrialConfirmado",
            result.Blockers);

        Assert.Contains(
            "ExistemEventosClassificadosComoOperacaoNormal",
            result.Warnings);
    }

    [Fact]
    public void RevisaoSemReferenciaAuditavel_DeveBloquear()
    {
        var result =
            _service.Assess(
                Evidence(),
                [
                    Review(
                        10,
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent,
                        evidenceReference:
                            null),
                    Review(
                        20,
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation)
                ]);

        Assert.False(
            result.IndustrialValidationConfirmed);

        Assert.Equal(
            1,
            result.MissingEvidenceReferenceCount);

        Assert.Contains(
            "ExistemRevisoesSemReferenciaAuditavel",
            result.Blockers);
    }

    [Fact]
    public void TodasRevisadasDecisivasComAoMenosUmEventoConfirmado_DeveConfirmarValidacao()
    {
        var result =
            _service.Assess(
                Evidence(),
                [
                    Review(
                        10,
                        MultivariateIndustrialReviewStatuses
                            .ConfirmedIndustrialEvent,
                        evidenceReference:
                            "REL-001"),
                    Review(
                        20,
                        MultivariateIndustrialReviewStatuses
                            .NormalOperation,
                        evidenceReference:
                            "REL-002")
                ]);

        Assert.True(
            result.IndustrialValidationConfirmed);

        Assert.True(
            result.AllExpectedEventsReviewed);

        Assert.Equal(
            1,
            result.ConfirmedIndustrialEventCount);

        Assert.Equal(
            1,
            result.NormalOperationCount);

        Assert.Empty(
            result.Blockers);

        Assert.Contains(
            "REL-001",
            result.IndustrialValidationReference);

        Assert.Contains(
            "REL-002",
            result.IndustrialValidationReference);
    }

    private static MultivariateIndustrialValidationEvidence
        Evidence()
    {
        return new MultivariateIndustrialValidationEvidence
        {
            ClienteId =
                "PK7",

            GroupName =
                "PK7_TESTE_MULTIVARIADO",

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

            ReferenceAnomalyCount =
                1,

            HoldoutAnomalyCount =
                1,

            EvidenceEventCount =
                2,

            EvidenceIsCompleteForObservedAnomalies =
                true,

            Events =
            [
                new MultivariateIndustrialEvidenceEvent
                {
                    SourceWindow =
                        MultivariateIndustrialEvidenceWindows
                            .Reference,

                    ClienteId =
                        "PK7",

                    GroupName =
                        "PK7_TESTE_MULTIVARIADO",

                    MinuteIndex =
                        10,

                    IsAnomaly =
                        true
                },

                new MultivariateIndustrialEvidenceEvent
                {
                    SourceWindow =
                        MultivariateIndustrialEvidenceWindows
                            .Holdout,

                    ClienteId =
                        "PK7",

                    GroupName =
                        "PK7_TESTE_MULTIVARIADO",

                    MinuteIndex =
                        20,

                    IsAnomaly =
                        true
                }
            ]
        };
    }

    private static MultivariateIndustrialEventReviewIa
        Review(
            long minuteIndex,
            string status,
            string? evidenceReference = "REL-001")
    {
        return new MultivariateIndustrialEventReviewIa
        {
            ClienteId =
                "PK7",

            GroupName =
                "PK7_TESTE_MULTIVARIADO",

            MinuteIndex =
                minuteIndex,

            SourceWindow =
                minuteIndex == 10
                    ? MultivariateIndustrialEvidenceWindows
                        .Reference
                    : MultivariateIndustrialEvidenceWindows
                        .Holdout,

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

            EvidenceReference =
                evidenceReference
        };
    }
}