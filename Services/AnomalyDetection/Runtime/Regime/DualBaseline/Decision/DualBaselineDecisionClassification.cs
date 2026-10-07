namespace IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

public enum DualBaselineDecisionClassification
{
    LegacyNormal,
    LegacyAnomaly,
    StableUnresolvedDeviationStart,
    StableUnresolvedDeviationContinuation,
    CandidateMismatch,
    ResolvedOrReturnedToActive,
    NotEligible,
    CandidateRejectedStillOutsideActive
}