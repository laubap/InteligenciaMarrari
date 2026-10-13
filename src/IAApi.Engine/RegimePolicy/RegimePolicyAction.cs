namespace IAApi.RegimePolicy;

public enum RegimePolicyAction
{
    None = 0,
    ObserveTransition = 1,
    OpenCandidate = 2,
    ContinueCandidate = 3,
    MarkStableUnresolved = 4,
    RejectCandidate = 5,
    RestoreActiveRegime = 6,
    ActivateKnownRegime = 7,
    ConfirmNewRegime = 8,
    SuspendForDataQuality = 9,
    ResumeAfterDataQuality = 10
}
