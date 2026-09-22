using ServiceBehaviorProfileTemporalState =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity.BehaviorProfileTemporalState;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Maturity;

public sealed class BehaviorProfileMaturityStateStore
{
    private readonly Dictionary<string, ServiceBehaviorProfileTemporalState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public int? FastWindowIndex { get; private set; }

    public void ResetWindow(int fastWindowIndex)
    {
        _states.Clear();
        FastWindowIndex = fastWindowIndex;
    }

    public ServiceBehaviorProfileTemporalState Get(
        string clientId,
        string tag) =>
        _states.GetValueOrDefault(Key(clientId, tag)) ??
        ServiceBehaviorProfileTemporalState.Initial;

    public void Set(
        string clientId,
        string tag,
        ServiceBehaviorProfileTemporalState state) =>
        _states[Key(clientId, tag)] = state;

    private static string Key(
        string clientId,
        string tag) =>
        clientId + "\u001f" + tag;
}