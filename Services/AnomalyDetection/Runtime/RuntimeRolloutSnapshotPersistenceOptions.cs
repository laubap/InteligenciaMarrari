namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.8 — configuração da persistência periódica de snapshots de rollout.
/// </summary>
public sealed class RuntimeRolloutSnapshotPersistenceOptions
{
    public const string SectionName =
        "AnomalyRuntimeSnapshotPersistence";

    public bool Enabled { get; init; } = false;

    public int IntervalMinutes { get; init; } = 5;
}