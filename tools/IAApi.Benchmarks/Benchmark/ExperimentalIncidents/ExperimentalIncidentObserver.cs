using System.Text.Json;
using System.Diagnostics;
using IAApi.Infrastructure.Incidents.Experimental.ReadModels;
using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;
using IAApi.Services.AnomalyDetection.Runtime.Incidents.StableDeviation;
using IAApi.Services.AnomalyDetection.Runtime.Incidents.Experimental;
using IAApi.Benchmark.ExperimentalIncidents.ReadModels;
using IAApi.Application.Incidents.Experimental.ReadModels;

namespace IAApi.Benchmark.ExperimentalIncidents;

public sealed class ExperimentalIncidentObserver : IAsyncDisposable
{
    private readonly ExperimentalIncidentOptions _options;
    private readonly string _scopeId;
    private readonly ExperimentalIncidentAdapter _adapter = new();
    private readonly ExperimentalIncidentCoordinator _coordinator = new();
    private readonly IIncidentStateStore _store;
    private readonly IExperimentalIncidentEventSink _sink;
    private readonly IIncidentReadModelProjector _readModelProjector;
    private readonly IIncidentReadModelStore _readModelStore;
    private readonly IIncidentReadModelSnapshotWriter _readModelWriter;
    private readonly string _readModelPath;
    private readonly string _summaryPath;
    private readonly string _errorPath;
    private long _sequence;
    private int _window;
    private long _rawAlerts, _opened, _updates, _continuations, _escalations,
        _resolutions, _boundaries, _postRejectContinuations, _rawOnly, _errors;
    private readonly List<double> _promotionLatencies = [];
    private readonly List<double> _completedDurations = [];
    private readonly Dictionary<string, long> _candidateAttempts =
        new(StringComparer.OrdinalIgnoreCase);
    private long _maximumActiveIncidents;
    private long _readModelProjectedEvents, _readModelProjectionTicks,
        _readModelErrors, _readModelBytes, _maximumReadModelBytes;

    public string? EventPath => (_sink as JsonlExperimentalIncidentEventSink)?.Path;
    public string SummaryPath => _summaryPath;
    public string ReadModelPath => _readModelPath;
    public long ExperimentalErrors => _errors;
    public long ReadModelErrors => _readModelErrors;
    public IIncidentReadModelStore ReadModelStore => _readModelStore;

    public string? GetOpenIncidentId(
    int fastWindow,
    string tag)
{
    var key = new IncidentStreamKey(
        _scopeId,
        fastWindow,
        tag);

    var stored = _store.Load(key);

    var incident = stored?.State.Incident;

    if (incident is null)
        return null;

    if (incident.Lifecycle is
        StableDeviationIncidentLifecycle.Resolved or
        StableDeviationIncidentLifecycle.ClosedByRegimePromotion)
    {
        return null;
    }

    return incident.IncidentId;
}

    public ExperimentalIncidentObserver(
        ExperimentalIncidentOptions options,
        string directory,
        string executionId,
        IIncidentStateStore? store = null,
        IExperimentalIncidentEventSink? sink = null,
        IIncidentReadModelProjector? readModelProjector = null,
        IIncidentReadModelStore? readModelStore = null,
        IIncidentReadModelSnapshotWriter? readModelWriter = null)
    {
        _options = options;
        _scopeId = executionId;
        _store = store ?? new InMemoryIncidentStateStore();
        _sink = sink ?? new JsonlExperimentalIncidentEventSink(Path.Combine(
            directory, $"experimental_incident_events_{executionId}.jsonl"));
        _readModelProjector = readModelProjector ?? new IncidentReadModelProjector();
        _readModelStore = readModelStore ?? new InMemoryIncidentReadModelStore();
        _readModelWriter = readModelWriter ?? new JsonIncidentReadModelSnapshotWriter();
        _readModelPath = Path.Combine(directory,
            $"experimental_incident_read_models_{executionId}.json");
        _summaryPath = Path.Combine(directory,
            $"experimental_incident_summary_{executionId}.json");
        _errorPath = Path.Combine(directory,
            $"experimental_incident_errors_{executionId}.jsonl");
    }

    public void ResetWindow(int window)
    {
        if (_window != 0) FinalizeWindowAsync().AsTask().GetAwaiter().GetResult();
        _window = window;
    }

