using System.Text.Json;
using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;
using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;

namespace IAApi.Benchmark.RegimeShadow;

public sealed class RegimeShadowObserver : IAsyncDisposable
{
    private readonly RegimeShadowOptions _options;
    private readonly RegimeShadowStateStore _store = new();
    private readonly RegimeEvidenceAdapter _adapter = new();
    private readonly RegimePolicyEngine _engine = new();
    private readonly RegimeShadowTraceWriter _writer;
    private readonly string _summaryPath;
    private readonly DualBaselineShadowObserver? _dualBaseline;
    private readonly Dictionary<string, RegimeShadowTraceRecord> _lastRecords = new(StringComparer.OrdinalIgnoreCase);
    public RegimeShadowMetrics Metrics { get; } = new();

    public RegimeShadowObserver(
        RegimeShadowOptions options,
        string resultDirectory,
        string executionId,
        DualBaselineShadowOptions? dualBaseline = null,
        ExperimentalIncidentOptions? experimentalIncident = null,
        ExperimentalAnomalyDecisionOptions? experimentalAnomalyDecision = null)
    {
        _options = options;
        _writer = new(Path.Combine(resultDirectory, $"regime_shadow_trace_{executionId}.jsonl"));
        _summaryPath = Path.Combine(resultDirectory, $"regime_shadow_summary_{executionId}.json");
        _dualBaseline = dualBaseline?.Enabled == true
? new DualBaselineShadowObserver(
    dualBaseline,
    resultDirectory,
    executionId,
    experimentalIncident,
    experimentalAnomalyDecision)
            : null;
    }

    public static RegimeShadowObserver? TryCreate(
    RegimeShadowOptions options,
    string resultDirectory,
    string executionId,
    DualBaselineShadowOptions? dualBaseline = null,
    ExperimentalIncidentOptions? experimentalIncident = null,
    ExperimentalAnomalyDecisionOptions?
        experimentalAnomalyDecision = null)
{
    try
    {
        return options.Enabled
            ? new RegimeShadowObserver(
                options,
                resultDirectory,
                executionId,
                dualBaseline,
                experimentalIncident,
                experimentalAnomalyDecision)
            : null;
    }
    catch
    {
        return null;
    }
}

    public string TracePath => _writer.Path;
    public string? DualBaselineTracePath => _dualBaseline?.TracePath;
    public string? DualBaselineDecisionTracePath => _dualBaseline?.DecisionTracePath;
    public string? DualBaselineDecisionSummaryPath => _dualBaseline?.DecisionSummaryPath;
    public string? StableDeviationIncidentTracePath => _dualBaseline?.IncidentTracePath;
    public string? StableDeviationIncidentSummaryPath => _dualBaseline?.IncidentSummaryPath;
    public string? ExperimentalIncidentEventPath => _dualBaseline?.ExperimentalIncidentEventPath;
    public string? ExperimentalIncidentSummaryPath => _dualBaseline?.ExperimentalIncidentSummaryPath;
    public string? ExperimentalAnomalyDecisionTracePath => _dualBaseline?.ExperimentalAnomalyDecisionTracePath;
    public string? ExperimentalAnomalyDecisionSummaryPath => _dualBaseline?.ExperimentalAnomalyDecisionSummaryPath;
    public void ResetWindow(int index) { _store.ResetWindow(index); _dualBaseline?.ResetWindow(index); }
    public bool ShouldObserve(string tag) => _options.Enabled &&
        (_options.ObserveAllTags || _options.Tags.Contains(tag));

