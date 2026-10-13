using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.PersistenceExperimental;

public sealed class PersistenceExperimentalObserver : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private long _evaluated;
    private long _withTemporalConfirmation;
    private long _pending;
    private long _confirmed;
    private long _interrupted;
    private bool _disabled;

    public PersistenceExperimentalObserver(string directory, string executionId)
    {
        TracePath = Path.Combine(directory,
            $"persistence_temporal_experimental_{executionId}.jsonl");
        SummaryPath = Path.Combine(directory,
            $"persistence_temporal_experimental_summary_{executionId}.json");
        _writer = new StreamWriter(TracePath, false, new UTF8Encoding(false));
    }

    public string TracePath { get; }
    public string SummaryPath { get; }
    public void ResetWindow() { }

    public void Observe(int fastWindowIndex, int minuteIndex, DetectorInput input,
        DetectorResult result)
    {
        if (_disabled) return;
        try
        {
            _evaluated++;
            var persistence = result.DetalhesDetectores.FirstOrDefault(x =>
                x.Nome.Contains("Persist", StringComparison.OrdinalIgnoreCase));
            if (persistence?.TemporalConfirmation is not { } temporal) return;

            _withTemporalConfirmation++;
            if (temporal.Status == TemporalConfirmationStatus.Pending) _pending++;
            if (temporal.Status == TemporalConfirmationStatus.Confirmed) _confirmed++;
            if (temporal.Status == TemporalConfirmationStatus.InterruptedByGap) _interrupted++;

            var independent = result.DetalhesDetectores
                .Where(x => x.Aplicavel &&
                            x.EvidenceRole == DetectorEvidenceRole.StatisticalEvidence)
                .Select(x => x.Nome).ToArray();
            var votes = result.DetalhesDetectores
                .Where(x => x.VotouAnomalia).Select(x => x.Nome).ToArray();

            var record = new
            {
                fastWindowIndex,
                minuteIndex,
                timestamp = input.DataHoraAtual,
                tag = input.TagName,
                currentValue = input.ValorAtual,
                finalAnomaly = result.EhAnomalia,
                finalScore = result.ScoreNormalizado,
                independentStatisticalDetectors = independent,
                anomalousDetectors = votes,
                persistenceEvidenceRole = persistence.EvidenceRole,
                persistenceUnderlyingEvidenceKind = persistence.UnderlyingEvidenceKind,
                temporalStatus = temporal.Status,
                temporal.ConsecutivePoints,
                temporal.DurationMinutes,
                temporal.RequiredPoints,
                temporal.RequiredDurationMinutes,
                temporal.StartedAt,
                temporal.ObservedThrough,
                temporal.InterruptedByGap,
                temporal.LargestObservedGapMinutes,
                madAnomalous = Vote(result, "Mediana e MAD"),
                ewmaAnomalous = Vote(result, "EWMA"),
                zScoreAnomalous = Vote(result, "Z-Score"),
                rocAnomalous = Vote(result, "Taxa de varia"),
                levelShiftAnomalous = Vote(result, "Mudan")
            };
            _writer.WriteLine(JsonSerializer.Serialize(record, _json));
        }
        catch (Exception exception)
        {
            _disabled = true;
            Console.Error.WriteLine(
                "Trace experimental de Persistence desativado: " + exception.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
        if (_disabled) return;
        var summary = new
        {
            generatedUtc = DateTime.UtcNow,
            evaluatedReadings = _evaluated,
            readingsWithTemporalConfirmation = _withTemporalConfirmation,
            pendingReadings = _pending,
            confirmedReadings = _confirmed,
            interruptedByGapReadings = _interrupted
        };
        await File.WriteAllTextAsync(SummaryPath,
            JsonSerializer.Serialize(summary,
                new JsonSerializerOptions(_json) { WriteIndented = true }));
    }

    private static bool Vote(DetectorResult result, string name) =>
        result.DetalhesDetectores.Any(x => x.VotouAnomalia &&
            x.Nome.Contains(name, StringComparison.OrdinalIgnoreCase));
}
