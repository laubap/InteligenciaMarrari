using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

namespace IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

public sealed record IncidentDetectionSnapshot(
    DateTime Timestamp,
    long MinuteIndex,
    bool FinalAnomaly,
    double Score,
    IReadOnlyList<string> AnomalousDetectors);

public sealed record IncidentTemporalConfirmationSnapshot(
    bool IsPresent,
    TemporalConfirmationStatus? Status,
    UnderlyingEvidenceKind? UnderlyingEvidenceKind,
    int ConsecutivePoints,
    double DurationMinutes,
    DateTime? SequenceStartedAt,
    bool GapInterrupted);

public sealed record StableDeviationIncidentObservation(
    int FastWindow,
    string Tag,
    string RegimeEpisodeId,
    DateTime Timestamp,
    long MinuteIndex,
    DualBaselineDecisionClassification Classification,
    IReadOnlyList<string> ClassificationReasons,
    IncidentDetectionSnapshot Detection,
    string? ActiveBaselineId,
    string? CandidateBaselineId,
    string RegimeState,
    bool CandidateBaselineReady,
    bool MadCandidateMismatch,
    bool ZCandidateMismatch,
    bool EwmaOnlyMismatch,
    bool ReturnedToActive,
    bool Promotion,
    bool OperationalBaselineChanged,
    StableDeviationDataQualityStatus DataQualityStatus,
    IncidentTemporalConfirmationSnapshot TemporalConfirmation,
    bool RegimeEpisodeActive,
    string? CandidateId = null,
    bool OutsideActive = false);

public sealed record StableDeviationIncidentState(
    string IncidentId,
    int FastWindow,
    string Tag,
    string RegimeEpisodeId,
    StableDeviationIncidentLifecycle Lifecycle,
    StableDeviationIncidentLifecycle? LifecycleBeforeSuspension,
    DateTime OpenedAt,
    long OpenedMinuteIndex,
    DateTime LastUpdatedAt,
    long LastMinuteIndex,
    double ObservedDurationMinutes,
    string? ActiveBaselineId,
    string? CandidateBaselineId,
    IncidentDetectionSnapshot FirstDetection,
    IncidentDetectionSnapshot CurrentEvidence,
    IncidentDetectionSnapshot MaximumEvidence,
    DateTime? CandidateReadyAt,
    DateTime? StableUnresolvedAt,
    long RawAnomalyRecords,
    long EvidenceUpdateCount,
    long ContinuationCount,
    long CandidateMismatchCount,
    long EscalationCount,
    IncidentDetectionSnapshot? LastMismatch,
    bool MismatchSubeventActive,
    long EwmaOnlyMismatchCount,
    long TemporalConfirmationCount,
    bool ReturnedToActive,
    bool WasPromoted,
    StableDeviationDataQualityStatus DataQualityStatus,
    StableDeviationIncidentClosureReason? ClosureReason,
    string? LastBoundaryReason,
    long CandidateRejectionCount = 0,
    string? LastRejectedCandidateId = null)
{
    public double WallClockDurationMinutes =>
        Math.Max(0, (LastUpdatedAt - OpenedAt).TotalMinutes);
}

public sealed record StableDeviationIncidentDecision(
    StableDeviationIncidentState? PreviousState,
    StableDeviationIncidentState? NextState,
    StableDeviationIncidentAction Action,
    IReadOnlyList<string> Reasons,
    bool EmitUserFacingAlert,
    StableDeviationIncidentAlertKind? AlertKind,
    bool EmitResolutionEvent);

public sealed record StableDeviationIncidentSummary(
    string IncidentId, int FastWindow, string Tag, string RegimeEpisodeId,
    DateTime OpenedAt, long OpenedMinuteIndex, DateTime LastUpdatedAt,
    long LastMinuteIndex, double WallClockDurationMinutes,
    double ObservedDurationMinutes, string? ActiveBaselineId,
    string? CandidateBaselineId, double FirstScore,
    IReadOnlyList<string> FirstDetectors, double MaximumScore,
    DateTime? CandidateReadyAt, DateTime? StableUnresolvedAt,
    long RawAnomalyRecords, long ContinuationCount,
    long CandidateMismatchCount, long EscalationCount,
    long EwmaOnlyMismatchCount, long TemporalConfirmationCount,
    bool ReturnedToActive, StableDeviationIncidentLifecycle FinalLifecycle,
    StableDeviationIncidentClosureReason? ClosureReason,
    string FinalizationReason,
    long CandidateRejectionCount = 0,
    string? LastRejectedCandidateId = null);
