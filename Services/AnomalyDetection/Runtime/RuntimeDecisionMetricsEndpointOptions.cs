namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.3 — controla a exposição do endpoint de métricas do runtime.
///
/// Enabled = false:
/// o endpoint responde 404.
///
/// Enabled = true:
/// o endpoint retorna o snapshot atual das métricas.
///
/// Isto não substitui autenticação/autorização.
/// É apenas um fail-safe de exposição enquanto a API ainda não possui auth.
/// </summary>
public sealed class RuntimeDecisionMetricsEndpointOptions
{
    public const string SectionName =
        "AnomalyRuntimeMetrics";

    public bool Enabled { get; init; }
}