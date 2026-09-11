using System.Text.Json;
using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

public sealed class StableDeviationIncidentObserver : IAsyncDisposable
{
    private readonly StableDeviationIncidentEngine _engine = new();
    private readonly StableDeviationIncidentAdapter _adapter = new();
    private readonly StableDeviationIncidentStateStore _store = new();
    private readonly StableDeviationIncidentTraceWriter _writer;
    private readonly string _summaryPath;
    private readonly List<StableDeviationIncidentSummary> _completed = [];
    private readonly Dictionary<string,long> _alertsByTag = new(StringComparer.OrdinalIgnoreCase);
    private int _window;
    private long _observations, _rawAnomalies, _opened, _continuations, _mismatches,
        _escalations, _resolved, _suspended, _boundaries, _openingAlerts, _escalationAlerts,
        _candidateRejections;

    public string TracePath => _writer.Path;
    public string SummaryPath => _summaryPath;
    public int ActiveIncidentCount => _store.Count;

    public StableDeviationIncidentObserver(string directory, string executionId)
    {
        _writer = new(Path.Combine(directory, $"stable_deviation_incident_trace_{executionId}.jsonl"));
        _summaryPath = Path.Combine(directory, $"stable_deviation_incident_summary_{executionId}.json");
    }

    public void ResetWindow(int window)
    {
        FinalizeAll("ExecutionBoundary").GetAwaiter().GetResult();
        _window = window;
        _store.Clear();
    }

    public async ValueTask ObserveAsync(
        DualBaselineTraceRecord row, DetectorResult original,
        DualBaselineDecisionClassification classification,
        IReadOnlyList<string> reasons, string episodeId, bool episodeActive)
    {
        _observations++;
        if (original.EhAnomalia) _rawAnomalies++;
        var observation = _adapter.Adapt(row, original, classification, reasons, episodeId, episodeActive);
        var previous = _store.Get(row.Tag);
        var decision = _engine.Evaluate(previous, observation);
        if (decision.NextState is null) return;
        _store.Set(row.Tag, decision.NextState);
        Count(decision, observation);

        if (IsTraceRelevant(decision.Action))
            await _writer.WriteAsync(Trace(decision, observation));

        if (decision.NextState.Lifecycle is StableDeviationIncidentLifecycle.Resolved or
            StableDeviationIncidentLifecycle.ClosedByRegimePromotion)
        {
            _store.Remove(row.Tag, out var terminal);
            _completed.Add(Summary(terminal, terminal.ClosureReason?.ToString() ?? "Terminal"));
        }
    }

    public async ValueTask FinalizeWindowAsync() => await FinalizeAll("ExecutionBoundary");

