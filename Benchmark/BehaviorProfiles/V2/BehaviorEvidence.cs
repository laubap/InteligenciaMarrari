using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.BehaviorProfiles.V2;

public sealed record BehaviorEvidence
{
    public TagBehaviorType Type { get; init; }
    public double Score { get; init; }
    public bool HasSufficientSamples { get; init; }
    public IReadOnlyList<string> FavorableFeatures { get; init; } = [];
    public IReadOnlyList<string> ContraryFeatures { get; init; } = [];
    public string Reason { get; init; } = "";
}
