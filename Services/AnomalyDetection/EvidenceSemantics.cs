namespace IAApi.Services.AnomalyDetection;

public enum DetectorEvidenceRole
{
    StatisticalEvidence = 0,
    TemporalConfirmation = 1
}

public enum UnderlyingEvidenceKind
{
    Unknown = 0,
    Mad = 1,
    DeadbandDeviation = 2,
    StandardDeviationFallback = 3
}

public enum TemporalConfirmationStatus
{
    NotObserved = 0,
    Pending = 1,
    Confirmed = 2,
    InterruptedByGap = 3
}

public sealed record TemporalConfirmationSnapshot
{
    public TemporalConfirmationStatus Status { get; init; }
    public UnderlyingEvidenceKind UnderlyingEvidenceKind { get; init; }
    public int ConsecutivePoints { get; init; }
    public double DurationMinutes { get; init; }
    public double RequiredDurationMinutes { get; init; }
    public int RequiredPoints { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? ObservedThrough { get; init; }
    public bool InterruptedByGap { get; init; }
    public double LargestObservedGapMinutes { get; init; }

    public bool IsActive =>
        Status is TemporalConfirmationStatus.Pending or
            TemporalConfirmationStatus.Confirmed;
}
