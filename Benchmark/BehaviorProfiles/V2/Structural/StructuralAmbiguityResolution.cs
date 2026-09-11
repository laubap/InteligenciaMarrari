using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Structural;

public sealed record StructuralAmbiguityResolution
{
    public const string AlgorithmVersion = "StructuralAmbiguity-shadow-1";
    public string Version { get; init; } = AlgorithmVersion;
    public IReadOnlyList<TagBehaviorType> ValidClasses { get; init; } = [];
    public TagBehaviorType PreferredClass { get; init; } = TagBehaviorType.Unknown;
    public StructuralResolutionStatus ResolutionStatus { get; init; }
    public IReadOnlyList<string> DiscriminatingEvidence { get; init; } = [];
    public IReadOnlyList<string> RemainingAmbiguities { get; init; } = [];
    public DeclaredTypeStructuralContext DeclaredTypeContext { get; init; } = new();
    public ObservedStructureKind ObservedStructure { get; init; }
    public string Reason { get; init; } = "";
}
