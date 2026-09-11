namespace IAApi.Benchmark.BehaviorProfiles.V2.Maturity;

public sealed record BehaviorProfileMaturityEvaluation(
    BehaviorProfileTemporalState NextState,
    StableBehaviorProfileObservation Observation);
