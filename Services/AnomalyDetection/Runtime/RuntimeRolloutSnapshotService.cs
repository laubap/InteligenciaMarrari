using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.8 — persiste snapshots agregados do rollout no SQLite.
/// Persistência separada do histórico de anomalias.
/// </summary>
public sealed class RuntimeRolloutSnapshotService
{
    private readonly AppDbContext
        _context;

    private readonly RuntimeDecisionMetrics
        _metrics;

    private readonly IOptions<RuntimeAnomalyDecisionOptions>
        _runtimeOptions;

    private readonly IOptions<RuntimeRolloutReadinessOptions>
        _readinessOptions;

    private readonly RuntimeRolloutSnapshotFactory
        _factory;

    public RuntimeRolloutSnapshotService(
        AppDbContext context,
        RuntimeDecisionMetrics metrics,
        IOptions<RuntimeAnomalyDecisionOptions> runtimeOptions,
        IOptions<RuntimeRolloutReadinessOptions> readinessOptions,
        RuntimeRolloutSnapshotFactory factory)
    {
        _context = context;
        _metrics = metrics;
        _runtimeOptions = runtimeOptions;
        _readinessOptions = readinessOptions;
        _factory = factory;
    }

    public async Task<RuntimeRolloutSnapshot> SaveCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot =
            _factory.Create(
                _runtimeOptions.Value,
                _metrics.Snapshot(),
                _readinessOptions.Value,
                DateTimeOffset.UtcNow);

        _context.RuntimeRolloutSnapshots.Add(
            snapshot);

        await _context.SaveChangesAsync(
            cancellationToken);

        return snapshot;
    }
}