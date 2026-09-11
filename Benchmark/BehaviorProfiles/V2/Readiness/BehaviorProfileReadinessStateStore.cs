namespace IAApi.Benchmark.BehaviorProfiles.V2.Readiness;

public sealed class BehaviorProfileReadinessStateStore
{
    private readonly Dictionary<string, BehaviorProfileReadinessState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public BehaviorProfileReadinessState Get(string client, string tag) =>
        _states.TryGetValue($"{client}\u001f{tag}", out var state) ? state : new();

    public void Set(string client, string tag, BehaviorProfileReadinessState state) =>
        _states[$"{client}\u001f{tag}"] = state;

    public void ResetWindow() => _states.Clear();
}
