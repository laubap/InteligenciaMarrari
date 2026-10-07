using IAApi.Services.AnomalyDetection.Runtime.Incidents.StableDeviation;
namespace IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

public sealed class StableDeviationIncidentStateStore
{
    private readonly Dictionary<string, StableDeviationIncidentState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public StableDeviationIncidentState? Get(string tag) =>
        _states.TryGetValue(tag, out var state) ? state : null;
    public void Set(string tag, StableDeviationIncidentState state) => _states[tag] = state;
    public bool Remove(string tag, out StableDeviationIncidentState state) =>
        _states.Remove(tag, out state!);
    public IReadOnlyCollection<StableDeviationIncidentState> Snapshot() => _states.Values.ToArray();
    public void Clear() => _states.Clear();
    public int Count => _states.Count;
}

