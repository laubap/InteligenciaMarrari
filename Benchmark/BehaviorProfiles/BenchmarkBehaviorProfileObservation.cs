using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Benchmark.BehaviorProfiles.V2.Maturity;
using IAApi.Benchmark.BehaviorProfiles.V2.Structural;
using IAApi.Benchmark.BehaviorProfiles.V2.Readiness;
using IAApi.Benchmark.BehaviorProfiles.Metadata;

using BenchmarkBehaviorProfileV2Result =
    IAApi.Benchmark.BehaviorProfiles.V2.BehaviorProfileV2Result;

namespace IAApi.Benchmark.BehaviorProfiles;

public sealed record BenchmarkBehaviorProfileObservation
{
    public int FastWindowIndex { get; init; }

    public int MinuteIndex { get; init; }

    public DateTime Timestamp { get; init; }

    public string Tag { get; init; } = "";

    public double CurrentValue { get; init; }

    public string LegacyTipoTag { get; init; } = "";

    public DeclaredTagType DeclaredTagType { get; init; } = new();

    public InferredBehaviorProfile InferredBehaviorProfile { get; init; } =
        new();

    public ResolvedBehaviorProfile ResolvedBehaviorProfile { get; init; } =
        new();

    public BenchmarkBehaviorProfileV2Result InferredBehaviorProfileV2 { get; init; } =
        new();

    public StableBehaviorProfileObservation StableBehaviorProfile { get; init; } =
        new();

    public BehaviorProfileStructuralAssessment StructuralEvidence { get; init; } =
        new();

    public StructuralAmbiguityResolution StructuralAmbiguityResolution { get; init; } =
        new();

    public ManifestDeclaredTagMetadata ManifestMetadata { get; init; } =
        new();

    public BehaviorProfileDecisionReadiness DecisionReadiness { get; init; } =
        new();
}