    public async ValueTask ObserveAsync(
        DualBaselineTraceRecord row,
        DetectorResult original,
        DualBaselineDecisionClassification classification,
        string episodeId,
        bool episodeActive)
    {
        if (!_options.Enabled) return;
        try
        {
            var observation = _adapter.Adapt(_scopeId, ++_sequence, row,
                original, classification, episodeId, episodeActive);
            var key = new IncidentStreamKey(_scopeId, row.FastWindowIndex, row.Tag);
            var stored = _store.Load(key);
            var previous = stored?.State;
            var evaluation = _coordinator.Evaluate(previous, observation);
            if (evaluation.DuplicateObservation) return;

            if (!_store.TryWrite(key, stored?.Version ?? 0, evaluation.NextState))
                throw new InvalidOperationException("IncidentStateConcurrencyConflict");
            _maximumActiveIncidents = Math.Max(_maximumActiveIncidents,
                _store.Snapshot().LongCount(x => x.Value.State.Incident is not null));
            await Publish(evaluation.Events, previous, evaluation.NextState);

            if (previous?.PendingRawAlert is not null &&
                evaluation.NextState.PendingRawAlert is null &&
                evaluation.NextState.Incident is null)
            {
                _rawOnly++;
                _store.Remove(key);
            }

            if (evaluation.NextState.Incident?.Lifecycle is
                StableDeviationIncidentLifecycle.Resolved or
                StableDeviationIncidentLifecycle.ClosedByRegimePromotion)
            {
                _completedDurations.Add(
                    evaluation.NextState.Incident.WallClockDurationMinutes);
                _store.Remove(key);
            }
        }
        catch (Exception ex)
        {
            _errors++;
            await RecordError(row.FastWindowIndex, row.MinuteIndex, row.Tag, ex);
            if (!_options.FailOpen) throw;
        }
    }

    public async ValueTask FinalizeWindowAsync()
    {
        foreach (var pair in _store.Snapshot()
                     .Where(x => x.Key.FastWindow == _window).ToArray())
        {
            try
            {
                var state = pair.Value.State;
                if (state.Incident is null)
                {
                    if (state.PendingRawAlert is not null) _rawOnly++;
                    _store.Remove(pair.Key);
                    continue;
                }
                var last = state.Incident;
                var observation = BoundaryObservation(pair.Key, last);
                var evaluation = _coordinator.AtExecutionBoundary(state, observation);
                await Publish(evaluation.Events, state, evaluation.NextState);
                _completedDurations.Add(last.WallClockDurationMinutes);
                _store.Remove(pair.Key);
            }
            catch (Exception ex)
            {
                _errors++;
                await RecordError(pair.Key.FastWindow,
                    pair.Value.State.LastProcessedSequence, pair.Key.Tag, ex);
                if (!_options.FailOpen) throw;
            }
        }
    }

    private async ValueTask Publish(
        IReadOnlyList<ExperimentalIncidentEvent> events,
        ExperimentalIncidentAggregateState? previous,
        ExperimentalIncidentAggregateState next)
    {
        foreach (var incidentEvent in events)
        {
            await _sink.WriteAsync(incidentEvent);
            await ProjectReadModel(incidentEvent);
            switch (incidentEvent.Kind)
            {
                case ExperimentalIncidentEventKind.RawAlert: _rawAlerts++; break;
                case ExperimentalIncidentEventKind.IncidentOpened:
                    _opened++;
                    if (next.PendingRawAlert is not null)
                        _promotionLatencies.Add(Math.Max(0,
                            incidentEvent.MinuteIndex - next.PendingRawAlert.ObservedMinuteIndex));
                    if (incidentEvent.IncidentId is not null)
                        _candidateAttempts[incidentEvent.IncidentId] = next.CandidateAttemptCount;
                    break;
                case ExperimentalIncidentEventKind.IncidentUpdated:
                    _updates++;
                    if (incidentEvent.Reason.Contains("Continuation", StringComparison.OrdinalIgnoreCase) ||
                        incidentEvent.Reason.Contains("CurrentReadingFitsReadyCandidate", StringComparison.OrdinalIgnoreCase))
                        _continuations++;
                    if ((previous?.Incident?.CandidateRejectionCount ?? 0) > 0)
                        _postRejectContinuations++;
                    break;
                case ExperimentalIncidentEventKind.IncidentEscalated: _escalations++; break;
                case ExperimentalIncidentEventKind.IncidentResolved: _resolutions++; break;
                case ExperimentalIncidentEventKind.IncidentExecutionBoundary: _boundaries++; break;
            }
            if (incidentEvent.IncidentId is not null)
                _candidateAttempts[incidentEvent.IncidentId] = next.CandidateAttemptCount;
        }
    }

