using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAApi.Benchmark.ExperimentalIncidents.ReadModels;

public interface IIncidentReadModelSnapshotWriter
{
    ValueTask WriteAsync(string path, string executionId,
        IReadOnlyList<IncidentReadModel> models);
}

public sealed class JsonIncidentReadModelSnapshotWriter : IIncidentReadModelSnapshotWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ValueTask WriteAsync(string path, string executionId,
        IReadOnlyList<IncidentReadModel> models)
    {
        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            executionId,
            count = models.Count,
            open = models.Count(x => x.IsOpen),
            resolved = models.Count(x => !x.IsOpen),
            incidents = models
        };
        return new(File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, Options)));
    }
}
