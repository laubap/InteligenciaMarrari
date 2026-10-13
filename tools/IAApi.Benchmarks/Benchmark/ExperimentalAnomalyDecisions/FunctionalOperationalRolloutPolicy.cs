namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

/// <summary>
/// B3.17 — política centralizada de rollout operacional.
///
/// Objetivo:
/// decidir se a leitura pode usar a decisão funcional
/// ou se deve voltar conservadoramente para o Legacy.
///
/// Também recebe o resultado do gate canário. Fora do escopo,
/// o rollback para Legacy acontece antes de qualquer decisão funcional.
///
/// A política não altera DetectorResult nem baseline.
/// </summary>
public sealed class FunctionalOperationalRolloutPolicy
{
    public FunctionalOperationalRolloutDecision Resolve(
        bool legacyFinalAnomaly,
        FunctionalOperationalAnomalyDecision? operationalDecision,
        bool canaryInScope = true)
    {
        /*
         * Canary fail-safe:
         *
         * quando o canário está habilitado e esta leitura ficou fora
         * do escopo permitido, a saída permanece integralmente Legacy.
         */
        if (!canaryInScope)
        {
            return LegacyFallback(
                legacyFinalAnomaly,
                operationalDecision?.SourceDecision,
                "CanaryOutOfScope");
        }

        /*
         * Ausência de decisão funcional:
         * rollback imediato para Legacy.
         */
        if (operationalDecision is null)
        {
            return LegacyFallback(
                legacyFinalAnomaly,
                sourceDecision: null,
                "OperationalDecisionUnavailable");
        }

        /*
         * Se qualquer camada anterior já precisou usar LegacyFallback,
         * mantemos o rollback até a saída.
         */
        if (operationalDecision.UsedLegacyFallback)
        {
            return LegacyFallback(
                legacyFinalAnomaly,
                operationalDecision.SourceDecision,
                "UpstreamLegacyFallback");
        }

        /*
         * DataQualityUncertain:
         * não usamos redução funcional para decisão de emissão.
         * Preservamos o Legacy conservadoramente.
         *
         * O canal de nova evidência também acompanha o Legacy neste modo.
         */
        if (operationalDecision.SourceDecision ==
            ExperimentalAnomalyDecisionKind.DataQualityUncertain)
        {
            var frozenSensorDataQuality =
                operationalDecision.Reasons.Any(reason =>
                    string.Equals(
                        reason,
                        "FrozenSensorDetected",
                        StringComparison.Ordinal));

            if (frozenSensorDataQuality)
            {
                return new FunctionalOperationalRolloutDecision(
                    Mode: FunctionalOperationalRolloutMode.UseFunctional,
                    EmitAnomaly: operationalDecision.EmitAnomaly,
                    EmitNewEvidence: operationalDecision.EmitNewEvidence,
                    LegacyFinalAnomaly: legacyFinalAnomaly,
                    SourceDecision: operationalDecision.SourceDecision,
                    Reasons: operationalDecision.Reasons
                        .Concat(["FrozenSensorFunctionalRolloutAccepted"])
                        .ToArray());
            }

            return LegacyFallback(
                legacyFinalAnomaly,
                operationalDecision.SourceDecision,
                "DataQualityUncertain");
        }

        /*
         * Decisões conhecidas e seguras para rollout funcional.
         */
        if (operationalDecision.SourceDecision is
            ExperimentalAnomalyDecisionKind.LegacyNormal or
            ExperimentalAnomalyDecisionKind.NewAnomaly or
            ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation or
            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation or
            ExperimentalAnomalyDecisionKind.CandidateMismatch)
        {
            return new FunctionalOperationalRolloutDecision(
                Mode: FunctionalOperationalRolloutMode.UseFunctional,
                EmitAnomaly: operationalDecision.EmitAnomaly,
                EmitNewEvidence: operationalDecision.EmitNewEvidence,
                LegacyFinalAnomaly: legacyFinalAnomaly,
                SourceDecision: operationalDecision.SourceDecision,
                Reasons: operationalDecision.Reasons
                    .Concat(["FunctionalRolloutAccepted"])
                    .ToArray());
        }

        /*
         * Fail-safe para qualquer enum/estado futuro não tratado.
         */
        return LegacyFallback(
            legacyFinalAnomaly,
            operationalDecision.SourceDecision,
            "UnknownOperationalDecision");
    }

    private static FunctionalOperationalRolloutDecision LegacyFallback(
        bool legacyFinalAnomaly,
        ExperimentalAnomalyDecisionKind? sourceDecision,
        params string[] reasons)
    {
        return new FunctionalOperationalRolloutDecision(
            Mode: FunctionalOperationalRolloutMode.UseLegacyFallback,
            EmitAnomaly: legacyFinalAnomaly,
            EmitNewEvidence: legacyFinalAnomaly,
            LegacyFinalAnomaly: legacyFinalAnomaly,
            SourceDecision: sourceDecision,
            Reasons: reasons
                .Concat(["LegacyRollback"])
                .ToArray());
    }
}