namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.7 — resumo consolidado de observação do rollout canary.
/// Somente leitura. Não altera decisões, persistência ou estado funcional.
/// </summary>
public sealed class RuntimeRolloutObservationSummaryResponse
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public bool RuntimeDecisionEnabled { get; init; }

    public bool CanaryOnly { get; init; }

    public string[] AllowedClientIds { get; init; } = Array.Empty<string>();

    public bool MetricsEndpointEnabled { get; init; }

    public RuntimeDecisionMetricsSnapshot Metrics { get; init; } = default!;

    public RuntimeRolloutReadinessResponse Readiness { get; init; } = default!;
}