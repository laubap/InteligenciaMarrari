using IAApi.Services.AnomalyDetection.Runtime.Incidents.Experimental;
namespace IAApi.Benchmark.ExperimentalIncidents;

public readonly record struct IncidentStreamKey(
    string ExecutionScopeId, int FastWindow, string Tag);

public sealed record VersionedIncidentState(
    long Version, ExperimentalIncidentAggregateState State);

public interface IIncidentStateStore
{
    VersionedIncidentState? Load(IncidentStreamKey key);
    bool TryWrite(IncidentStreamKey key, long expectedVersion,
        ExperimentalIncidentAggregateState next);
    IReadOnlyCollection<KeyValuePair<IncidentStreamKey, VersionedIncidentState>> Snapshot();
    void Remove(IncidentStreamKey key);
    void Clear();
}

public sealed class InMemoryIncidentStateStore : IIncidentStateStore
{
    private readonly Dictionary<IncidentStreamKey, VersionedIncidentState> _states = [];

    public VersionedIncidentState? Load(IncidentStreamKey key) =>
        _states.TryGetValue(key, out var value) ? value : null;

    public bool TryWrite(IncidentStreamKey key, long expectedVersion,
        ExperimentalIncidentAggregateState next)
    {
        var current = Load(key);
        if ((current?.Version ?? 0) != expectedVersion) return false;
        var version = expectedVersion + 1;
        _states[key] = new(version, next with { Version = version });
        return true;
    }

    public IReadOnlyCollection<KeyValuePair<IncidentStreamKey, VersionedIncidentState>> Snapshot() =>
        _states.ToArray();
    public void Remove(IncidentStreamKey key) => _states.Remove(key);
    public void Clear() => _states.Clear();
}
