namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness;

public sealed record BehaviorProfileReadinessEvaluation(
    BehaviorProfileReadinessState NextState,
    BehaviorProfileDecisionReadiness Readiness);