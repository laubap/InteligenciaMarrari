using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness;

public sealed record BehaviorProfileDecisionReadiness
{
    public BehaviorProfileReadinessStatus Status { get; init; }

    public TagBehaviorType ProspectiveProfile { get; init; } =
        TagBehaviorType.Unknown;

    public string Reason { get; init; } = "";

    public bool CurrentMaturityIsStable { get; init; }

    public int ResolutionChangeCount { get; init; }

    public int ResolvedToAmbiguousCount { get; init; }

    public int AmbiguousToResolvedCount { get; init; }

    public int ResolvedClassChangeCount { get; init; }

    public DateTime? LastResolutionChangeTimestamp { get; init; }

    public int? LastResolutionChangeMinuteIndex { get; init; }

    public double MinutesSinceLastResolutionChange { get; init; }

    public bool ResolutionChangedThisObservation { get; init; }

    public bool StableAfterReady { get; init; }
}