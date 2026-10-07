using System.Text.Json;
using IAApi.Services.AnomalyDetection;
using IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

namespace IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;

public sealed class DualBaselineDecisionObserver : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _summaryPath;
    private readonly Dictionary<string, Episode> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DualBaselineCounterfactualEpisodeSummary> _episodes = [];
    private readonly Dictionary<string,long> _continuationsByTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,long> _suppressibleByDetector = new(StringComparer.OrdinalIgnoreCase);
    private readonly StableDeviationIncidentObserver? _incidentObserver;
    private readonly ExperimentalIncidentObserver? _experimentalIncidentObserver;
    private readonly ExperimentalAnomalyDecisionObserver?
    _experimentalAnomalyDecisionObserver;
    private int _window;
    private long _legacyAnomalies, _eligible, _continuations, _mismatches, _suppressible, _ewmaOnly;
    private long _candidateRejections;
    public string TracePath { get; }
    public string SummaryPath => _summaryPath;
    public int ActiveEpisodeCount => _active.Count;
    public string? IncidentTracePath => _incidentObserver?.TracePath;
    public string? IncidentSummaryPath => _incidentObserver?.SummaryPath;
    public string? ExperimentalIncidentEventPath => _experimentalIncidentObserver?.EventPath;
    public string? ExperimentalIncidentSummaryPath => _experimentalIncidentObserver?.SummaryPath;
    public string? ExperimentalAnomalyDecisionTracePath =>
    _experimentalAnomalyDecisionObserver?.TracePath;

public string? ExperimentalAnomalyDecisionSummaryPath =>
    _experimentalAnomalyDecisionObserver?.SummaryPath;

