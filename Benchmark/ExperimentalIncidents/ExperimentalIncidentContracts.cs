using IAApi.Services.AnomalyDetection.Runtime.Incidents.StableDeviation;
using IAApi.Services.AnomalyDetection;
using System.Text.Json.Serialization;

namespace IAApi.Benchmark.ExperimentalIncidents;

public sealed record ExperimentalDetectorSnapshot(
    bool FinalAnomaly, double Score, IReadOnlyList<string> AnomalousDetectors);

public enum ExperimentalPersistenceMeaning
{
    None,
    MadTemporalConfirmation,
    LegacyDeadbandStatisticalEvidence,
    LegacyStandardDeviationFallbackEvidence,
    UninterpretedLegacyEvidence
}

public sealed record ExperimentalPersistenceSnapshot(
    bool Present,
    DetectorEvidenceRole EvidenceRole,
    UnderlyingEvidenceKind Origin,
    TemporalConfirmationStatus? Status,
    ExperimentalPersistenceMeaning Meaning,
    int ConsecutivePoints,
    double DurationMinutes,
    DateTime? StartedAt,
    bool GapInterrupted);

public sealed record ExperimentalIncidentObservation(
    string ExecutionScopeId,
    int FastWindow,
    long Sequence,
    DateTime Timestamp,
    long MinuteIndex,
    string Tag,
    string RegimeEpisodeId,
    bool RegimeEpisodeActive,
    string RegimeState,
    string? PolicyAction,
    string? CandidateId,
    string? ActiveBaselineId,
    string? CandidateBaselineId,
    bool CandidateRegimeConfirmed,
    bool CandidateRejected,
    bool CandidateMismatch,
    bool EwmaOnlyMismatch,
    bool ReturnedToActive,
    bool CriticalGap,
    bool Promotion,
    bool OperationalBaselineChanged,
    bool OutsideActive,
    bool CandidateBaselineReady,
    ExperimentalDetectorSnapshot Detection,
    ExperimentalPersistenceSnapshot Persistence);

public sealed record PendingRawAlertState(
    string CorrelationId,
    string RawAlertId,
    string RegimeEpisodeId,
    DateTime ObservedAt,
    long ObservedMinuteIndex,
    ExperimentalDetectorSnapshot FirstDetection,
    string? ActiveBaselineId,
    long LastSequence);

public sealed record ExperimentalIncidentAggregateState(
    long Version,
    long LastProcessedSequence,
    PendingRawAlertState? PendingRawAlert,
    StableDeviationIncidentState? Incident,
    long CandidateAttemptCount,
    string? LastCandidateId);

public enum ExperimentalIncidentEventKind
{
    RawAlert,
    IncidentOpened,
    IncidentUpdated,
    IncidentEscalated,
    IncidentResolved,
    IncidentExecutionBoundary
}

/// <summary>
/// Semantic facts captured at event creation time so downstream projections do
/// not have to reinterpret detector names or free-form transition reasons.
/// This snapshot is diagnostic only and never flows back to the incident engine.
/// </summary>
public sealed record ExperimentalIncidentProjectionSnapshot(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? FirstDetectionAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    long? FirstDetectionMinuteIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ExperimentalDetectorSnapshot? FirstDetection,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool CandidateBaselineReady,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool CandidateRejected,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool CandidateMismatch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool EwmaOnlyMismatch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool ReturnedToActive,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool CriticalGap,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool OutsideActive);

public sealed record ExperimentalIncidentEvent(
    string EventId,
    string CorrelationId,
    ExperimentalIncidentEventKind Kind,
    DateTime Timestamp,
    long MinuteIndex,
    long Sequence,
    string Tag,
    int FastWindow,
    string? RegimeEpisodeId,
    string? IncidentId,
    string? CandidateId,
    string? ActiveBaselineId,
    string? CandidateBaselineId,
    string? OriginatingRawAlertId,
    string Lifecycle,
    string Reason,
    ExperimentalDetectorSnapshot Detection,
    ExperimentalPersistenceSnapshot Persistence,
    ExperimentalIncidentProjectionSnapshot Projection,
    StableDeviationDataQualityStatus DataQuality,
    bool UserFacingNotificationRequested,
    bool AlreadyDeliveredByLegacy);

public sealed record ExperimentalIncidentEvaluation(
    ExperimentalIncidentAggregateState NextState,
    IReadOnlyList<ExperimentalIncidentEvent> Events,
    bool DuplicateObservation = false);
