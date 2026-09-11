namespace IAApi.Services.AnomalyDetection.Runtime;

public sealed class RuntimeRolloutEvolutionResponse
{
    public int SnapshotCount { get; init; }

    public long? OldestSnapshotId { get; init; }

    public long? LatestSnapshotId { get; init; }

    public long ObservedDecisionGrowth { get; init; }

    public double ObservedObservationMinutesGrowth { get; init; }

    public int ProcessRestartCount { get; init; }

    public int ObservationSessionCount { get; init; }

    public string CurrentStatus { get; init; } = "Unknown";

    public bool CurrentReadyToExpand { get; init; }

    public DateTimeOffset? FirstReadyToExpandAtUtc { get; init; }

    public string LegacyFallbackTrend { get; init; } = "Stable";

    public string DataQualityUncertainTrend { get; init; } = "Stable";

    public string CandidateMismatchTrend { get; init; } = "Stable";

    public string StableUnresolvedContinuationTrend { get; init; } = "Stable";

    public RuntimeRolloutEvolutionItemResponse[] Changes { get; init; }
        = Array.Empty<RuntimeRolloutEvolutionItemResponse>();
}