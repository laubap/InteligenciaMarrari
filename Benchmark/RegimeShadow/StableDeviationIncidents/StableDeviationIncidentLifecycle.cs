namespace IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

public enum StableDeviationIncidentLifecycle
{
    Open,
    StableUnresolved,
    Escalated,
    SuspendedDataQuality,
    Resolved,
    ClosedByRegimePromotion
}

public enum StableDeviationIncidentAction
{
    None,
    OpenIncident,
    UpdateEvidence,
    MarkStableUnresolved,
    RecordContinuation,
    EscalateCandidateMismatch,
    SuspendForDataQuality,
    ResumeAfterDataQuality,
    ResolveReturnedToActive,
    CloseByRegimePromotion,
    ExecutionBoundary,
    RecordCandidateRejected
}

public enum StableDeviationIncidentAlertKind { Opening, Escalation }
public enum StableDeviationIncidentClosureReason
{
    ReturnedToActiveBaseline,
    RegimePromotion
}

public enum StableDeviationDataQualityStatus { Valid, CriticalGap }