    public async ValueTask<ExperimentalAnomalyDecision?> ObserveAsync(
        int window,
        int minute,
        DetectorInput input,
        DetectorResult original)
    {
        if (!ShouldObserve(input.TagName))
            return null;
        try
        {
var current =
    _store.Get(
        input.ClienteId,
        input.TagName);

var shadowInput =
    CreateContiguousShadowInput(
        input);

var adaptation =
    _adapter.Adapt(
        minute,
        shadowInput,
        original,
        current.PolicyState,
        current.CandidateEvidence,
        _options);

var rawDecision =
    _engine.Evaluate(
        current.PolicyState,
        adaptation.Observation,
        adaptation.Evidence);
            var decision = RegimeShadowLifecycle.NormalizeDecision(
                current.PolicyState, rawDecision, adaptation.Evidence, adaptation.NextCandidateState);
            ExperimentalAnomalyDecision? experimentalDecision = null;

            if (_dualBaseline is not null)
            {
                experimentalDecision =
                    await _dualBaseline.ObserveAsync(
                        minute,
                        shadowInput,
                        original,
                        decision,
                        adaptation.Evidence.HasIncreasingNoiseEvidence);
            }

            var lifecycle = RegimeShadowLifecycle.Apply(current.PolicyState, decision, adaptation.NextCandidateState);
            var resetReason = lifecycle.ResetReason;
            var terminatedCandidateId = lifecycle.TerminatedCandidateId;
            _store.Set(input.ClienteId, input.TagName, new(decision.NextState, lifecycle.NextCandidateState));
            var audit = new Dictionary<string, object?>(adaptation.Audit)
            {
                ["hasMaterialTransitionEdge"] = adaptation.Evidence.HasMaterialTransitionEdge,
                ["hasLevelShiftConfirmation"] = adaptation.Evidence.HasLevelShiftConfirmation,
                ["hasSlowDriftEvidence"] = adaptation.Evidence.HasSlowDriftEvidence,
                ["isOutsideActiveRegime"] = adaptation.Evidence.IsOutsideActiveRegime,
                ["hasReturnedToActiveRegime"] = adaptation.Evidence.HasReturnedToActiveRegime,
                ["hasPersistentDeviation"] = adaptation.Evidence.HasPersistentDeviation,
                ["candidateFormationConfirmed"] = adaptation.Evidence.CandidateFormationConfirmed,
                ["candidateInvalidated"] = adaptation.Evidence.CandidateInvalidated,
                ["candidateStabilityReasons"] = adaptation.Evidence.CandidateStability.Reasons
            };
            var record = new RegimeShadowTraceRecord(window, minute, input.DataHoraAtual, input.TagName, input.ValorAtual,
                current.PolicyState.Lifecycle, decision.NextState.Lifecycle, current.PolicyState.Occurrence, decision.NextState.Occurrence,
                decision.Action, decision.Reasons.Select(x => x.Code.ToString()).ToArray(), adaptation.Evidence.CandidateStability.Status,
                adaptation.Evidence.CandidateStability.Confidence, adaptation.Evidence.CandidateStability.ObservedDurationMinutes,
                adaptation.Evidence.CandidateStability.IndependentSampleCount, adaptation.Evidence.Legitimacy.Kinds,
                decision.NextState.ActiveRegimeId, decision.NextState.CandidateRegimeId, original.EhAnomalia, original.Score,
                decision.CandidateWasPromoted, decision.CandidateWasRejected,
                adaptation.NextCandidateState.EpisodeId,
                adaptation.NextCandidateState.BufferStartedMinuteIndex,
                adaptation.NextCandidateState.BufferStartedTimestamp,
                adaptation.NextCandidateState.BufferInitialValue,
                adaptation.NextCandidateState.PointCountBeforeCurrentEpisode,
                resetReason ?? current.PendingResetReason,
                terminatedCandidateId,
                adaptation.Evidence.CandidateStability.DispersionIsStable,
                adaptation.Evidence.CandidateStability.ResidualSlopeIsStable,
                Value<double?>(audit, "candidateMedian"),
                Value<double?>(audit, "candidateMad"),
                Value<double?>(audit, "candidateRobustScale"),
                adaptation.Evidence.CandidateStability.HasCriticalGap,
                adaptation.NextCandidateState.FormationEvidenceSources ?? [],
                audit);
            Metrics.Add(record);
            _lastRecords[input.ClienteId + "\u001f" + input.TagName] = record;
            if (!_options.DecisionCounterfactualEnabled ||
                current.PolicyState.Lifecycle != decision.NextState.Lifecycle ||
                adaptation.Evidence.HasIncreasingNoiseEvidence ||
                decision.Action is RegimePolicyAction.RejectCandidate or
                    RegimePolicyAction.RestoreActiveRegime or
                    RegimePolicyAction.SuspendForDataQuality)
                await _writer.WriteAsync(record);

            return experimentalDecision;
        }
        catch (Exception ex)
        {
            Metrics.Error();
            await File.AppendAllTextAsync(
                _summaryPath + ".errors.jsonl",
                JsonSerializer.Serialize(new
                {
                    window,
                    minute,
                    input.TagName,
                    error = ex.ToString()
                }) + Environment.NewLine);

            // Fail-safe para a B3.16:
            // qualquer falha no shadow retorna null para permitir
            // LegacyFallback na camada funcional.
            return null;
        }
    }

    


