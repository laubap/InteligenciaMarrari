using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAApi.Benchmark.ExperimentalIncidents.ReadModels;

public sealed class IncidentReadModelSnapshotLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<int> LoadAsync(string path, IIncidentReadModelStore store,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream,
            cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("incidents", out var incidents) ||
            incidents.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Incident read-model snapshot has no incidents array.");

        var loaded = 0;
        foreach (var element in incidents.EnumerateArray())
        {
            var source = element.TryGetProperty("incident", out var nested)
                ? nested : element;
            var model = source.Deserialize<IncidentReadModel>(Options)
                ?? throw new InvalidDataException("Invalid incident read-model entry.");
            store.Upsert(model);
            loaded++;
        }
        return loaded;
    }
}
