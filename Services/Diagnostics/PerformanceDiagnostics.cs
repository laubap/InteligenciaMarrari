using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.Services.AnomalyDetection;
using Microsoft.Extensions.Hosting;

namespace IAApi.Services.Diagnostics;

/// <summary>Passive, opt-in timings. No operational state or payload is captured.</summary>
public sealed class PerformanceDiagnostics
{
    private readonly ILogger<PerformanceDiagnostics> _logger;
    private readonly AsyncLocal<Measurement?> _current = new();

    public PerformanceDiagnostics(IHostEnvironment environment, ILogger<PerformanceDiagnostics> logger)
        : this(environment.EnvironmentName,
            Environment.GetEnvironmentVariable("IAAPI_PERF_DIAGNOSTICS"), logger)
    { }

    // Explicit inputs allow testing the gate without changing process-wide environment variables.
    public PerformanceDiagnostics(string environmentName, string? flag, ILogger<PerformanceDiagnostics> logger)
    {
        Enabled = string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && flag == "1";
        _logger = logger;
    }

    public bool Enabled { get; }

    public static T Measure<T>(PerformanceDiagnostics? diagnostics, string client, string tag,
        string stage, Func<T> operation)
    {
        if (diagnostics?.Enabled != true) return operation();
        using var timing = diagnostics.Start(client, tag, stage);
        try
        {
            var result = operation();
            timing.Status = "Succeeded";
            return result;
        }
        catch (OperationCanceledException) { timing.Status = "Cancelled"; throw; }
        catch { timing.Status = "Failed"; throw; }
    }

    public static async Task<T> MeasureAsync<T>(PerformanceDiagnostics? diagnostics, string client,
        string tag, string stage, Func<Task<T>> operation)
    {
        if (diagnostics?.Enabled != true) return await operation();
        using var timing = diagnostics.Start(client, tag, stage);
        try
        {
            var result = await operation();
            timing.Status = "Succeeded";
            return result;
        }
        catch (OperationCanceledException) { timing.Status = "Cancelled"; throw; }
        catch { timing.Status = "Failed"; throw; }
    }

    private Measurement Start(string client, string tag, string stage)
    {
        var timing = new Measurement(this, _current.Value, client, tag, stage);
        _current.Value = timing;
        return timing;
    }

    public void Records(int? received, int? valid)
    {
        if (!Enabled || _current.Value is not { } current) return;
        current.Received = received;
        current.Valid = valid;
    }

    public void HttpStatus(int status)
    {
        if (Enabled && _current.Value is { } current) current.HttpStatus = status;
    }

    // Counts actual HTTP history sends, including separate regime-period requests.
    // Counts are inclusive, like durations: aggregate siblings, never parents plus children.
    public void HistoricalRequest()
    {
        if (!Enabled) return;
        for (var current = _current.Value; current is not null; current = current.Parent)
            current.HistoricalRequests++;
    }

    public void HistoryAcquisition(string source, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc,
        List<LeituraBruta>? readings, DateTime? predictionReference)
    {
        if (!Enabled || _current.Value is not { } current) return;
        try
        {
            current.HistorySource = source;
            current.HistoryAcquiredStartedUtc = startedUtc;
            current.HistoryAcquiredCompletedUtc = completedUtc;
            current.PredictionReference = predictionReference;
            current.Received = readings?.Count;
            if (readings is null) return;
            foreach (var reading in readings)
            {
                if (reading is null || reading.DataHora == default) continue;
                if (current.HistoryFirst is null || reading.DataHora < current.HistoryFirst)
                    current.HistoryFirst = reading.DataHora;
                if (current.HistoryLast is null || reading.DataHora > current.HistoryLast)
                    current.HistoryLast = reading.DataHora;
            }
        }
        catch { /* Metadata must not change the history operation. */ }
    }

