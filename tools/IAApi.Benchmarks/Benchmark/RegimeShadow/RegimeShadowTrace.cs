using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.RegimePolicy;

namespace IAApi.Benchmark.RegimeShadow;

public sealed record RegimeShadowTraceRecord(
    int FastWindowIndex, int MinuteIndex, DateTime Timestamp, string Tag, double CurrentValue,
    RegimeLifecycleState CurrentLifecycle, RegimeLifecycleState NextLifecycle,
    OccurrenceState CurrentOccurrence, OccurrenceState NextOccurrence,
    RegimePolicyAction Action, IReadOnlyList<string> TransitionReasons,
    CandidateStabilityStatus CandidateStabilityStatus, double CandidateStabilityConfidence,
    double CandidateObservedDurationMinutes, int CandidateIndependentSampleCount,
    LegitimacyEvidenceKind LegitimacyKinds, string ActiveRegimeId, string? CandidateRegimeId,
    bool OriginalFinalAnomaly, double OriginalFinalScore, bool CandidateWasPromoted,
    bool CandidateWasRejected,
    string? EpisodeId, long? CandidateBufferStartedMinuteIndex,
    DateTimeOffset? CandidateBufferStartedTimestamp, double? CandidateBufferInitialValue,
    int CandidateBufferPointCountBeforeCurrentEpisode, string? CandidateBufferResetReason,
    string? TerminatedCandidateId, bool DispersionIsStable, bool ResidualSlopeIsStable,
    double? CandidateMedian, double? CandidateMad, double? CandidateRobustScale,
    bool CandidateCriticalGap, IReadOnlyList<string> FormationEvidenceSources,
    IReadOnlyDictionary<string, object?> Evidence);

public sealed class RegimeShadowTraceWriter : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    public RegimeShadowTraceWriter(string path) { Path = path; _writer = new(path); }
    public string Path { get; }
    public Task WriteAsync(RegimeShadowTraceRecord record) => _writer.WriteLineAsync(JsonSerializer.Serialize(record, _json));
    public async ValueTask DisposeAsync()
    {
        try { await _writer.DisposeAsync(); } catch { /* Shadow nunca interrompe o benchmark. */ }
    }
}

public sealed class RegimeShadowMetrics
{
    public long Observations { get; private set; }
    public long OriginalAnomaliesInTransition { get; private set; }
    public long OriginalAnomaliesInCandidate { get; private set; }
    public long OriginalAnomaliesInStableUnresolved { get; private set; }
    public long CandidatesOpened { get; private set; }
    public long StableCandidates { get; private set; }
    public long ReturnedToActive { get; private set; }
    public long Rejected { get; private set; }
    public long DataQualityBlocks { get; private set; }
    public long ConfirmNewRegime { get; private set; }
    public long ActivateKnownRegime { get; private set; }
    public long ShadowErrors { get; private set; }
    public List<double> MinutesToStability { get; } = [];
    public void Add(RegimeShadowTraceRecord r)
    {
        Observations++;
        if (r.OriginalFinalAnomaly && r.NextLifecycle == RegimeLifecycleState.TransitionSuspected) OriginalAnomaliesInTransition++;
        if (r.OriginalFinalAnomaly && r.NextLifecycle == RegimeLifecycleState.CandidateRegime) OriginalAnomaliesInCandidate++;
        if (r.OriginalFinalAnomaly && r.NextLifecycle == RegimeLifecycleState.StableUnresolvedDeviation) OriginalAnomaliesInStableUnresolved++;
        if (r.Action == RegimePolicyAction.OpenCandidate) CandidatesOpened++;
        if (r.NextLifecycle == RegimeLifecycleState.StableUnresolvedDeviation && r.CurrentLifecycle != r.NextLifecycle) { StableCandidates++; MinutesToStability.Add(r.CandidateObservedDurationMinutes); }
        if (r.Action == RegimePolicyAction.RestoreActiveRegime) ReturnedToActive++;
        if (r.CandidateWasRejected) Rejected++;
        if (r.Action == RegimePolicyAction.SuspendForDataQuality) DataQualityBlocks++;
        if (r.Action == RegimePolicyAction.ConfirmNewRegime) ConfirmNewRegime++;
        if (r.Action == RegimePolicyAction.ActivateKnownRegime) ActivateKnownRegime++;
    }
    public void Error() => ShadowErrors++;
}
