namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.4D2A
///
/// Cruza o pacote de evidência industrial com as revisões persistidas e
/// deriva o estado de validação industrial.
///
/// A validação só é considerada confirmada quando:
/// - o pacote de evidência cobre todas as anomalias observadas;
/// - todos os eventos possuem revisão correspondente ao mesmo
///   cliente/grupo/modelo/threshold/janela;
/// - nenhuma revisão está Inconclusive;
/// - todas as revisões possuem referência auditável;
/// - existe ao menos um evento ConfirmedIndustrialEvent.
///
/// IMPORTANTE:
/// isso NÃO aprova o grupo e NÃO altera ApprovedGroups.
/// Apenas remove a dependência do boolean manual.
/// </summary>
public sealed class MultivariateIndustrialReviewAssessmentService
{
    public MultivariateIndustrialReviewAssessmentResult Assess(
        MultivariateIndustrialValidationEvidence evidence,
        IReadOnlyList<MultivariateIndustrialEventReviewIa> reviews)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(reviews);

        var blockers =
            new List<string>();

        var warnings =
            new List<string>();

        if (!evidence.EvidenceIsCompleteForObservedAnomalies)
        {
            blockers.Add(
                "PacoteEvidenciaIncompleto");
        }

        var expectedEvents =
            evidence.Events
                .Where(item =>
                    item is not null &&
                    item.IsAnomaly)
                .ToArray();

        if (expectedEvents.Length == 0)
        {
            blockers.Add(
                "NenhumEventoAnomaloParaRevisao");
        }

        var matchingReviews =
            reviews
                .Where(item =>
                    item is not null &&
                    MatchesEvidenceIdentity(
                        evidence,
                        item))
                .ToArray();

        var matched =
            new List<MultivariateIndustrialEventReviewIa>();

        foreach (var expectedEvent in expectedEvents)
        {
            var candidates =
                matchingReviews
                    .Where(review =>
                        review.MinuteIndex ==
                            expectedEvent.MinuteIndex &&
                        string.Equals(
                            review.SourceWindow,
                            expectedEvent.SourceWindow,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            if (candidates.Length == 0)
            {
                blockers.Add(
                    $"RevisaoAusente:{expectedEvent.SourceWindow}:{expectedEvent.MinuteIndex}");

                continue;
            }

            if (candidates.Length > 1)
            {
                blockers.Add(
                    $"RevisaoDuplicada:{expectedEvent.SourceWindow}:{expectedEvent.MinuteIndex}");

                continue;
            }

            matched.Add(
                candidates[0]);
        }

        var inconclusiveCount =
            matched.Count(review =>
                string.Equals(
                    review.ReviewStatus,
                    MultivariateIndustrialReviewStatuses
                        .Inconclusive,
                    StringComparison.OrdinalIgnoreCase));

        if (inconclusiveCount > 0)
        {
            blockers.Add(
                "ExistemRevisoesInconclusivas");
        }

        var missingEvidenceReferenceCount =
            matched.Count(review =>
                string.IsNullOrWhiteSpace(
                    review.EvidenceReference));

        if (missingEvidenceReferenceCount > 0)
        {
            blockers.Add(
                "ExistemRevisoesSemReferenciaAuditavel");
        }

        var confirmedIndustrialEventCount =
            matched.Count(review =>
                string.Equals(
                    review.ReviewStatus,
                    MultivariateIndustrialReviewStatuses
                        .ConfirmedIndustrialEvent,
                    StringComparison.OrdinalIgnoreCase));

        var normalOperationCount =
            matched.Count(review =>
                string.Equals(
                    review.ReviewStatus,
                    MultivariateIndustrialReviewStatuses
                        .NormalOperation,
                    StringComparison.OrdinalIgnoreCase));

        if (matched.Count > 0 &&
            matched.Count ==
                expectedEvents.Length &&
            confirmedIndustrialEventCount == 0)
        {
            blockers.Add(
                "NenhumEventoIndustrialConfirmado");
        }

        if (normalOperationCount > 0)
        {
            warnings.Add(
                "ExistemEventosClassificadosComoOperacaoNormal");
        }

        var allExpectedEventsReviewed =
            expectedEvents.Length > 0 &&
            matched.Count ==
                expectedEvents.Length &&
            !blockers.Any(item =>
                item.StartsWith(
                    "RevisaoAusente:",
                    StringComparison.OrdinalIgnoreCase) ||
                item.StartsWith(
                    "RevisaoDuplicada:",
                    StringComparison.OrdinalIgnoreCase));

        var validationConfirmed =
            evidence.EvidenceIsCompleteForObservedAnomalies &&
            allExpectedEventsReviewed &&
            blockers.Count == 0;

        var auditReference =
            validationConfirmed
                ? BuildAuditReference(
                    matched)
                : null;

        return new MultivariateIndustrialReviewAssessmentResult
        {
            ExpectedEventCount =
                expectedEvents.Length,

            MatchedReviewCount =
                matched.Count,

            ConfirmedIndustrialEventCount =
                confirmedIndustrialEventCount,

            NormalOperationCount =
                normalOperationCount,

            InconclusiveCount =
                inconclusiveCount,

            MissingEvidenceReferenceCount =
                missingEvidenceReferenceCount,

            AllExpectedEventsReviewed =
                allExpectedEventsReviewed,

            IndustrialValidationConfirmed =
                validationConfirmed,

            IndustrialValidationReference =
                auditReference,

            Blockers =
                blockers
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),

            Warnings =
                warnings
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
        };
    }

    private static bool MatchesEvidenceIdentity(
        MultivariateIndustrialValidationEvidence evidence,
        MultivariateIndustrialEventReviewIa review)
    {
        return
            string.Equals(
                review.ClienteId,
                evidence.ClienteId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                review.GroupName,
                evidence.GroupName,
                StringComparison.OrdinalIgnoreCase) &&
            review.ModelVersion ==
                evidence.ModelVersion &&
            NormalizeUtc(
                review.ModelTrainedAtUtc) ==
                NormalizeUtc(
                    evidence.ModelTrainedAtUtc) &&
            Math.Abs(
                review.Threshold -
                evidence.Threshold) <
                1e-9;
    }

    private static string BuildAuditReference(
        IReadOnlyCollection<
            MultivariateIndustrialEventReviewIa> reviews)
    {
        var references =
            reviews
                .Select(item =>
                    item.EvidenceReference?.Trim())
                .Where(item =>
                    !string.IsNullOrWhiteSpace(
                        item))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(item =>
                    item,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return
            $"PersistedIndustrialReviews:{string.Join("|", references)}";
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }
}

public sealed class MultivariateIndustrialReviewAssessmentResult
{
    public int ExpectedEventCount { get; init; }

    public int MatchedReviewCount { get; init; }

    public int ConfirmedIndustrialEventCount { get; init; }

    public int NormalOperationCount { get; init; }

    public int InconclusiveCount { get; init; }

    public int MissingEvidenceReferenceCount { get; init; }

    public bool AllExpectedEventsReviewed { get; init; }

    public bool IndustrialValidationConfirmed { get; init; }

    public string? IndustrialValidationReference { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } =
        Array.Empty<string>();
}