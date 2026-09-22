using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity;

public sealed record BehaviorProfileTemporalState
{
    public BehaviorProfileMaturityLifecycle Lifecycle { get; init; } =
        BehaviorProfileMaturityLifecycle.InsufficientEvidence;

    public TagBehaviorType StableProfile { get; init; } =
        TagBehaviorType.Unknown;

    public double StableConfidence { get; init; }

    public TagBehaviorType CandidateProfile { get; init; } =
        TagBehaviorType.Unknown;

    public DateTime? CandidateSince { get; init; }

    public int? CandidateSinceMinuteIndex { get; init; }

    public int CandidateObservationCount { get; init; }

    public TagBehaviorType LastRawProfile { get; init; } =
        TagBehaviorType.Unknown;

    public int TotalRawFlipCount { get; init; }

    public IReadOnlyList<TagBehaviorType> RecentRawWinners { get; init; } =
        [];

    public static BehaviorProfileTemporalState Initial { get; } =
        new();
}