    private async ValueTask ProjectReadModel(ExperimentalIncidentEvent incidentEvent)
    {
        if (!_options.ReadModelEnabled || incidentEvent.IncidentId is null) return;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var previous = _readModelStore.Get(incidentEvent.IncidentId);
            var next = _readModelProjector.Project(previous, incidentEvent);
            if (next is not null) _readModelStore.Upsert(next);
            _readModelProjectedEvents++;
        }
        catch (Exception ex)
        {
            _readModelErrors++;
            _errors++;
            await RecordError(incidentEvent.FastWindow, incidentEvent.MinuteIndex,
                incidentEvent.Tag, ex);
            if (!_options.FailOpen) throw;
        }
        finally
        {
            _readModelProjectionTicks += Stopwatch.GetElapsedTime(started).Ticks;
        }
    }

    private ExperimentalIncidentObservation BoundaryObservation(
        IncidentStreamKey key, StableDeviationIncidentState state) => new(
        _scopeId, key.FastWindow, ++_sequence, state.LastUpdatedAt,
        state.LastMinuteIndex, key.Tag, state.RegimeEpisodeId, true,
        state.Lifecycle.ToString(), "ExecutionBoundary", state.LastRejectedCandidateId,
        state.ActiveBaselineId, state.CandidateBaselineId, false, false, false,
        false, false, false, false, false, true, false,
        new(state.CurrentEvidence.FinalAnomaly, state.CurrentEvidence.Score,
            state.CurrentEvidence.AnomalousDetectors),
        new(false, DetectorEvidenceRole.StatisticalEvidence,
            UnderlyingEvidenceKind.Unknown, null,
            ExperimentalPersistenceMeaning.None, 0, 0, null, false));

    private async Task RecordError(int window, long minute, string tag, Exception ex)
    {
        var line = JsonSerializer.Serialize(new
        {
            timestamp = DateTime.UtcNow,
            window,
            minute,
            tag,
            error = ex.ToString(),
            failOpen = _options.FailOpen
        });
        try { await File.AppendAllTextAsync(_errorPath, line + Environment.NewLine); }
        catch { /* Observability failures cannot affect Legacy. */ }
    }

    public async ValueTask DisposeAsync()
    {
        try { await FinalizeWindowAsync(); }
        catch when (_options.FailOpen) { _errors++; }
        try { await _sink.DisposeAsync(); }
        catch when (_options.FailOpen) { _errors++; }

        var readModels = _readModelStore.List(new(Descending: false));
        if (_options.ReadModelEnabled)
        {
            try
            {
                await _readModelWriter.WriteAsync(_readModelPath, _scopeId, readModels);
                if (File.Exists(_readModelPath)) _readModelBytes = new FileInfo(_readModelPath).Length;
                _maximumReadModelBytes = readModels.Count == 0 ? 0 : readModels.Max(x =>
                    JsonSerializer.SerializeToUtf8Bytes(x).LongLength);
            }
            catch (Exception ex)
            {
                _readModelErrors++;
                _errors++;
                await RecordError(_window, _sequence, "read-model-snapshot", ex);
                if (!_options.FailOpen) throw;
            }
        }

        var latencies = _promotionLatencies.Order().ToArray();
        var summary = new
        {
            generatedAtUtc = DateTime.UtcNow,
            mode = _options.Mode.ToString(),
            enabled = _options.Enabled,
            failOpen = _options.FailOpen,
            readModelEnabled = _options.ReadModelEnabled,
            semantics = new
            {
                legacyMutated = false,
                notificationSuppressed = false,
                incidentOpenedRequestsNotification = false,
                operationalBaselineChanged = false,
                persistenceMad = "TemporalConfirmation",
                persistenceDeadband = "Legacy",
                persistenceSdFallback = "Legacy"
            },
            rawAlertsObserved = _rawAlerts,
            incidentsOpened = _opened,
            transientRawOnlyEvents = _rawOnly,
            promotionLatency = Stats(latencies),
            continuations = _continuations,
            incidentUpdates = _updates,
            escalations = _escalations,
            resolutions = _resolutions,
            executionBoundaries = _boundaries,
            postRejectContinuations = _postRejectContinuations,
            candidateAttemptsPerIncident = _candidateAttempts,
            activeIncidents = _store.Snapshot().Count(x => x.Value.State.Incident is not null),
            maximumActiveIncidents = _maximumActiveIncidents,
            longestIncidentMinutes = _completedDurations.Count == 0 ? 0 : _completedDurations.Max(),
            experimentalErrors = _errors,
            readModel = new
            {
                incidents = readModels.Count,
                open = readModels.Count(x => x.IsOpen),
                resolved = readModels.Count(x => !x.IsOpen),
                escalated = readModels.Count(x => x.EscalationCount > 0),
                executionBoundary = readModels.Count(x => x.ExecutionBoundary),
                projectedEvents = _readModelProjectedEvents,
                projectionElapsedMilliseconds = TimeSpan.FromTicks(_readModelProjectionTicks).TotalMilliseconds,
                averageProjectionMicroseconds = _readModelProjectedEvents == 0 ? 0 :
                    TimeSpan.FromTicks(_readModelProjectionTicks).TotalMicroseconds / _readModelProjectedEvents,
                totalBytes = _readModelBytes,
                averageBytesPerIncident = readModels.Count == 0 ? 0 : (double)_readModelBytes / readModels.Count,
                maximumSerializedIncidentBytes = _maximumReadModelBytes,
                activeObjects = _readModelStore.Count,
                errors = _readModelErrors,
                path = _readModelPath
            }
        };
        try
        {
            await File.WriteAllTextAsync(_summaryPath,
                JsonSerializer.Serialize(summary, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
        }
        catch when (_options.FailOpen) { _errors++; }
    }

    private static object Stats(IReadOnlyList<double> values) => new
    {
        count = values.Count,
        minimum = values.Count == 0 ? 0 : values[0],
        median = Percentile(values, .5),
        average = values.Count == 0 ? 0 : values.Average(),
        p90 = Percentile(values, .9),
        p95 = Percentile(values, .95),
        maximum = values.Count == 0 ? 0 : values[^1]
    };

    private static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0) return 0;
        var position = p * (values.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper ? values[lower] : values[lower] +
            (position - lower) * (values[upper] - values[lower]);
    }
}
