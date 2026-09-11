namespace IAApi.RegimePolicy;

public enum RegimeTransitionReasonCode
{
    NoRelevantEvidence = 0,
    MaterialTransitionObserved = 1,
    LevelShiftConfirmed = 2,
    SlowDriftObserved = 3,
    CandidateFormationConfirmed = 4,
    CandidateNeedsMoreData = 5,
    CandidateIsUnstable = 6,
    CandidateIsStable = 7,
    StabilityWithoutLegitimacy = 8,
    ReturnedToActiveRegime = 9,
    KnownRegimeMatched = 10,
    NewRegimePromotionAuthorized = 11,
    PromotionBlockedBySignalKind = 12,
    PromotionBlockedBySafetyViolation = 13,
    PromotionBlockedByContradictoryEvidence = 14,
    CandidateExplicitlyInvalidated = 15,
    CriticalGapDetected = 16,
    InvalidReading = 17,
    ValidDataResumed = 18,
    PersistentDeviationObserved = 19,

    FrozenSensorDetected = 20,
    IncreasingNoiseObserved = 21
}

public sealed record RegimeTransitionReason(
    RegimeTransitionReasonCode Code,
    string Description);