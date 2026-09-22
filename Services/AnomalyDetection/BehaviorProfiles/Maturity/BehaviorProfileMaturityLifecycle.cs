namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity;

public enum BehaviorProfileMaturityLifecycle
{
    InsufficientEvidence = 0,
    Candidate = 1,
    Stable = 2,
    Conflicted = 3
}