public DualBaselineDecisionObserver(
    string directory,
    string executionId,
    bool incidentCounterfactualEnabled = false,
    ExperimentalIncidentOptions? experimentalIncidentOptions = null,
    ExperimentalAnomalyDecisionOptions?
        experimentalAnomalyDecisionOptions = null)
    {
        TracePath = Path.Combine(directory, $"dual_baseline_decision_trace_{executionId}.jsonl");
        _summaryPath = Path.Combine(directory, $"dual_baseline_decision_summary_{executionId}.json");
        _writer = new StreamWriter(new FileStream(TracePath, FileMode.Create, FileAccess.Write, FileShare.Read));
        _incidentObserver = incidentCounterfactualEnabled
            ? new StableDeviationIncidentObserver(directory, executionId)
            : null;
        _experimentalIncidentObserver = experimentalIncidentOptions?.Enabled == true
            ? new ExperimentalIncidentObserver(experimentalIncidentOptions, directory, executionId)
            : null;

            _experimentalAnomalyDecisionObserver =
    experimentalAnomalyDecisionOptions?.Enabled == true
        ? new ExperimentalAnomalyDecisionObserver(
            experimentalAnomalyDecisionOptions,
            directory,
            executionId)
        : null;
    }

    public void ResetWindow(int window)
    {
        FinalizeAll("FastWindowReset");
        _incidentObserver?.ResetWindow(window);
        _experimentalIncidentObserver?.ResetWindow(window);
        _window = window;
    }

    public async ValueTask<ExperimentalAnomalyDecision?> ObserveAsync(DualBaselineTraceRecord row, DetectorResult original)
    {
        if (original.EhAnomalia) _legacyAnomalies++;
        var key = row.Tag;
        _active.TryGetValue(key, out var episode);
        if (row.PolicyAction == "ObserveTransition" && row.RegimeState == "TransitionSuspected" && episode is null)
        {
            episode = new Episode(row.Tag, _window, row.EpisodeId ?? $"decision:{_window}:{row.Tag}:{row.MinuteIndex}", row.MinuteIndex);
            _active[key] = episode;
        }

        var stableAlreadyStarted = episode?.StableUnresolvedStartMinute is not null;
        var (classification, reasons) = DualBaselineDecisionClassifier.Classify(row, stableAlreadyStarted);
        episode?.Observe(row, classification);
        if (_incidentObserver is not null)
            await _incidentObserver.ObserveAsync(row, original, classification, reasons,
                episode?.Id ?? row.EpisodeId ?? $"episode:{_window}:{row.Tag}:{row.MinuteIndex}",
                episode is not null);
        if (_experimentalIncidentObserver is not null)
            await _experimentalIncidentObserver.ObserveAsync(row, original, classification,
                episode?.Id ?? row.EpisodeId ?? $"episode:{_window}:{row.Tag}:{row.MinuteIndex}",
                episode is not null);

        ExperimentalAnomalyDecision? experimentalDecision = null;

        if (_experimentalAnomalyDecisionObserver is not null)
        {
            var regimeEpisodeId =
                episode?.Id ??
                row.EpisodeId ??
                $"episode:{_window}:{row.Tag}:{row.MinuteIndex}";

            var incidentId =
                _experimentalIncidentObserver?
                    .GetOpenIncidentId(
                        row.FastWindowIndex,
                        row.Tag);

            experimentalDecision =
                await _experimentalAnomalyDecisionObserver.ObserveAsync(
                    row,
                    original,
                    classification,
                    regimeEpisodeId,
                    incidentId);
        }

        if (row.CandidateBaselineReady && row.RegimeState == "StableUnresolvedDeviation" && row.AgainstActive == "Abnormal") _eligible++;
        if (classification == DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation)
        {
            _continuations++;
            Increment(_continuationsByTag, row.Tag);
            if (row.EwmaCandidate?.Vote == true) _ewmaOnly++;
            if (original.EhAnomalia)
            {
                _suppressible++;
                foreach (var detector in original.DetalhesDetectores.Where(x => x.VotouAnomalia).Select(x => x.Nome))
                    Increment(_suppressibleByDetector, detector);
            }
        }
        if (classification == DualBaselineDecisionClassification.CandidateMismatch) _mismatches++;
        if (classification == DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive) _candidateRejections++;

        var relevant = classification is DualBaselineDecisionClassification.StableUnresolvedDeviationStart
            or DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation
            or DualBaselineDecisionClassification.CandidateMismatch
            or DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive
            or DualBaselineDecisionClassification.ResolvedOrReturnedToActive;
        if (relevant && (original.EhAnomalia || classification != DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation))
        {
            var record = new DualBaselineDecisionRecord(row.FastWindowIndex, row.MinuteIndex, row.Timestamp,
                row.Tag, episode?.Id ?? row.EpisodeId ?? "none", classification, reasons,
                original.EhAnomalia, original.Score, row.CandidateBaselineReady, row.RegimeState,
                row.AgainstActive, row.MadCandidate?.Vote == true, row.ZCandidate?.Vote == true,
                row.EwmaCandidate?.Vote == true,
                classification == DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation && row.EwmaCandidate?.Vote == true,
                original.EhAnomalia && classification == DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation,
                row.CurrentReadingInActiveReference, row.CurrentReadingInCandidateReference,
                row.OperationalBaselineChanged, row.Promotion,
                original.DetalhesDetectores.Where(x => x.VotouAnomalia).Select(x => x.Nome).ToArray());
            await _writer.WriteLineAsync(JsonSerializer.Serialize(record, JsonOptions));
        }

        if (classification == DualBaselineDecisionClassification.ResolvedOrReturnedToActive)
            Finalize(key, "ReturnedToActive");

        return experimentalDecision;
    }

    public void FinalizeWindow()
    {
        FinalizeAll("FastWindowReset");
        if (_incidentObserver is not null)
            _incidentObserver.FinalizeWindowAsync().AsTask().GetAwaiter().GetResult();
        if (_experimentalIncidentObserver is not null)
            _experimentalIncidentObserver.FinalizeWindowAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        FinalizeAll("ExecutionEnd");
        await _writer.FlushAsync(); await _writer.DisposeAsync();
        if (_incidentObserver is not null)
            await _incidentObserver.DisposeAsync();
        if (_experimentalIncidentObserver is not null)
            await _experimentalIncidentObserver.DisposeAsync();

            if (_experimentalAnomalyDecisionObserver is not null)
    await _experimentalAnomalyDecisionObserver.DisposeAsync();
        var summary = new
        {
            generatedAtUtc = DateTime.UtcNow,
            semantics = new
            {
                continuation = "Ready + StableUnresolved + Active abnormal + MAD/Z normal against B; isolated EWMA-B mismatch is retained as temporal diagnostic",
                candidateMismatch = "MAD or Z abnormal against frozen B",
                finalAnomalyChanged = false,
                candidateLegitimized = false
            },
            legacyAnomalyRecords = _legacyAnomalies,
            eligibleStableDeviationRecords = _eligible,
            continuationRecords = _continuations,
            candidateMismatchRecords = _mismatches,
            candidateRejectionRecords = _candidateRejections,
            repeatedAlertsPotentiallySuppressible = _suppressible,
            reductionPercentage = _legacyAnomalies == 0 ? 0 : 100.0 * _suppressible / _legacyAnomalies,
            episodeCount = _episodes.Count,
            consolidatedEpisodeCount = _episodes.Count(x => x.SuppressibleRepeatedAlerts > 0),
            ewmaOnlyMismatchContinuationRecords = _ewmaOnly,
            continuationsByTag = _continuationsByTag.OrderByDescending(x => x.Value),
            suppressibleByDetector = _suppressibleByDetector.OrderByDescending(x => x.Value),
            episodes = _episodes
        };
        await File.WriteAllTextAsync(_summaryPath, JsonSerializer.Serialize(summary,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private void FinalizeAll(string finalState)
    { foreach (var key in _active.Keys.ToArray()) Finalize(key, finalState); }
    private void Finalize(string key, string finalState)
    {
        if (!_active.Remove(key, out var e)) return;
        _episodes.Add(e.ToSummary(finalState));
    }
    private static void Increment(Dictionary<string,long> map, string key) => map[key] = map.GetValueOrDefault(key) + 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed class Episode(string tag, int window, string id, long transition)
    {
        public string Id { get; } = id;
        public long? StableUnresolvedStartMinute { get; private set; }
        private long? _ready, _eligible; private long _last=transition, _legacy, _continuations, _suppressible, _mismatches, _rejections, _ewma;
        private bool _returned, _promotion;
        public void Observe(DualBaselineTraceRecord row, DualBaselineDecisionClassification classification)
        {
            _last=row.MinuteIndex; if(row.OriginalFinalAnomaly)_legacy++;
            if(row.RegimeState=="StableUnresolvedDeviation") StableUnresolvedStartMinute ??= row.MinuteIndex;
            if(row.CandidateBaselineReady)_ready ??= row.MinuteIndex;
            if(classification==DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation)
            { _eligible??=row.MinuteIndex;_continuations++;if(row.OriginalFinalAnomaly)_suppressible++;if(row.EwmaCandidate?.Vote==true)_ewma++; }
            if(classification==DualBaselineDecisionClassification.CandidateMismatch)_mismatches++;
            if(classification==DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive)_rejections++;
            if(classification==DualBaselineDecisionClassification.ResolvedOrReturnedToActive)_returned=true;
            _promotion|=row.Promotion;
        }
        public DualBaselineCounterfactualEpisodeSummary ToSummary(string finalState) => new(tag,window,Id,transition,
            StableUnresolvedStartMinute,_ready,_eligible,_last,_last-transition+1,_legacy,_continuations,_suppressible,
            _mismatches,_rejections,_ewma,_returned,_promotion,finalState);
    }
}