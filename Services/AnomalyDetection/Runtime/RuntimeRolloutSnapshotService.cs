namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.8 — persiste snapshots agregados do rollout.
/// Persistência separada do histórico de anomalias.
/// </summary>
public sealed class RuntimeRolloutSnapshotService
{
    private readonly IRuntimeRolloutSnapshotWriter
        _snapshotWriter;

    private readonly RuntimeDecisionMetrics
        _metrics;

    private readonly RuntimeAnomalyDecisionOptions
        _runtimeOptions;

    private readonly RuntimeRolloutReadinessOptions
        _readinessOptions;

    private readonly RuntimeRolloutSnapshotFactory
        _factory;

    public RuntimeRolloutSnapshotService(
        IRuntimeRolloutSnapshotWriter snapshotWriter,
        RuntimeDecisionMetrics metrics,
        RuntimeAnomalyDecisionOptions runtimeOptions,
        RuntimeRolloutReadinessOptions readinessOptions,
        RuntimeRolloutSnapshotFactory factory)
    {
        _snapshotWriter = snapshotWriter;
        _metrics = metrics;
        _runtimeOptions = runtimeOptions;
        _readinessOptions = readinessOptions;
        _factory = factory;
    }

    public async Task<RuntimeRolloutSnapshotData> SaveCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot =
            _factory.Create(
                _runtimeOptions,
                _metrics.Snapshot(),
                _readinessOptions,
                DateTimeOffset.UtcNow);

        await _snapshotWriter.SaveAsync(
            snapshot,
            cancellationToken);

        return snapshot;
    }
}