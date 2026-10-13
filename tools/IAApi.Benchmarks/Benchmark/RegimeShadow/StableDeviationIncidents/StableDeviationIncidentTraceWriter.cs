using System.Text.Json;

namespace IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

public sealed class StableDeviationIncidentTraceWriter : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private static readonly JsonSerializerOptions JsonOptions = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string Path { get; }
    public StableDeviationIncidentTraceWriter(string path)
    {
        Path = path;
        _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
    }
    public Task WriteAsync(object record) =>
        _writer.WriteLineAsync(JsonSerializer.Serialize(record, JsonOptions));
    public async ValueTask DisposeAsync()
    { await _writer.FlushAsync(); await _writer.DisposeAsync(); }
}