    public async ValueTask DisposeAsync()
    {
        await FinalizeAll("ExecutionEnd");
        await _writer.DisposeAsync();
        var rawInside = _completed.Sum(x => x.RawAnomalyRecords);
        var incidentAlerts = _openingAlerts + _escalationAlerts;
        var rawOutside = Math.Max(0, _rawAnomalies - rawInside);
        var userAlerts = rawOutside + incidentAlerts;
        var reduced = Math.Max(0, _rawAnomalies - userAlerts);
        var durations = _completed.Select(x => x.WallClockDurationMinutes).Order().ToArray();
        var summary = new
        {
            generatedAtUtc = DateTime.UtcNow,
            semantics = new
            {
                userFacingAlert = "one opening alert plus one alert for the first mismatch of each continuous mismatch subevent",
                resolutionEventCountedSeparately = true,
                continuationIsNormal = false,
                rawDetectionRecordsRemoved = 0,
                finalAnomalyChanged = false
            },
            observations = _observations,
            rawUnivariateAnomalyRecords = _rawAnomalies,
            rawAnomalyRecordsInsideIncidents = rawInside,
            rawAnomalyRecordsOutsideIncidents = rawOutside,
            incidentsOpened = _opened,
            continuationRecords = _continuations,
            candidateMismatchRecords = _mismatches,
            candidateRejectionRecords = _candidateRejections,
            escalationEvents = _escalations,
            resolvedIncidents = _resolved,
            suspendedDataQualityEvents = _suspended,
            executionBoundaries = _boundaries,
            openingAlerts = _openingAlerts,
            escalationAlerts = _escalationAlerts,
            userFacingIncidentAlerts = incidentAlerts,
            totalCounterfactualUserFacingAlerts = userAlerts,
            repeatedAlertsPotentiallyConsolidated = reduced,
            reductionPercentageWithinIncidents = rawInside == 0 ? 0 : 100.0 * reduced / rawInside,
            globalUserFacingAlertReductionPercentage = _rawAnomalies == 0 ? 0 : 100.0 * reduced / _rawAnomalies,
            durationMinutes = new
            {
                average = durations.Length == 0 ? 0 : durations.Average(),
                median = durations.Length == 0 ? 0 : Percentile(durations, .5),
                maximum = durations.Length == 0 ? 0 : durations[^1]
            },
            alertsByTag = _alertsByTag.OrderByDescending(x => x.Value),
            incidents = _completed
        };
        await File.WriteAllTextAsync(_summaryPath, JsonSerializer.Serialize(summary,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private async Task FinalizeAll(string reason)
    {
        foreach (var state in _store.Snapshot().ToArray())
        {
            var boundary = _engine.AtExecutionBoundary(state, state.LastUpdatedAt, state.LastMinuteIndex);
            await _writer.WriteAsync(Trace(boundary, null));
            _completed.Add(Summary(boundary.NextState!, reason));
            _boundaries++;
        }
        _store.Clear();
    }

    private void Count(StableDeviationIncidentDecision d, StableDeviationIncidentObservation o)
    {
        switch (d.Action)
        {
            case StableDeviationIncidentAction.OpenIncident: _opened++; break;
            case StableDeviationIncidentAction.MarkStableUnresolved:
            case StableDeviationIncidentAction.RecordContinuation: _continuations++; break;
            case StableDeviationIncidentAction.EscalateCandidateMismatch: _mismatches++; _escalations++; break;
            case StableDeviationIncidentAction.UpdateEvidence when
                o.Classification == DualBaselineDecisionClassification.CandidateMismatch: _mismatches++; break;
            case StableDeviationIncidentAction.ResolveReturnedToActive: _resolved++; break;
            case StableDeviationIncidentAction.SuspendForDataQuality: _suspended++; break;
            case StableDeviationIncidentAction.RecordCandidateRejected: _candidateRejections++; break;
        }
        if (d.EmitUserFacingAlert)
        {
            if (d.AlertKind == StableDeviationIncidentAlertKind.Opening) _openingAlerts++;
            if (d.AlertKind == StableDeviationIncidentAlertKind.Escalation) _escalationAlerts++;
            _alertsByTag[o.Tag] = _alertsByTag.GetValueOrDefault(o.Tag) + 1;
        }
    }

    private static bool IsTraceRelevant(StableDeviationIncidentAction action) => action is
        StableDeviationIncidentAction.OpenIncident or
        StableDeviationIncidentAction.MarkStableUnresolved or
        StableDeviationIncidentAction.EscalateCandidateMismatch or
        StableDeviationIncidentAction.SuspendForDataQuality or
        StableDeviationIncidentAction.ResumeAfterDataQuality or
        StableDeviationIncidentAction.ResolveReturnedToActive or
        StableDeviationIncidentAction.RecordCandidateRejected or
        StableDeviationIncidentAction.CloseByRegimePromotion or
        StableDeviationIncidentAction.ExecutionBoundary;

    private static object Trace(StableDeviationIncidentDecision d, StableDeviationIncidentObservation? o) => new
    {
        timestamp = o?.Timestamp ?? d.NextState?.LastUpdatedAt,
        minuteIndex = o?.MinuteIndex ?? d.NextState?.LastMinuteIndex,
        tag = o?.Tag ?? d.NextState?.Tag,
        incidentId = d.NextState?.IncidentId,
        previousLifecycle = d.PreviousState?.Lifecycle,
        nextLifecycle = d.NextState?.Lifecycle,
        d.Action, d.Reasons, d.EmitUserFacingAlert, d.AlertKind, d.EmitResolutionEvent,
        originalFinalAnomaly = o?.Detection.FinalAnomaly,
        originalScore = o?.Detection.Score,
        anomalousDetectors = o?.Detection.AnomalousDetectors,
        candidateMismatch = o?.Classification == DualBaselineDecisionClassification.CandidateMismatch,
        candidateRejected = o?.Classification == DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive,
        candidateId = o?.CandidateId,
        outsideActive = o?.OutsideActive,
        ewmaOnlyMismatch = o?.EwmaOnlyMismatch,
        persistence = o?.TemporalConfirmation,
        activeBaselineId = d.NextState?.ActiveBaselineId,
        candidateBaselineId = d.NextState?.CandidateBaselineId,
        operationalBaselineChanged = o?.OperationalBaselineChanged ?? false,
        promotion = o?.Promotion ?? false
    };

    private static StableDeviationIncidentSummary Summary(StableDeviationIncidentState s, string reason) =>
        new(s.IncidentId, s.FastWindow, s.Tag, s.RegimeEpisodeId, s.OpenedAt,
            s.OpenedMinuteIndex, s.LastUpdatedAt, s.LastMinuteIndex,
            s.WallClockDurationMinutes, s.ObservedDurationMinutes,
            s.ActiveBaselineId, s.CandidateBaselineId, s.FirstDetection.Score,
            s.FirstDetection.AnomalousDetectors, s.MaximumEvidence.Score,
            s.CandidateReadyAt, s.StableUnresolvedAt, s.RawAnomalyRecords,
            s.ContinuationCount, s.CandidateMismatchCount, s.EscalationCount,
            s.EwmaOnlyMismatchCount, s.TemporalConfirmationCount,
            s.ReturnedToActive, s.Lifecycle, s.ClosureReason, reason,
            s.CandidateRejectionCount, s.LastRejectedCandidateId);

    private static double Percentile(IReadOnlyList<double> values, double p)
    {
        var position = p * (values.Count - 1); var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper ? values[lower] : values[lower] + (position - lower) * (values[upper] - values[lower]);
    }
}
