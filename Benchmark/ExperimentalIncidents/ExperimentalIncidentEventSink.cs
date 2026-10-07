using System.Text.Json;
using IAApi.Services.AnomalyDetection.Runtime.Incidents.Experimental;

namespace IAApi.Benchmark.ExperimentalIncidents;

public interface IExperimentalIncidentEventSink : IAsyncDisposable
{
    ValueTask WriteAsync(ExperimentalIncidentEvent incidentEvent);
}

public sealed class JsonlExperimentalIncidentEventSink : IExperimentalIncidentEventSink
{
    private readonly StreamWriter _writer;
    private static readonly JsonSerializerOptions JsonOptions = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string Path { get; }

    public JsonlExperimentalIncidentEventSink(string path)
    {
        Path = path;
        _writer = new StreamWriter(new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.Read));
    }

    public ValueTask WriteAsync(ExperimentalIncidentEvent incidentEvent) =>
        new(_writer.WriteLineAsync(JsonSerializer.Serialize(incidentEvent, JsonOptions)));

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }
}
