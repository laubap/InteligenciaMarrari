namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.9 — consulta somente-leitura do histórico persistido de rollout.
/// </summary>
public sealed class RuntimeRolloutHistoryService
{
    public const int DefaultLimit = 20;
    public const int MaximumLimit = 100;

    private readonly IRuntimeRolloutSnapshotReader _snapshotReader;

    public RuntimeRolloutHistoryService(
        IRuntimeRolloutSnapshotReader snapshotReader)
    {
        _snapshotReader = snapshotReader;
    }

    public Task<IReadOnlyList<RuntimeRolloutHistoryItemResponse>> GetLatestAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit =
            NormalizeLimit(limit);

        return _snapshotReader.GetLatestAsync(
            normalizedLimit,
            cancellationToken);
    }

    public static int NormalizeLimit(
        int? limit)
    {
        if (limit is null)
        {
            return DefaultLimit;
        }

        return Math.Clamp(
            limit.Value,
            1,
            MaximumLimit);
    }
}