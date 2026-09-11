namespace IAApi.Services.AnomalyDetection.Runtime;

public sealed class RuntimeRolloutEvolutionItemResponse
{
    public long PreviousSnapshotId { get; init; }

    public long CurrentSnapshotId { get; init; }

    public DateTimeOffset PreviousGeneratedAtUtc { get; init; }

    public DateTimeOffset CurrentGeneratedAtUtc { get; init; }

    public bool RestartBoundary { get; init; }

    public long? DecisionDelta { get; init; }

    public double? ObservationMinutesDelta { get; init; }

    public double LegacyFallbackRateDelta { get; init; }

    public double DataQualityUncertainRateDelta { get; init; }

    public double CandidateMismatchRateDelta { get; init; }

    public double StableUnresolvedContinuationRateDelta { get; init; }

    public string PreviousStatus { get; init; } = "Warning";

    public string CurrentStatus { get; init; } = "Warning";

    public bool StatusChanged { get; init; }

    public bool BecameReadyToExpand { get; init; }
}