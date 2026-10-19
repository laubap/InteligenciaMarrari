namespace IAApi.Services.AnomalyDetection.Runtime;

public sealed class RuntimeRolloutHistoryItemResponse
{
    public long Id { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public bool RuntimeDecisionEnabled { get; init; }
    public bool CanaryOnly { get; init; }
    public string AllowedClientIdsJson { get; init; } = "[]";
    public string Status { get; init; } = "Warning";
    public bool ReadyToExpand { get; init; }
    public long TotalDecisions { get; init; }
    public double ObservationMinutes { get; init; }
    public double LegacyFallbackRate { get; init; }
    public double DataQualityUncertainRate { get; init; }
    public double CandidateMismatchRate { get; init; }
    public double StableUnresolvedContinuationRate { get; init; }
    public string ReasonsJson { get; init; } = "[]";
    public DateTimeOffset? FirstDecisionAtUtc { get; init; }
    public DateTimeOffset? LastDecisionAtUtc { get; init; }
}