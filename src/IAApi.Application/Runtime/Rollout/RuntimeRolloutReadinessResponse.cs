namespace IAApi.Services.AnomalyDetection.Runtime;

public sealed class RuntimeRolloutReadinessResponse
{
    public string Status { get; init; } = "Warning";

    public bool ReadyToExpand { get; init; }

    public long TotalDecisions { get; init; }

    public int MinimumDecisions { get; init; }

    public double ObservationMinutes { get; init; }

    public double MinimumObservationMinutes { get; init; }

    public double LegacyFallbackRate { get; init; }

    public double MaximumLegacyFallbackRate { get; init; }

    public double DataQualityUncertainRate { get; init; }

    public double MaximumDataQualityUncertainRate { get; init; }

    public double CandidateMismatchRate { get; init; }

    public double MaximumCandidateMismatchRate { get; init; }

    public double StableUnresolvedContinuationRate { get; init; }

    public double MaximumStableUnresolvedContinuationRate { get; init; }

    public DateTimeOffset? FirstDecisionAtUtc { get; init; }

    public DateTimeOffset? LastDecisionAtUtc { get; init; }

    public string[] Reasons { get; init; } = Array.Empty<string>();
}