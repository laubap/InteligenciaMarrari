namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.4 — visão consolidada do rollout operacional do runtime decision.
/// Não contém dados de tags, valores de processo ou histórico por cliente.
/// </summary>
public sealed class RuntimeRolloutStatusResponse
{
    public bool RuntimeDecisionEnabled { get; init; }

    public bool CanaryOnly { get; init; }

    public string[] AllowedClientIds { get; init; } = Array.Empty<string>();

    public bool MetricsEndpointEnabled { get; init; }

    public RuntimeDecisionMetricsSnapshot Metrics { get; init; } = default!;
}