namespace IAApi.Services.AnomalyDetection.Runtime;

public interface IRuntimeRolloutSnapshotReader
{
    Task<IReadOnlyList<RuntimeRolloutHistoryItemResponse>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
