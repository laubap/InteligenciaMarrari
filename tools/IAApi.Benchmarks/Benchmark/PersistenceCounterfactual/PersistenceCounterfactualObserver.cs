using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.PersistenceCounterfactual;

public sealed class PersistenceCounterfactualObserver : IAsyncDisposable
{
    private readonly PersistenceCounterfactualCollector _collector;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private bool _disabled;

    public PersistenceCounterfactualObserver(string directory, string executionId)
    {
        _collector = new PersistenceCounterfactualCollector();
        TracePath = Path.Combine(directory, $"persistence_counterfactual_{executionId}.jsonl");
        SummaryPath = Path.Combine(directory, $"persistence_counterfactual_summary_{executionId}.json");
    }

    public string TracePath { get; }
    public string SummaryPath { get; }
    public void ResetWindow() => _collector.ResetWindow();

    public void Observe(int window, int minute, string tag, DetectorResult result)
    {
        if (_disabled) return;
        try { _collector.Add(window, minute, tag, result); }
        catch (Exception exception)
        {
            _disabled = true;
            Console.Error.WriteLine("Contrafactual Persistence desativado: " + exception.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disabled) return;
        try
        {
            await using var writer = new StreamWriter(TracePath, false, new UTF8Encoding(false));
            foreach (var record in _collector.Records)
                await writer.WriteLineAsync(JsonSerializer.Serialize(record, _json));
            await File.WriteAllTextAsync(SummaryPath,
                JsonSerializer.Serialize(_collector.BuildSummary(),
                    new JsonSerializerOptions(_json) { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Falha ao finalizar contrafactual Persistence: " + exception.Message);
        }
    }
}
