namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

public enum FunctionalOperationalRolloutMode
{
    UseFunctional,
    UseLegacyFallback
}

public sealed record FunctionalOperationalRolloutDecision(
    FunctionalOperationalRolloutMode Mode,
    bool EmitAnomaly,
    bool EmitNewEvidence,
    bool LegacyFinalAnomaly,
    ExperimentalAnomalyDecisionKind? SourceDecision,
    IReadOnlyList<string> Reasons);