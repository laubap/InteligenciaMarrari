using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural;

public sealed record DeclaredTypeStructuralContext
{
    public TagBehaviorType Type { get; init; }

    public DeclaredTagTypeStatus Status { get; init; }

    public string? RawValue { get; init; }

    public string Source { get; init; } = "";

    public DeclaredTypeObservationRelation Relation { get; init; }

    public string Reason { get; init; } = "";
}