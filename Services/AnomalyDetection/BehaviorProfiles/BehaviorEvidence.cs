using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

public sealed record BehaviorEvidence
{
    public TagBehaviorType Type { get; init; }

    public double Score { get; init; }

    public bool HasSufficientSamples { get; init; }

    public IReadOnlyList<string> FavorableFeatures { get; init; } = [];

    public IReadOnlyList<string> ContraryFeatures { get; init; } = [];

    public string Reason { get; init; } = "";
}