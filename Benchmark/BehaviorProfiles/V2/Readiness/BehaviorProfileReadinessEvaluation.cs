namespace IAApi.Benchmark.BehaviorProfiles.V2.Readiness;

public sealed record BehaviorProfileReadinessEvaluation(
    BehaviorProfileReadinessState NextState,
    BehaviorProfileDecisionReadiness Readiness);
