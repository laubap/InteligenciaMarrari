namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity;

public sealed record BehaviorProfileMaturityEvaluation(
    BehaviorProfileTemporalState NextState,
    StableBehaviorProfileObservation Observation);