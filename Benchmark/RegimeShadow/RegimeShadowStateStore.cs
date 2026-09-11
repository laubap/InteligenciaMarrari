using IAApi.RegimePolicy;

namespace IAApi.Benchmark.RegimeShadow;

public sealed class RegimeShadowStateStore
{
    private readonly Dictionary<string, RegimeShadowTagState> _states = new(StringComparer.OrdinalIgnoreCase);
    private int _window;

    public void ResetWindow(int fastWindowIndex) { _window = fastWindowIndex; _states.Clear(); }
    public RegimeShadowTagState Get(string client, string tag) => _states.TryGetValue(Key(client, tag), out var value)
        ? value : new(RegimePolicyState.Initial($"active:{_window}:{tag}"), CandidateEvidenceState.Empty, "FastWindowReset");
    public void Set(string client, string tag, RegimeShadowTagState state) => _states[Key(client, tag)] = state;
    public IReadOnlyCollection<RegimeShadowTagState> Snapshot() => _states.Values.ToArray();
    public void FinalizeWindow()
    {
        foreach (var key in _states.Keys.ToArray())
            _states[key] = _states[key] with { CandidateEvidence = CandidateEvidenceState.Empty, PendingResetReason = "FastWindowReset" };
    }
    private static string Key(string client, string tag) => client + "\u001f" + tag;
}
