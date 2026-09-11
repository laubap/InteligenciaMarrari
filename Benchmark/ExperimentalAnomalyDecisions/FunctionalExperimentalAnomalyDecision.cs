namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

public sealed record FunctionalExperimentalAnomalyDecision(
    bool LegacyFinalAnomaly,
    bool FunctionalFinalAnomaly,
    bool CountsAsNewEvidence,
    ExperimentalAnomalyDecisionKind SourceDecision,
    bool UsedLegacyFallback,
    IReadOnlyList<string> Reasons);