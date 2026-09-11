namespace IAApi.RegimePolicy;

public enum RegimeLifecycleState
{
    StableKnownRegime = 0,
    TransitionSuspected = 1,
    CandidateRegime = 2,
    StableUnresolvedDeviation = 3,
    SuspendedByDataQuality = 4
}
