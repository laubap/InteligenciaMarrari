using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Benchmark.BehaviorProfiles.Metadata;

public sealed record ManifestDeclaredTagMetadata
{
    public string Tag { get; init; } = "";
    public string? RawValue { get; init; }
    public string? NormalizedValue { get; init; }
    public string Source { get; init; } = "";
    public string SourceReference { get; init; } = "";
    public bool MetadataWasAbsent { get; init; }
    public bool MetadataWasInvalid { get; init; }
    public bool MetadataWasLegacy { get; init; }
    public DeclaredTagType Declaration { get; init; } = new();
}
