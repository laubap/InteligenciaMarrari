using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

public sealed record BehaviorProfileV2Result
{
    public const string AlgorithmVersion =
        "BehaviorProfileInferenceV2-experimental-1";

    public TagBehaviorType Type { get; init; }

    public double Confidence { get; init; }

    public TagBehaviorType Winner { get; init; }

    public double WinnerScore { get; init; }

    public TagBehaviorType RunnerUp { get; init; }

    public double RunnerUpScore { get; init; }

    public double Margin { get; init; }

    public bool HasSufficientSamples { get; init; }

    public string Reason { get; init; } = "";

    public BehaviorProfileFeatures Features { get; init; } =
        new();

    public IReadOnlyList<BehaviorEvidence> EvidenceByType { get; init; } =
        [];
}