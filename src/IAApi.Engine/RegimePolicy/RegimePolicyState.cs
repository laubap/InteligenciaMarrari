namespace IAApi.RegimePolicy;

public sealed record RegimePolicyState(
    RegimeLifecycleState Lifecycle,
    OccurrenceState Occurrence,
    string ActiveRegimeId,
    string? CandidateRegimeId = null,
    DateTimeOffset? TransitionStartedAt = null,
    DateTimeOffset? CandidateStartedAt = null,
    long? TransitionStartedSequence = null,
    long? CandidateStartedSequence = null,
    int ConsecutiveCandidateObservations = 0,
    RegimeLifecycleState? LifecycleBeforeSuspension = null)
{
    public static RegimePolicyState Initial(string activeRegimeId) =>
        new(
            RegimeLifecycleState.StableKnownRegime,
            OccurrenceState.None,
            NormalizeRequired(activeRegimeId));

    private static string NormalizeRequired(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
