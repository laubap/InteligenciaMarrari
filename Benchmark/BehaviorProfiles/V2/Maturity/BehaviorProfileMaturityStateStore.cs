using IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Maturity;

public sealed class BehaviorProfileMaturityStateStore
{
    private readonly Dictionary<string, BehaviorProfileTemporalState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public int? FastWindowIndex { get; private set; }

    public void ResetWindow(int fastWindowIndex)
    {
        _states.Clear();
        FastWindowIndex = fastWindowIndex;
    }

    public BehaviorProfileTemporalState Get(
        string clientId,
        string tag) =>
        _states.GetValueOrDefault(Key(clientId, tag)) ??
        BehaviorProfileTemporalState.Initial;

    public void Set(
        string clientId,
        string tag,
        BehaviorProfileTemporalState state) =>
        _states[Key(clientId, tag)] = state;

    private static string Key(
        string clientId,
        string tag) =>
        clientId + "\u001f" + tag;
}