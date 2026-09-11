namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.6 — critérios objetivos para avaliar a saúde do rollout.
/// Nenhum destes limites altera a decisão do detector.
/// </summary>
public sealed class RuntimeRolloutReadinessOptions
{
    public const string SectionName =
        "AnomalyRuntimeReadiness";

    public int MinimumDecisions { get; init; } = 20;

    public double MinimumObservationMinutes { get; init; } = 30;

    public double MaximumLegacyFallbackRate { get; init; } = 0.05;

    public double MaximumDataQualityUncertainRate { get; init; } = 0.20;

    public double MaximumCandidateMismatchRate { get; init; } = 0.25;

    public double MaximumStableUnresolvedContinuationRate { get; init; } = 0.60;
}