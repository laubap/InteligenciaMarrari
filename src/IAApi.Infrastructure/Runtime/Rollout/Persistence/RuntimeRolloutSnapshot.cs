namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.8 — snapshot persistido da observação/readiness do rollout.
/// Mantém apenas dados agregados de rollout.
/// </summary>
public sealed class RuntimeRolloutSnapshot
{
    public long Id { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public bool RuntimeDecisionEnabled { get; set; }

    public bool CanaryOnly { get; set; }

    public string AllowedClientIdsJson { get; set; } = "[]";

    public string Status { get; set; } = "Warning";

    public bool ReadyToExpand { get; set; }

    public long TotalDecisions { get; set; }

    public double ObservationMinutes { get; set; }

    public double LegacyFallbackRate { get; set; }

    public double DataQualityUncertainRate { get; set; }

    public double CandidateMismatchRate { get; set; }

    public double StableUnresolvedContinuationRate { get; set; }

    public string ReasonsJson { get; set; } = "[]";

    public DateTimeOffset? FirstDecisionAtUtc { get; set; }

    public DateTimeOffset? LastDecisionAtUtc { get; set; }
}