using Microsoft.Extensions.Options;

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

    private readonly IOptions<RuntimeAnomalyDecisionOptions>
        _runtimeOptions;

    private readonly IOptions<RuntimeRolloutReadinessOptions>
        _readinessOptions;

    private readonly RuntimeRolloutSnapshotFactory
        _factory;

    public RuntimeRolloutSnapshotService(
        IRuntimeRolloutSnapshotWriter snapshotWriter,
        RuntimeDecisionMetrics metrics,
        IOptions<RuntimeAnomalyDecisionOptions> runtimeOptions,
        IOptions<RuntimeRolloutReadinessOptions> readinessOptions,
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
                _runtimeOptions.Value,
                _metrics.Snapshot(),
                _readinessOptions.Value,
                DateTimeOffset.UtcNow);

        await _snapshotWriter.SaveAsync(
            snapshot,
            cancellationToken);

        return snapshot;
    }
}