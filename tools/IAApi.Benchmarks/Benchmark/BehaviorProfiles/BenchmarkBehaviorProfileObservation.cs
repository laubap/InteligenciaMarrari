using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Benchmark.BehaviorProfiles.Metadata;

using ServiceBehaviorProfileV2Result =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.BehaviorProfileV2Result;

using ServiceStableBehaviorProfileObservation =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity.StableBehaviorProfileObservation;

using ServiceBehaviorProfileStructuralAssessment =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural.BehaviorProfileStructuralAssessment;

using ServiceStructuralAmbiguityResolution =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural.StructuralAmbiguityResolution;

using ServiceBehaviorProfileDecisionReadiness =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness.BehaviorProfileDecisionReadiness;

namespace IAApi.Benchmark.BehaviorProfiles;

public sealed record BenchmarkBehaviorProfileObservation
{
    public int FastWindowIndex { get; init; }

    public int MinuteIndex { get; init; }

    public DateTime Timestamp { get; init; }

    public string Tag { get; init; } = "";

    public double CurrentValue { get; init; }

    public string LegacyTipoTag { get; init; } = "";

    public DeclaredTagType DeclaredTagType { get; init; } =
        new();

    public InferredBehaviorProfile InferredBehaviorProfile { get; init; } =
        new();

    public ResolvedBehaviorProfile ResolvedBehaviorProfile { get; init; } =
        new();

    public ServiceBehaviorProfileV2Result InferredBehaviorProfileV2 { get; init; } =
        new();

    public ServiceStableBehaviorProfileObservation StableBehaviorProfile { get; init; } =
        new();

    public ServiceBehaviorProfileStructuralAssessment StructuralEvidence { get; init; } =
        new();

    public ServiceStructuralAmbiguityResolution StructuralAmbiguityResolution { get; init; } =
        new();

    public ManifestDeclaredTagMetadata ManifestMetadata { get; init; } =
        new();

    public ServiceBehaviorProfileDecisionReadiness DecisionReadiness { get; init; } =
        new();
}