    private static DetectorInput CreateContiguousShadowInput(
        DetectorInput input)
    {
        var history =
            input.Historico;

        if (history.Count < 2 ||
            !input.QualidadeHistorico.HasCriticalGap)
        {
            return input;
        }

        var cadence =
            input.QualidadeHistorico
                .MedianIntervalMinutes;

        if (!double.IsFinite(cadence) ||
            cadence <= 0)
        {
            return input;
        }

        var criticalGapMinutes =
            cadence * 5.0;

        var startIndex = 0;

        for (var index = 1;
             index < history.Count;
             index++)
        {
            var gapMinutes =
                (history[index].DataHora -
                 history[index - 1].DataHora)
                .TotalMinutes;

            if (double.IsFinite(gapMinutes) &&
                gapMinutes > criticalGapMinutes)
            {
                startIndex = index;
            }
        }

        if (startIndex <= 0)
        {
            return input;
        }

        var contiguousHistory =
            history
                .Skip(startIndex)
                .Select(point =>
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            point.DataHora,

                        Valor =
                            point.Valor
                    })
                .ToArray();

        /*
         * MantÃ©m a mesma polÃ­tica jÃ¡ utilizada
         * pelo runtime funcional:
         *
         * um gap histÃ³rico sÃ³ deixa de bloquear
         * a camada de regime depois de pelo menos
         * 10 pontos contÃ­nuos vÃ¡lidos.
         */
        if (contiguousHistory.Length < 10)
        {
            return input;
        }

        return new DetectorInput
        {
            ClienteId =
                input.ClienteId,

            TagName =
                input.TagName,

            TipoTag =
                input.TipoTag,

            ValorAtual =
                input.ValorAtual,

            DataHoraAtual =
                input.DataHoraAtual,

            MediaHistorica =
                input.MediaHistorica,

            DesvioPadraoHistorico =
                input.DesvioPadraoHistorico,

            Historico =
                contiguousHistory
        };
    }

    public async ValueTask FinalizeWindowAsync(int fastWindowIndex)
    {
        try
        {
            foreach (var state in _store.Snapshot())
            {
                if (!state.CandidateEvidence.Started) continue;
                var last = _lastRecords.Values.LastOrDefault(x => x.FastWindowIndex == fastWindowIndex && x.EpisodeId == state.CandidateEvidence.EpisodeId);
                if (last is null) continue;
                await _writer.WriteAsync(last with
                {
                    TransitionReasons = last.TransitionReasons.Concat(["FastWindowReset"]).ToArray(),
                    CandidateBufferResetReason = "FastWindowReset",
                    TerminatedCandidateId = state.PolicyState.CandidateRegimeId
                });
            }
            _store.FinalizeWindow();
            if (_dualBaseline is not null)
                await _dualBaseline.FinalizeWindowAsync(fastWindowIndex);
        }
        catch { Metrics.Error(); }
    }

    private static T? Value<T>(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) && value is T typed ? typed : default;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _writer.DisposeAsync();
            if (_dualBaseline is not null)
                await _dualBaseline.DisposeAsync();
            var summary = new
            {
                generatedAtUtc = DateTime.UtcNow,
                parameters = _options,
                metrics = new
                {
                    Metrics.Observations, Metrics.OriginalAnomaliesInTransition, Metrics.OriginalAnomaliesInCandidate,
                    Metrics.OriginalAnomaliesInStableUnresolved, Metrics.CandidatesOpened, Metrics.StableCandidates,
                    averageMinutesToStability = Metrics.MinutesToStability.Count == 0 ? (double?)null : Metrics.MinutesToStability.Average(),
                    Metrics.ReturnedToActive, Metrics.Rejected, Metrics.DataQualityBlocks, Metrics.ConfirmNewRegime,
                    Metrics.ActivateKnownRegime, Metrics.ShadowErrors
                }
            };
            await File.WriteAllTextAsync(_summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch { /* Shadow nunca interrompe o benchmark normal. */ }
    }
}
