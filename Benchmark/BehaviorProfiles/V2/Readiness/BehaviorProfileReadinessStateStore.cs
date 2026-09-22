using IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Readiness;

public sealed class BehaviorProfileReadinessStateStore
{
    private readonly Dictionary<string, BehaviorProfileReadinessState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public BehaviorProfileReadinessState Get(
        string clientId,
        string tag) =>
        _states.GetValueOrDefault(Key(clientId, tag)) ??
        new BehaviorProfileReadinessState();

    public void Set(
        string clientId,
        string tag,
        BehaviorProfileReadinessState state) =>
        _states[Key(clientId, tag)] = state;

    public void ResetWindow()
    {
        _states.Clear();
    }

    private static string Key(
        string clientId,
        string tag) =>
        clientId + "\u001f" + tag;
}