using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.Benchmark.BehaviorProfiles;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.DetectorApplicabilityCounterfactual;

/// <summary>Writer benchmark-only, fail-safe e estritamente observacional.</summary>
public sealed class DetectorApplicabilityCounterfactualObserver : IAsyncDisposable
{
    private readonly DetectorApplicabilityCounterfactualCollector _collector = new();
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };
    private bool _disabled;

    public DetectorApplicabilityCounterfactualObserver(string resultDirectory, string executionId)
    {
        TracePath = Path.Combine(resultDirectory, $"detector_applicability_counterfactual_{executionId}.jsonl");
        SummaryPath = Path.Combine(resultDirectory, $"detector_applicability_counterfactual_summary_{executionId}.json");
    }

    public string TracePath { get; }
    public string SummaryPath { get; }

    public void Observe(BenchmarkBehaviorProfileObservation observation)
    {
        if (_disabled) return;
        try { _collector.Add(observation); }
        catch (Exception exception)
        {
            _disabled = true;
            Console.Error.WriteLine("Contrafactual de aplicabilidade desativado: " + exception.Message);
        }
    }

    public void RecordOriginalDecision(string tag, int minuteIndex, DetectorResult result)
    {
        if (_disabled) return;
        try { _collector.RecordOriginalDecision(tag, minuteIndex, result); }
        catch (Exception exception)
        {
            _disabled = true;
            Console.Error.WriteLine("Contrafactual de aplicabilidade desativado: " + exception.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disabled) return;
        try
        {
            await using var writer = new StreamWriter(TracePath, false, new UTF8Encoding(false));
            foreach (var row in _collector.Differences)
                await writer.WriteLineAsync(JsonSerializer.Serialize(row, _json));
            await File.WriteAllTextAsync(SummaryPath,
                JsonSerializer.Serialize(_collector.BuildSummary(),
                    new JsonSerializerOptions(_json) { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Falha ao finalizar contrafactual de aplicabilidade: " + exception.Message);
        }
    }
}
