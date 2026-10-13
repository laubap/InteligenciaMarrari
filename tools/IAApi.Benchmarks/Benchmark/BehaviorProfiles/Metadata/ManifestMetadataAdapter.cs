using System.Text.Json;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Benchmark.BehaviorProfiles.Metadata;

/// <summary>
/// Transporta somente tags[].analyticalKind. Nao usa nome, unidade ou outras
/// features para inferir tipo. O unico normalizador de vocabulario continua
/// sendo DeclaredTagTypeParser.
/// </summary>
public sealed class ManifestMetadataAdapter
{
    private const string FieldName = "analyticalKind";
    private readonly string _manifestPath;
    private readonly IReadOnlyDictionary<string, string?> _rawByTag;
    private readonly DeclaredTagTypeParser _parser = new();

    private ManifestMetadataAdapter(string path, IReadOnlyDictionary<string, string?> rawByTag)
    {
        _manifestPath = path;
        _rawByTag = rawByTag;
    }

    public static ManifestMetadataAdapter Load(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (document.RootElement.TryGetProperty("tags", out var tags))
        {
            foreach (var item in tags.EnumerateArray())
            {
                if (!item.TryGetProperty("tagName", out var tagNode)) continue;
                var tag = tagNode.GetString();
                if (string.IsNullOrWhiteSpace(tag)) continue;
                values[tag] = item.TryGetProperty(FieldName, out var valueNode) &&
                              valueNode.ValueKind != JsonValueKind.Null
                    ? valueNode.GetString() : null;
            }
        }
        return new ManifestMetadataAdapter(Path.GetFullPath(manifestPath), values);
    }

    public ManifestDeclaredTagMetadata Adapt(string tag)
    {
        var found = _rawByTag.TryGetValue(tag, out var raw);
        var source = "BenchmarkManifest";
        var declaration = _parser.Parse(found ? raw : null, source);
        return new ManifestDeclaredTagMetadata
        {
            Tag = tag,
            RawValue = raw,
            NormalizedValue = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToLowerInvariant(),
            Source = source,
            SourceReference = $"{_manifestPath}#tags[{tag}].{FieldName}",
            MetadataWasAbsent = !found || raw is null,
            MetadataWasInvalid = declaration.Status == DeclaredTagTypeStatus.InvalidOrUnrecognized,
            MetadataWasLegacy = declaration.Status == DeclaredTagTypeStatus.LegacyMissing,
            Declaration = declaration
        };
    }
}
