using System.Text.Json;
using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

namespace IAApi.Benchmark.RegimeShadow.DualBaseline;

public sealed class DualBaselineShadowObserver : IAsyncDisposable
{
    private readonly DualBaselineShadowOptions _options;
    private readonly DualBaselineShadowEngine _engine;
    private readonly StreamWriter _writer;
    private readonly string _metadataPath;
    private readonly Dictionary<string, DualBaselineTagState> _states =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly DualBaselineDecisionObserver? _decisionObserver;
    private int _window;
    public int Errors { get; private set; }
    public string TracePath { get; }

public DualBaselineShadowObserver(
    DualBaselineShadowOptions options,
    string directory,
    string executionId,
    ExperimentalIncidentOptions?
        experimentalIncidentOptions = null,
    ExperimentalAnomalyDecisionOptions?
        experimentalAnomalyDecisionOptions = null)
    {
        _options = options;
        _engine = new(options);
        TracePath = Path.Combine(directory, $"dual_baseline_shadow_trace_{executionId}.jsonl");
        _metadataPath = Path.Combine(directory, $"dual_baseline_shadow_metadata_{executionId}.json");
        _writer = new StreamWriter(new FileStream(TracePath, FileMode.Create, FileAccess.Write, FileShare.Read));
        _decisionObserver = options.DecisionCounterfactualEnabled
? new DualBaselineDecisionObserver(
    directory,
    executionId,
    options.IncidentCounterfactualEnabled,
    experimentalIncidentOptions,
    experimentalAnomalyDecisionOptions)
            : null;
    }

    public string? DecisionTracePath => _decisionObserver?.TracePath;
    public string? DecisionSummaryPath => _decisionObserver?.SummaryPath;
    public string? IncidentTracePath => _decisionObserver?.IncidentTracePath;
    public string? IncidentSummaryPath => _decisionObserver?.IncidentSummaryPath;
    public string? ExperimentalIncidentEventPath => _decisionObserver?.ExperimentalIncidentEventPath;
    public string? ExperimentalIncidentSummaryPath => _decisionObserver?.ExperimentalIncidentSummaryPath;
    public string? ExperimentalAnomalyDecisionTracePath => _decisionObserver?.ExperimentalAnomalyDecisionTracePath;
public string? ExperimentalAnomalyDecisionSummaryPath => _decisionObserver?.ExperimentalAnomalyDecisionSummaryPath;

    public void ResetWindow(int window)
    {
        _decisionObserver?.ResetWindow(window);
        _window = window;
        _states.Clear();
    }
    public bool ShouldObserve(int window, string tag) => _options.Enabled &&
        (_options.ObserveAllTagsAndWindows ||
         (window == _options.FastWindowIndex && tag.Equals(_options.Tag, StringComparison.OrdinalIgnoreCase)));

    public async ValueTask<ExperimentalAnomalyDecision?> ObserveAsync(long minute, DetectorInput input, DetectorResult original, RegimePolicyDecision decision, bool increasingNoiseEvidence = false)
    {
        if (!ShouldObserve(_window, input.TagName))
            return null;
        try
        {
            if (!_states.TryGetValue(input.TagName, out var state))
            {
                state = new DualBaselineTagState();
                _states[input.TagName] = state;
            }
            var record =
                _engine.Evaluate(
                    _window,
                    minute,
                    input,
                    original,
                    decision,
                    state)
                with
                {
                    IncreasingNoiseEvidence =
                        increasingNoiseEvidence
                };

            ExperimentalAnomalyDecision? experimentalDecision = null;

            if (_decisionObserver is not null)
            {
                experimentalDecision =
                    await _decisionObserver.ObserveAsync(
                        record,
                        original);
            }

            if (!_options.DecisionCounterfactualEnabled &&
                minute >= _options.TraceStartMinuteIndex && minute <= _options.TraceEndMinuteIndex)
                await _writer.WriteLineAsync(JsonSerializer.Serialize(record, JsonOptions));

            return experimentalDecision;
        }
        catch (Exception ex)
        {
            Errors++;
            await File.AppendAllTextAsync(_metadataPath + ".errors.jsonl",
                JsonSerializer.Serialize(new { _window, minute, input.TagName, error = ex.ToString() }) + Environment.NewLine);

            return null;
        }
    }

    public async ValueTask FinalizeWindowAsync(int window)
    {
        if (!_options.ObserveAllTagsAndWindows && window != _options.FastWindowIndex) return;
        foreach (var state in _states.Values)
            _engine.ResetCandidate(state, "FastWindowReset");
        _decisionObserver?.FinalizeWindow();
        await _writer.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _writer.FlushAsync(); await _writer.DisposeAsync();
            if (_decisionObserver is not null)
                await _decisionObserver.DisposeAsync();
            await File.WriteAllTextAsync(_metadataPath, JsonSerializer.Serialize(new
            {
                generatedAtUtc = DateTime.UtcNow,
                scope = new { _options.Tag, _options.FastWindowIndex, _options.TraceStartMinuteIndex, _options.TraceEndMinuteIndex },
                detectorParametersFromCurrentConfiguration = new { _options.MadThreshold, _options.ZScoreThreshold, _options.EwmaLambda, _options.EwmaSigmaLimit },
                experimentalReadiness = new
                {
                    _options.FormationMinimumSamples, _options.ValidationMinimumSamples,
                    _options.MinimumIndependentSamples, _options.MaximumFormationValidationScaleRatio,
                    coverageSearch = "smallest two equal-sized, non-overlapping prior blocks that repeat structurally; no period or tag is hardcoded",
                    centerRule = "mean and median deltas <= max(block robust scales, active deadband)",
                    scaleRule = "stddev and robust-scale ratios <= MaximumFormationValidationScaleRatio",
                    rangeAndQuantileRule = "range delta and maximum P05/P25/P75/P95 delta <= max(block robust scales, active deadband)",
                    slopeRule = "block slope difference and combined residual excursion <= max(block robust scales, active deadband)",
                    gapRule = "existing DetectionInputQuality threshold, or existing 5x-cadence sanitizer convention"
                },
                lifecycle = "minimum points start collection; coverage forms B; independent comparable coverage validates B; Ready snapshot is frozen; later points only audit fit/degradation",
                invariants = new
                {
                    candidateBaselineReadyDoesNotImplyLegitimacy = true,
                    candidateBaselineReadyDoesNotChangeOperationalBaseline = true,
                    operationalBaselineChanged = false, legitimacy = "None", promotion = false
                }, Errors
            }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch { }
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}