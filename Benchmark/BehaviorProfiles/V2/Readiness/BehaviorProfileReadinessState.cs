using IAApi.Benchmark.BehaviorProfiles.V2.Structural;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Readiness;

public sealed record BehaviorProfileReadinessState
{
    public StructuralResolutionStatus? PreviousResolutionStatus { get; init; }
    public TagBehaviorType PreviousPreferredClass { get; init; }
    public BehaviorProfileReadinessStatus? PreviousReadinessStatus { get; init; }
    public int ResolutionChangeCount { get; init; }
    public int ResolvedToAmbiguousCount { get; init; }
    public int AmbiguousToResolvedCount { get; init; }
    public int ResolvedClassChangeCount { get; init; }
    public DateTime? LastResolutionChangeTimestamp { get; init; }
    public int? LastResolutionChangeMinuteIndex { get; init; }
}
