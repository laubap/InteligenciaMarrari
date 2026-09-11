namespace IAApi.RegimePolicy;

public enum OccurrenceState
{
    None = 0,
    TransientDeviation = 1,
    TransitionInProgress = 2,
    PersistentDeviation = 3,
    StableUnresolvedDeviation = 4,
    ConfirmedRegimeTransition = 5,
    DataQualityIncident = 6
}
