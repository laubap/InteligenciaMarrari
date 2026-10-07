using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
namespace IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;

public static class DualBaselineDecisionClassifier
{
    public static (DualBaselineDecisionClassification Classification, string[] Reasons)
        Classify(DualBaselineTraceRecord row, bool episodeStarted)
    {
        if (row.PolicyAction == "RestoreActiveRegime")
            return (DualBaselineDecisionClassification.ResolvedOrReturnedToActive,
                ["ExplicitRestoreActiveRegime", "ReturnedToActiveOperationalBaseline"]);

        if (row.PolicyAction == "RejectCandidate" && episodeStarted)
            return (DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive,
                ["CandidateRejected", "RegimeEpisodePreserved", "ReturnToActiveNotProved"]);

        if (row.OperationalBaselineChanged || row.Promotion)
            return (DualBaselineDecisionClassification.NotEligible,
                [row.OperationalBaselineChanged ? "OperationalBaselineChanged" : "CandidatePromoted"]);

        if (row.PolicyAction == "MarkStableUnresolved" && !episodeStarted)
            return (DualBaselineDecisionClassification.StableUnresolvedDeviationStart,
                ["StableUnresolvedDeviationOpened", "FirstEvidenceMustBePreserved"]);

        var eligible = row.CandidateBaselineReady &&
                       row.RegimeState == "StableUnresolvedDeviation" &&
                       row.AgainstActive == "Abnormal" &&
                       !row.OperationalBaselineChanged && !row.Promotion;
        if (eligible)
        {
            // MAD e Z medem o fit estrutural contra B. EWMA isolado permanece
            // visivel, mas nao bloqueia globalmente devido a limitacao de fase
            // ja demonstrada na B3.5.2/3. Nenhum threshold novo e introduzido.
            var primaryMismatch = row.MadCandidate?.Vote == true || row.ZCandidate?.Vote == true;
            if (primaryMismatch)
                return (DualBaselineDecisionClassification.CandidateMismatch,
                    [row.MadCandidate?.Vote == true ? "MadAgainstCandidateAbnormal" : "ZAgainstCandidateAbnormal"]);

            return (DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation,
                row.EwmaCandidate?.Vote == true
                    ? ["MadAndZFitCandidate", "EwmaOnlyTemporalMismatch", "CandidateNotLegitimized"]
                    : ["MadAndZFitCandidate", "EwmaAlsoFitsCandidate", "CandidateNotLegitimized"]);
        }

        return row.OriginalFinalAnomaly
            ? (DualBaselineDecisionClassification.LegacyAnomaly, ["LegacyDecisionPreserved"])
            : (DualBaselineDecisionClassification.LegacyNormal, ["LegacyDecisionPreserved"]);
    }
}
