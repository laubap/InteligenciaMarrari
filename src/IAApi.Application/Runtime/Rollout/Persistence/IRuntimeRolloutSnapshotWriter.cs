namespace IAApi.Services.AnomalyDetection.Runtime;

public interface IRuntimeRolloutSnapshotWriter
{
    Task SaveAsync(
        RuntimeRolloutSnapshotData snapshot,
        CancellationToken cancellationToken = default);
}