    public void PredictionOutcome(DetectorResult detectorResult, string valueTrend,
        DateTime predictionReference)
    {
        if (!Enabled || _current.Value is not { } current) return;
        current.DetectorScore = detectorResult.Score;
        current.DetectorAnomaly = detectorResult.EhAnomalia;
        current.ValueTrend = valueTrend;
        current.PredictionReference = predictionReference;
        try
        {
            // Whitelist outcomes only: no explanations, metrics dictionaries or historical values.
            current.DetectorResults = JsonSerializer.Serialize(detectorResult.DetalhesDetectores.Select(d => new
            { d.Nome, d.Aplicavel, d.VotouAnomalia, d.ScoreOriginal, d.ScoreNormalizado }),
                new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals });
        }
        catch { /* Optional diagnostic summaries must never change prediction results. */ }
    }

    /// <summary>Only UTC source timestamps are trusted; unspecified/local dates lack source zone evidence.</summary>
    public void MonitoringReading(string client, string tag, DateTime? reading,
        DateTimeOffset startedUtc, DateTimeOffset completedUtc, double durationMs, string status)
    {
        if (!Enabled) return;
        var trustworthy = reading is { Kind: DateTimeKind.Utc } && reading != default(DateTime);
        DateTimeOffset? readingUtc = trustworthy ? new DateTimeOffset(reading!.Value) : null;
        var ageMs = readingUtc is not null && readingUtc <= startedUtc
            ? (startedUtc - readingUtc.Value).TotalMilliseconds : (double?)null;
        try
        {
            _logger.LogInformation(
                "PERF_DIAG Client={Client} Tag={Tag} Stage=Monitoring.Reading DurationMs={DurationMs} TimestampUtc={TimestampUtc:O} Status={Status} ReadingOriginal={ReadingOriginal:O} ReadingKind={ReadingKind} ReadingUtc={ReadingUtc:O} PredictionStartedUtc={PredictionStartedUtc:O} PredictionCompletedUtc={PredictionCompletedUtc:O} ReadingAgeAtStartMs={ReadingAgeAtStartMs}",
                client, tag, durationMs, completedUtc, status, reading, reading?.Kind.ToString(),
                readingUtc, startedUtc, completedUtc, ageMs);
        }
        catch { /* Diagnostics must never affect monitoring. */ }
    }

    private sealed class Measurement : IDisposable
    {
        private readonly PerformanceDiagnostics _owner;
        private readonly Measurement? _parent;
        private readonly string _client, _tag, _stage;
        private readonly Stopwatch _timer = Stopwatch.StartNew();
        private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
        private readonly string _id = Guid.NewGuid().ToString("N");
        private readonly string _trace;
        public string Status { get; set; } = "Failed";
        public int? Received { get; set; }
        public int? Valid { get; set; }
        public int? HttpStatus { get; set; }
        public Measurement? Parent => _parent;
        public int HistoricalRequests { get; set; }
        public string? HistorySource { get; set; }
        public DateTimeOffset? HistoryAcquiredStartedUtc { get; set; }
        public DateTimeOffset? HistoryAcquiredCompletedUtc { get; set; }
        public DateTime? HistoryFirst { get; set; }
        public DateTime? HistoryLast { get; set; }
        public DateTime? PredictionReference { get; set; }
        public double? DetectorScore { get; set; }
        public bool? DetectorAnomaly { get; set; }
        public string? ValueTrend { get; set; }
        public string? DetectorResults { get; set; }

        public Measurement(PerformanceDiagnostics owner, Measurement? parent, string client, string tag, string stage)
        {
            _owner = owner; _parent = parent; _client = client; _tag = tag; _stage = stage;
            _trace = parent?._trace ?? _id;
        }

        public void Dispose()
        {
            _timer.Stop();
            _owner._current.Value = _parent;
            try
            {
                _owner._logger.LogInformation(
                    "PERF_DIAG Client={Client} Tag={Tag} Stage={Stage} DurationMs={DurationMs} TimestampUtc={TimestampUtc:O} StartedUtc={StartedUtc:O} Status={Status} TraceId={TraceId} SpanId={SpanId} ParentSpanId={ParentSpanId} ReceivedRecords={ReceivedRecords} ValidRecords={ValidRecords} HttpStatus={HttpStatus} HistoricalRequests={HistoricalRequests} HistorySource={HistorySource} HistoryAcquiredStartedUtc={HistoryAcquiredStartedUtc:O} HistoryAcquiredCompletedUtc={HistoryAcquiredCompletedUtc:O} HistoryFirstOriginal={HistoryFirstOriginal:O} HistoryFirstKind={HistoryFirstKind} HistoryLastOriginal={HistoryLastOriginal:O} HistoryLastKind={HistoryLastKind} PredictionReferenceOriginal={PredictionReferenceOriginal:O} PredictionReferenceKind={PredictionReferenceKind} DetectorScore={DetectorScore} DetectorAnomaly={DetectorAnomaly} ValueTrend={ValueTrend} DetectorResults={DetectorResults}",
                    _client, _tag, _stage, _timer.Elapsed.TotalMilliseconds, DateTimeOffset.UtcNow,
                    _startedUtc, Status, _trace, _id, _parent?._id, Received, Valid, HttpStatus,
                    HistoricalRequests, HistorySource, HistoryAcquiredStartedUtc, HistoryAcquiredCompletedUtc,
                    HistoryFirst, HistoryFirst?.Kind.ToString(), HistoryLast, HistoryLast?.Kind.ToString(),
                    PredictionReference, PredictionReference?.Kind.ToString(), DetectorScore, DetectorAnomaly, ValueTrend,
                    DetectorResults);
            }
            catch { /* Do not mask results, failures or cancellation with a logging failure. */ }
        }
    }
}
