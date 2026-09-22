using ServiceBehaviorProfileReadinessState =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness.BehaviorProfileReadinessState;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Readiness;

public sealed class BehaviorProfileReadinessStateStore
{
    private readonly Dictionary<string, ServiceBehaviorProfileReadinessState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public ServiceBehaviorProfileReadinessState Get(
        string clientId,
        string tag) =>
        _states.GetValueOrDefault(Key(clientId, tag)) ??
        new ServiceBehaviorProfileReadinessState();

    public void Set(
        string clientId,
        string tag,
        ServiceBehaviorProfileReadinessState state) =>
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