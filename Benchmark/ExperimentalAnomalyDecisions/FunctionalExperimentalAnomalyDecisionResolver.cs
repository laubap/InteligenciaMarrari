namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

public sealed class FunctionalExperimentalAnomalyDecisionResolver
{
    public FunctionalExperimentalAnomalyDecision Resolve(
        bool legacyFinalAnomaly,
        ExperimentalAnomalyDecision? experimentalDecision)
    {
        // Fail-safe:
        // se não existe decisão experimental disponível,
        // preserva exatamente o comportamento Legacy.
        if (experimentalDecision is null)
        {
            return new FunctionalExperimentalAnomalyDecision(
                LegacyFinalAnomaly: legacyFinalAnomaly,
                FunctionalFinalAnomaly: legacyFinalAnomaly,
                CountsAsNewEvidence: legacyFinalAnomaly,
                SourceDecision: legacyFinalAnomaly
                    ? ExperimentalAnomalyDecisionKind.NewAnomaly
                    : ExperimentalAnomalyDecisionKind.LegacyNormal,
                UsedLegacyFallback: true,
                Reasons: new[]
                {
                    "ExperimentalDecisionUnavailable",
                    "LegacyFallback"
                });
        }

        return experimentalDecision.Kind switch
        {
            ExperimentalAnomalyDecisionKind.LegacyNormal =>
                new FunctionalExperimentalAnomalyDecision(
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    FunctionalFinalAnomaly: false,
                    CountsAsNewEvidence: false,
                    SourceDecision: experimentalDecision.Kind,
                    UsedLegacyFallback: false,
                    Reasons: experimentalDecision.Reasons),

            ExperimentalAnomalyDecisionKind.NewAnomaly =>
                new FunctionalExperimentalAnomalyDecision(
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    FunctionalFinalAnomaly: true,
                    CountsAsNewEvidence: true,
                    SourceDecision: experimentalDecision.Kind,
                    UsedLegacyFallback: false,
                    Reasons: experimentalDecision.Reasons),

            ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation =>
                new FunctionalExperimentalAnomalyDecision(
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    FunctionalFinalAnomaly: false,
                    CountsAsNewEvidence: false,
                    SourceDecision: experimentalDecision.Kind,
                    UsedLegacyFallback: false,
                    Reasons: experimentalDecision.Reasons),

            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation =>
                new FunctionalExperimentalAnomalyDecision(
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    FunctionalFinalAnomaly: true,
                    CountsAsNewEvidence: false,
                    SourceDecision: experimentalDecision.Kind,
                    UsedLegacyFallback: false,
                    Reasons: experimentalDecision.Reasons),
            ExperimentalAnomalyDecisionKind.CandidateMismatch =>
                new FunctionalExperimentalAnomalyDecision(
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    FunctionalFinalAnomaly: false,
                    CountsAsNewEvidence: true,
                    SourceDecision: experimentalDecision.Kind,
                    UsedLegacyFallback: false,
                    Reasons: experimentalDecision.Reasons),

            ExperimentalAnomalyDecisionKind.DataQualityUncertain =>
                new FunctionalExperimentalAnomalyDecision(
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    FunctionalFinalAnomaly:
                        experimentalDecision.Reasons.Any(reason =>
                            string.Equals(
                                reason,
                                "FrozenSensorDetected",
                                StringComparison.Ordinal))
                            ? experimentalDecision.CountsAsNewAnomaly
                            : legacyFinalAnomaly,
                    CountsAsNewEvidence:
                        experimentalDecision.Reasons.Any(reason =>
                            string.Equals(
                                reason,
                                "FrozenSensorDetected",
                                StringComparison.Ordinal))
                            ? experimentalDecision.CountsAsNewEvidence
                            : legacyFinalAnomaly,
                    SourceDecision: experimentalDecision.Kind,
                    UsedLegacyFallback: false,
                    Reasons: experimentalDecision.Reasons),


            _ =>
                new FunctionalExperimentalAnomalyDecision(
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    FunctionalFinalAnomaly: legacyFinalAnomaly,
                    CountsAsNewEvidence: legacyFinalAnomaly,
                    SourceDecision: experimentalDecision.Kind,
                    UsedLegacyFallback: true,
                    Reasons: new[]
                    {
                        "UnknownExperimentalDecision",
                        "LegacyFallback"
                    })
        };
    }
}