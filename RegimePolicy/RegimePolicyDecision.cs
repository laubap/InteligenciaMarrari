namespace IAApi.RegimePolicy;

public sealed record RegimePolicyDecision
{
    public RegimePolicyDecision(
        RegimePolicyState previousState,
        RegimePolicyState nextState,
        RegimePolicyAction action,
        IEnumerable<RegimeTransitionReason> reasons,
        bool activeBaselineMustRemainFrozen,
        bool candidateWasPromoted = false,
        bool candidateWasRejected = false)
    {
        PreviousState = previousState;
        NextState = nextState;
        Action = action;
        Reasons = Array.AsReadOnly(reasons.ToArray());
        ActiveBaselineMustRemainFrozen = activeBaselineMustRemainFrozen;
        CandidateWasPromoted = candidateWasPromoted;
        CandidateWasRejected = candidateWasRejected;
    }

    public RegimePolicyState PreviousState { get; }
    public RegimePolicyState NextState { get; }
    public RegimePolicyAction Action { get; }
    public IReadOnlyList<RegimeTransitionReason> Reasons { get; }
    public bool ActiveBaselineMustRemainFrozen { get; }
    public bool CandidateWasPromoted { get; }
    public bool CandidateWasRejected { get; }
}
