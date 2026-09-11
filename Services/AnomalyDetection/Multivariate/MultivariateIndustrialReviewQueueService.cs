namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.4D3
///
/// Monta uma fila de revisão industrial a partir da qualificação real,
/// combinando os eventos anômalos do pacote de evidência com as revisões
/// persistidas para o mesmo cliente/grupo/modelo/threshold.
///
/// Este serviço é SOMENTE de consulta/organização.
/// NÃO:
/// - altera ApprovedGroups;
/// - altera readiness;
/// - habilita promoção;
/// - altera RuntimeDecision;
/// - cria ou atualiza revisões.
/// </summary>
public interface IMultivariateIndustrialReviewQueueService
{
    Task<MultivariateIndustrialReviewQueueResult>
        BuildAsync(
            string clienteId,
            string groupName,
            DateTime referenciaInicio,
            DateTime referenciaFim,
            DateTime holdoutInicio,
            DateTime holdoutFim,
            double threshold = 75.0,
            CancellationToken cancellationToken = default);
}

public sealed class MultivariateIndustrialReviewQueueService
    : IMultivariateIndustrialReviewQueueService
{
    private readonly OperationalMultivariateQualificationService
        _qualificationService;

    private readonly MultivariateIndustrialEventReviewStore
        _reviewStore;

    private readonly MultivariateIndustrialReviewQueueBuilder
        _builder;

    public MultivariateIndustrialReviewQueueService(
        OperationalMultivariateQualificationService qualificationService,
        MultivariateIndustrialEventReviewStore reviewStore,
        MultivariateIndustrialReviewQueueBuilder builder)
    {
        _qualificationService =
            qualificationService ??
            throw new ArgumentNullException(
                nameof(qualificationService));

        _reviewStore =
            reviewStore ??
            throw new ArgumentNullException(
                nameof(reviewStore));

        _builder =
            builder ??
            throw new ArgumentNullException(
                nameof(builder));
    }

    public async Task<MultivariateIndustrialReviewQueueResult>
        BuildAsync(
            string clienteId,
            string groupName,
            DateTime referenciaInicio,
            DateTime referenciaFim,
            DateTime holdoutInicio,
            DateTime holdoutFim,
            double threshold = 75.0,
            CancellationToken cancellationToken = default)
    {
        var qualification =
            await _qualificationService
                .QualificarAsync(
                    clienteId,
                    groupName,
                    referenciaInicio,
                    referenciaFim,
                    holdoutInicio,
                    holdoutFim,
                    threshold,
                    cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var reviews =
            await _reviewStore
                .ListAsync(
                    qualification.ClienteId,
                    qualification.GroupName,
                    qualification.ModelVersion,
                    qualification.ModelTrainedAtUtc,
                    qualification.Threshold,
                    cancellationToken);

        return _builder
            .Build(
                qualification,
                reviews);
    }
}

/// <summary>
/// Faz apenas o merge determinístico entre evidência e revisão.
/// Separado do serviço para ser testável sem executar backtest.
/// </summary>
public sealed class MultivariateIndustrialReviewQueueBuilder
{
    public MultivariateIndustrialReviewQueueResult Build(
        OperationalMultivariateQualificationRunResult qualification,
        IReadOnlyList<MultivariateIndustrialEventReviewIa> reviews)
    {
        ArgumentNullException.ThrowIfNull(
            qualification);

        ArgumentNullException.ThrowIfNull(
            reviews);

        var evidence =
            qualification.IndustrialValidationEvidence ??
            throw new InvalidOperationException(
                "A qualificação não contém pacote de evidência industrial.");

        var queueEvents =
            evidence.Events
                .Where(item =>
                    item is not null &&
                    item.IsAnomaly)
                .Select(item =>
                    BuildEvent(
                        qualification,
                        item,
                        reviews))
                .OrderBy(item =>
                    item.EventDateTime ??
                    DateTime.MaxValue)
                .ThenBy(item =>
                    item.MinuteIndex)
                .ToArray();

        var pendingCount =
            queueEvents.Count(item =>
                string.Equals(
                    item.ReviewState,
                    MultivariateIndustrialReviewQueueStates.Pending,
                    StringComparison.OrdinalIgnoreCase));

        var inconclusiveCount =
            queueEvents.Count(item =>
                string.Equals(
                    item.ReviewState,
                    MultivariateIndustrialReviewStatuses.Inconclusive,
                    StringComparison.OrdinalIgnoreCase));

        var confirmedIndustrialEventCount =
            queueEvents.Count(item =>
                string.Equals(
                    item.ReviewState,
                    MultivariateIndustrialReviewStatuses
                        .ConfirmedIndustrialEvent,
                    StringComparison.OrdinalIgnoreCase));

        var normalOperationCount =
            queueEvents.Count(item =>
                string.Equals(
                    item.ReviewState,
                    MultivariateIndustrialReviewStatuses.NormalOperation,
                    StringComparison.OrdinalIgnoreCase));

        var reviewedCount =
            queueEvents.Length -
            pendingCount;

        var decisiveReviewCount =
            confirmedIndustrialEventCount +
            normalOperationCount;

        return new MultivariateIndustrialReviewQueueResult
        {
            ClienteId =
                qualification.ClienteId,

            GroupName =
                qualification.GroupName,

            ModelVersion =
                qualification.ModelVersion,

            ModelAlgorithm =
                qualification.ModelAlgorithm,

            ModelTrainedAtUtc =
                qualification.ModelTrainedAtUtc,

            Threshold =
                qualification.Threshold,

            ReferenciaInicio =
                qualification.ReferenciaInicio,

            ReferenciaFim =
                qualification.ReferenciaFim,

            HoldoutInicio =
                qualification.HoldoutInicio,

            HoldoutFim =
                qualification.HoldoutFim,

            TotalEvents =
                queueEvents.Length,

            PendingCount =
                pendingCount,

            ReviewedCount =
                reviewedCount,

            DecisiveReviewCount =
                decisiveReviewCount,

            InconclusiveCount =
                inconclusiveCount,

            ConfirmedIndustrialEventCount =
                confirmedIndustrialEventCount,

            NormalOperationCount =
                normalOperationCount,

            ReviewCompletionRate =
                queueEvents.Length == 0
                    ? 0
                    : (double) reviewedCount /
                      queueEvents.Length,

            IndustrialValidationConfirmed =
                qualification
                    .IndustrialReviewAssessment
                    .IndustrialValidationConfirmed,

            EligibleForExplicitApproval =
                qualification
                    .Qualification
                    .EligibleForExplicitApproval,

            QualificationStatus =
                qualification
                    .Qualification
                    .Status,

            Events =
                queueEvents
        };
    }

    private static MultivariateIndustrialReviewQueueEvent
        BuildEvent(
            OperationalMultivariateQualificationRunResult qualification,
            MultivariateIndustrialEvidenceEvent evidence,
            IReadOnlyList<MultivariateIndustrialEventReviewIa> reviews)
    {
        var candidates =
            reviews
                .Where(review =>
                    ReviewMatches(
                        qualification,
                        evidence,
                        review))
                .ToArray();

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                $"Mais de uma revisão encontrada para " +
                $"{evidence.SourceWindow}:{evidence.MinuteIndex}.");
        }

        var review =
            candidates.SingleOrDefault();

        return new MultivariateIndustrialReviewQueueEvent
        {
            MinuteIndex =
                evidence.MinuteIndex,

            EventDateTime =
                evidence.EventDateTime,

            SourceWindow =
                evidence.SourceWindow,

            Score =
                evidence.Score,

            Threshold =
                evidence.Threshold,

            MahalanobisSquared =
                evidence.MahalanobisSquared,

            EvidenceType =
                evidence.EvidenceType,

            EvidenceReason =
                evidence.EvidenceReason,

            DominantTag =
                evidence.DominantTag,

            DominantSquaredZShare =
                evidence.DominantSquaredZShare,

            MaximumAbsoluteCorrelation =
                evidence.MaximumAbsoluteCorrelation,

            RelationshipDirectionContradicted =
                evidence.RelationshipDirectionContradicted,

            Tags =
                evidence.Tags
                    .ToArray(),

            Contributions =
                evidence.Contributions
                    .ToArray(),

            ReviewState =
                review?.ReviewStatus ??
                MultivariateIndustrialReviewQueueStates.Pending,

            ReviewId =
                review?.Id,

            Reviewer =
                review?.Reviewer,

            ReviewNotes =
                review?.Notes,

            EvidenceReference =
                review?.EvidenceReference,

            ReviewedAtUtc =
                review?.ReviewedAtUtc
        };
    }

    private static bool ReviewMatches(
        OperationalMultivariateQualificationRunResult qualification,
        MultivariateIndustrialEvidenceEvent evidence,
        MultivariateIndustrialEventReviewIa review)
    {
        return
            string.Equals(
                review.ClienteId,
                qualification.ClienteId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                review.GroupName,
                qualification.GroupName,
                StringComparison.OrdinalIgnoreCase) &&
            review.MinuteIndex ==
                evidence.MinuteIndex &&
            string.Equals(
                review.SourceWindow,
                evidence.SourceWindow,
                StringComparison.OrdinalIgnoreCase) &&
            review.ModelVersion ==
                qualification.ModelVersion &&
            NormalizeUtc(
                review.ModelTrainedAtUtc) ==
                NormalizeUtc(
                    qualification.ModelTrainedAtUtc) &&
            Math.Abs(
                review.Threshold -
                qualification.Threshold) <
                1e-9;
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

public static class MultivariateIndustrialReviewQueueStates
{
    public const string Pending =
        "Pending";
}

public sealed class MultivariateIndustrialReviewQueueResult
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public int ModelVersion { get; init; }

    public string ModelAlgorithm { get; init; } =
        "";

    public DateTime ModelTrainedAtUtc { get; init; }

    public double Threshold { get; init; }

    public DateTime ReferenciaInicio { get; init; }

    public DateTime ReferenciaFim { get; init; }

    public DateTime HoldoutInicio { get; init; }

    public DateTime HoldoutFim { get; init; }

    public int TotalEvents { get; init; }

    public int PendingCount { get; init; }

    public int ReviewedCount { get; init; }

    public int DecisiveReviewCount { get; init; }

    public int InconclusiveCount { get; init; }

    public int ConfirmedIndustrialEventCount { get; init; }

    public int NormalOperationCount { get; init; }

    public double ReviewCompletionRate { get; init; }

    public bool IndustrialValidationConfirmed { get; init; }

    public bool EligibleForExplicitApproval { get; init; }

    public string QualificationStatus { get; init; } =
        "";

    public IReadOnlyList<
        MultivariateIndustrialReviewQueueEvent>
        Events { get; init; } =
            Array.Empty<
                MultivariateIndustrialReviewQueueEvent>();
}

public sealed class MultivariateIndustrialReviewQueueEvent
{
    public long MinuteIndex { get; init; }

    public DateTime? EventDateTime { get; init; }

    public string SourceWindow { get; init; } =
        "";

    public double Score { get; init; }

    public double Threshold { get; init; }

    public double MahalanobisSquared { get; init; }

    public string? EvidenceType { get; init; }

    public string? EvidenceReason { get; init; }

    public string? DominantTag { get; init; }

    public double? DominantSquaredZShare { get; init; }

    public double? MaximumAbsoluteCorrelation { get; init; }

    public bool? RelationshipDirectionContradicted { get; init; }

    public IReadOnlyList<string> Tags { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<
        MultivariateIndustrialEvidenceTag>
        Contributions { get; init; } =
            Array.Empty<
                MultivariateIndustrialEvidenceTag>();

    public string ReviewState { get; init; } =
        MultivariateIndustrialReviewQueueStates.Pending;

    public long? ReviewId { get; init; }

    public string? Reviewer { get; init; }

    public string? ReviewNotes { get; init; }

    public string? EvidenceReference { get; init; }

    public DateTime? ReviewedAtUtc { get; init; }
}
