using IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

namespace IAApi.Benchmark.ExperimentalIncidents.ReadModels;

public enum IncidentMilestoneKind
{
    RawAlertObserved,
    IncidentOpened,
    CandidateReady,
    StableUnresolved,
    CandidateRejected,
    CandidateMismatchStarted,
    IncidentEscalated,
    DataQualitySuspended,
    DataQualityResumed,
    ReturnedToActive,
    IncidentResolved,
    ExecutionBoundary
}

public sealed record IncidentMilestone(
    IncidentMilestoneKind Kind,
    DateTime Timestamp,
    long MinuteIndex,
    string Reason,
    string Lifecycle,
    string? CandidateId = null,
    string? CandidateBaselineId = null);

public sealed record IncidentReadModel(
    string IncidentId,
    string OriginatingRawAlertId,
    string Tag,
    int FastWindow,
    string Lifecycle,
    DateTime OpenedAt,
    long OpenedMinuteIndex,
    DateTime PromotedAt,
    long PromotedMinuteIndex,
    DateTime LastUpdatedAt,
    long LastUpdatedMinuteIndex,
    double DurationMinutes,
    double ObservedDurationMinutes,
    string? ActiveBaselineId,
    string? CandidateBaselineId,
    string RegimeEpisodeId,
    string? CurrentCandidateId,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> CandidateBaselineIds,
    ExperimentalDetectorSnapshot FirstDetection,
    ExperimentalDetectorSnapshot CurrentEvidence,
    ExperimentalDetectorSnapshot MaximumEvidence,
    DateTime? CandidateReadyAt,
    DateTime? StableUnresolvedAt,
    long ContinuationCount,
    long CandidateMismatchCount,
    long EscalationCount,
    long RejectCandidateCount,
    long CandidateAttemptCount,
    long EwmaOnlyMismatchCount,
    long PersistenceConfirmationCount,
    StableDeviationDataQualityStatus DataQualityStatus,
    bool IsSuspended,
    bool ReturnedToActive,
    DateTime? ResolutionAt,
    string? ClosureReason,
    bool ExecutionBoundary,
    bool IsOpen,
    IReadOnlyList<IncidentMilestone> Milestones,
    long LastProcessedSequence,
    string LastAppliedEventId);

public sealed record IncidentReadModelQuery(
    bool? IsOpen = null,
    string? Tag = null,
    string? Lifecycle = null,
    DateTime? From = null,
    DateTime? To = null,
    bool? Escalated = null,
    bool? SuspendedDataQuality = null,
    double? MinimumDurationMinutes = null,
    bool? HasCandidateMismatch = null,
    bool LongRunningOnly = false,
    double LongRunningMinutes = 60,
    bool Descending = true);
