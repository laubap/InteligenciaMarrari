using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural;

public sealed record BehaviorProfileStructuralAssessment
{
    public const string AlgorithmVersion =
        "StructuralEvidence-shadow-1";

    public string Version { get; init; } =
        AlgorithmVersion;

    public TagBehaviorType RawWinner { get; init; }

    public StructuralEvidenceStatus RawWinnerStatus { get; init; }

    public bool WouldMatureUnderCurrentPolicy { get; init; }

    public bool WouldBeBlockedByStructuralGate { get; init; }

    public IReadOnlyList<ClassEvidenceAssessment> Classes { get; init; } =
        [];
}