using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Maturity;

public sealed record StableBehaviorProfileObservation
{
    public BehaviorProfileMaturityLifecycle MaturityState { get; init; }
    public TagBehaviorType StableProfile { get; init; }
    public double StableConfidence { get; init; }
    public TagBehaviorType CandidateProfile { get; init; }
    public DateTime? CandidateSince { get; init; }
    public int? CandidateSinceMinuteIndex { get; init; }
    public double EvidenceDurationMinutes { get; init; }
    public int WinnerPersistence { get; init; }
    public int FlipCount { get; init; }
    public int RecentFlipCount { get; init; }
    public bool HasCounterContinuousConflict { get; init; }
    public string Reason { get; init; } = "";
}
