using IAApi.RegimePolicy;

namespace IAApi.Benchmark.RegimeShadow;

public sealed record RegimeShadowLifecycleResult(
    CandidateEvidenceState NextCandidateState,
    string? ResetReason,
    string? TerminatedCandidateId);

public static class RegimeShadowLifecycle
{
    public static RegimePolicyDecision NormalizeDecision(
        RegimePolicyState previousPolicyState,
        RegimePolicyDecision decision,
        RegimeEvidenceSnapshot evidence,
        CandidateEvidenceState candidateState)
    {
        if (previousPolicyState.Lifecycle != RegimeLifecycleState.StableKnownRegime ||
            !candidateState.EpisodeStarted ||
            !evidence.HasReturnedToActiveRegime)
            return decision;

        return new RegimePolicyDecision(
            previousPolicyState,
            decision.NextState,
            RegimePolicyAction.RestoreActiveRegime,
            [new RegimeTransitionReason(
                RegimeTransitionReasonCode.ReturnedToActiveRegime,
                "O Shadow comprovou retorno ao baseline ativo preservado apos rejeicao do candidato.")],
            activeBaselineMustRemainFrozen: false,
            candidateWasPromoted: false,
            candidateWasRejected: false);
    }

    public static RegimeShadowLifecycleResult Apply(
        RegimePolicyState previousPolicyState,
        RegimePolicyDecision decision,
        CandidateEvidenceState adaptedCandidateState)
    {
        var reason = decision.Action switch
        {
            RegimePolicyAction.RejectCandidate => "RejectCandidate",
            RegimePolicyAction.RestoreActiveRegime => "RestoreActiveRegime",
            RegimePolicyAction.SuspendForDataQuality =>
                decision.Reasons.Any(reason =>
                    reason.Code == RegimeTransitionReasonCode.FrozenSensorDetected)
                    ? "FrozenSensor"
                    : "CriticalGap",
            _ => null
        };
        if (reason is null)
            return new(adaptedCandidateState, null, null);

        var next = decision.Action switch
        {
            RegimePolicyAction.RestoreActiveRegime => adaptedCandidateState.ResetEpisode(),
            RegimePolicyAction.RejectCandidate => adaptedCandidateState.ResetCandidate(),
            RegimePolicyAction.SuspendForDataQuality => adaptedCandidateState.ResetCandidate(),
            _ => adaptedCandidateState
        };
        return new(next, reason, previousPolicyState.CandidateRegimeId);
    }
}
