namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.18 — produz o candidato funcional do runtime real.
///
/// Regra de segurança:
/// contexto funcional ausente/incompleto NUNCA reduz uma anomalia Legacy.
///
/// B3.20.11 — recuperação conservadora de falso negativo Legacy:
/// quando o Legacy ficou abaixo do threshold, mas Z-Score + MAD + Taxa de
/// Variação apresentam evidência extrema e concordante, o candidato runtime
/// pode promover o evento para NewAnomaly. O DetectorResult Legacy permanece
/// imutável.
/// </summary>
public sealed class RuntimeFunctionalDecisionCandidateFactory
{
    private const double MinimumLegacyScoreForRecovery = 50.0;
    private const double MinimumStrongDetectorScore = 90.0;

public RuntimeFunctionalDecisionCandidate Create(
    bool legacyFinalAnomaly,
    RuntimeFunctionalDecisionContext? context)
{

        if (context?.FrozenSensor == true)
        {
            return Trace(
                legacyFinalAnomaly,
                context,
                new RuntimeFunctionalDecisionCandidate(
                    EmitAnomaly: true,
                    EmitNewEvidence: true,
                    Source:
                        RuntimeFunctionalDecisionSources
                            .DataQualityUncertain,
                    Reasons:
                    [
                        "FrozenSensorDetected",
                        "IndependentDataQualityEvidence"
                    ]));
        }
        if (!legacyFinalAnomaly &&
            context is not null &&
            IsStrongLegacyFalseNegative(context))
        {
            return Trace(
                legacyFinalAnomaly,
                context,
                new RuntimeFunctionalDecisionCandidate(
                    EmitAnomaly: true,
                    EmitNewEvidence: true,
                    Source:
                        RuntimeFunctionalDecisionSources.NewAnomaly,
                    Reasons:
                    [
                        "LegacyFalseNegativeRecovered",
                        "LegacyScoreNearEnsembleThreshold",
                        "StrongZActiveEvidence",
                        "StrongMadActiveEvidence",
                        "StrongRateOfChangeEvidence"
                    ]));
        }

        if (!legacyFinalAnomaly &&
            context?.IncreasingNoise == true &&
            context.IncreasingNoiseStarted &&
            !context.CriticalGap)
        {
            return Trace(
                legacyFinalAnomaly,
                context,
                new RuntimeFunctionalDecisionCandidate(
                    EmitAnomaly: true,
                    EmitNewEvidence: true,
                    Source:
                        RuntimeFunctionalDecisionSources.NewAnomaly,
                    Reasons:
                    [
                        "IncreasingNoiseDetected",
                        "IndependentVarianceExpansionEvidence"
                    ]));
        }

        if (!legacyFinalAnomaly &&
            context?.IncreasingNoise == true &&
            context.IncreasingNoiseContinuation &&
            !context.CriticalGap)
        {
            return Trace(
                legacyFinalAnomaly,
                context,
                new RuntimeFunctionalDecisionCandidate(
                    EmitAnomaly: true,
                    EmitNewEvidence: false,
                    Source:
                        "IncreasingNoiseContinuation",
                    Reasons:
                    [
                        "IncreasingNoiseStillActive"
                    ]));
        }
if (!legacyFinalAnomaly)
{
    return Trace(
        legacyFinalAnomaly,
        context,
        new RuntimeFunctionalDecisionCandidate(
            EmitAnomaly: false,
            EmitNewEvidence: false,
            Source:
                RuntimeFunctionalDecisionSources.LegacyNormal,
            Reasons:
                ["LegacyFinalAnomalyFalse"]));
}

        if (context is null)
        {
            return Trace(
                legacyFinalAnomaly,
                context,
                PreserveAsNewAnomaly(
                    "RuntimeFunctionalContextUnavailable"));
        }

        if (context.CriticalGap)
        {
            return Trace(
                legacyFinalAnomaly,
                context,
                new RuntimeFunctionalDecisionCandidate(
                    EmitAnomaly: true,
                    EmitNewEvidence: true,
                    Source:
                        RuntimeFunctionalDecisionSources
                            .DataQualityUncertain,
                    Reasons:
                    [
                        "CriticalDataQualityGap",
                        "LegacyDecisionPreservedConservatively"
                    ]));
        }

        if (context.MadCandidateVote ||
            context.ZCandidateVote)
        {
            var reasons =
                new List<string>
                {
                    "CandidateBaselineMismatch"
                };

            if (context.MadCandidateVote)
                reasons.Add("MadAgainstCandidateAbnormal");

            if (context.ZCandidateVote)
                reasons.Add("ZAgainstCandidateAbnormal");

            return Trace(
                legacyFinalAnomaly,
                context,
                new RuntimeFunctionalDecisionCandidate(
                    EmitAnomaly: false,
                    EmitNewEvidence: true,
                    Source:
                        RuntimeFunctionalDecisionSources
                            .CandidateMismatch,
                    Reasons:
                        reasons));
        }

        var incidentOpen =
            !string.IsNullOrWhiteSpace(
                context.IncidentId);

        var stableUnresolved =
            string.Equals(
                context.RegimeState,
                "StableUnresolvedDeviation",
                StringComparison.OrdinalIgnoreCase);

        var activeStillAbnormal =
            string.Equals(
                context.AgainstActive,
                "Abnormal",
                StringComparison.OrdinalIgnoreCase);

        var safeForContinuation =
            incidentOpen &&
            context.CandidateBaselineReady &&
            stableUnresolved &&
            activeStillAbnormal &&
            !context.MadCandidateVote &&
            !context.ZCandidateVote &&
            !context.CriticalGap &&
            !context.OperationalBaselineChanged &&
            !context.Promotion;

        if (safeForContinuation)
        {
            var reasons =
                new List<string>
                {
                    "IncidentAlreadyOpen",
                    "CandidateBaselineReady",
                    "StableUnresolvedDeviation",
                    "ActiveBaselineStillAbnormal",
                    "MadFitsCandidate",
                    "ZFitsCandidate",
                    "OperationalBaselineUnchanged",
                    "CandidateNotPromoted"
                };

            if (context.EwmaCandidateVote)
                reasons.Add("EwmaOnlyTemporalMismatch");

            return Trace(
                legacyFinalAnomaly,
                context,
                new RuntimeFunctionalDecisionCandidate(
                    EmitAnomaly: false,
                    EmitNewEvidence: false,
                    Source:
                        RuntimeFunctionalDecisionSources
                            .StableUnresolvedContinuation,
                    Reasons:
                        reasons));
        }

        var fallbackReasons =
            new List<string>
            {
                "LegacyFinalAnomalyPreserved"
            };

        if (!incidentOpen)
            fallbackReasons.Add("NoOpenIncident");

        if (!context.CandidateBaselineReady)
            fallbackReasons.Add("CandidateNotReady");

        if (!stableUnresolved)
            fallbackReasons.Add("RegimeNotStableUnresolved");

        if (!activeStillAbnormal)
            fallbackReasons.Add("ActiveBaselineNotAbnormal");

        if (context.OperationalBaselineChanged)
            fallbackReasons.Add("OperationalBaselineChanged");

        if (context.Promotion)
            fallbackReasons.Add("CandidatePromoted");

        return Trace(
             legacyFinalAnomaly,
            context,
            new RuntimeFunctionalDecisionCandidate(
                EmitAnomaly: true,
                EmitNewEvidence: true,
                Source:
                    RuntimeFunctionalDecisionSources.NewAnomaly,
                Reasons:
                    fallbackReasons));
    }

    private static bool IsStrongLegacyFalseNegative(
        RuntimeFunctionalDecisionContext context)
    {
        return
            !context.CriticalGap &&
            double.IsFinite(context.LegacyScoreNormalized) &&
            context.LegacyScoreNormalized >=
                MinimumLegacyScoreForRecovery &&

            context.ZActiveVote &&
            double.IsFinite(context.ZActiveScore) &&
            context.ZActiveScore >=
                MinimumStrongDetectorScore &&

            context.MadActiveVote &&
            double.IsFinite(context.MadActiveScore) &&
            context.MadActiveScore >=
                MinimumStrongDetectorScore &&

            context.RateActiveVote &&
            double.IsFinite(context.RateActiveScore) &&
            context.RateActiveScore >=
                MinimumStrongDetectorScore;
    }

private static RuntimeFunctionalDecisionCandidate Trace(
    bool legacyFinalAnomaly,
    RuntimeFunctionalDecisionContext? context,
    RuntimeFunctionalDecisionCandidate candidate)
    {
        return candidate;
    }

    private static RuntimeFunctionalDecisionCandidate
        PreserveAsNewAnomaly(
            string reason)
    {
        return new RuntimeFunctionalDecisionCandidate(
            EmitAnomaly: true,
            EmitNewEvidence: true,
            Source:
                RuntimeFunctionalDecisionSources.NewAnomaly,
            Reasons:
            [
                "LegacyFinalAnomalyPreserved",
                reason
            ]);
    }
}


