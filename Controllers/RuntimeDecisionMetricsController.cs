using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/anomaly-runtime")]
public sealed class RuntimeDecisionMetricsController : ControllerBase
{
    private readonly RuntimeDecisionMetrics
        _metrics;

    private readonly IOptions<RuntimeDecisionMetricsEndpointOptions>
        _metricsEndpointOptions;

    private readonly IOptions<RuntimeAnomalyDecisionOptions>
        _runtimeDecisionOptions;

    private readonly IOptions<RuntimeRolloutReadinessOptions>
        _readinessOptions;

    private readonly RuntimeRolloutReadinessEvaluator
        _readinessEvaluator;

    public RuntimeDecisionMetricsController(
        RuntimeDecisionMetrics metrics,
        IOptions<RuntimeDecisionMetricsEndpointOptions> metricsEndpointOptions,
        IOptions<RuntimeAnomalyDecisionOptions> runtimeDecisionOptions,
        IOptions<RuntimeRolloutReadinessOptions> readinessOptions,
        RuntimeRolloutReadinessEvaluator readinessEvaluator)
    {
        _metrics = metrics;
        _metricsEndpointOptions = metricsEndpointOptions;
        _runtimeDecisionOptions = runtimeDecisionOptions;
        _readinessOptions = readinessOptions;
        _readinessEvaluator = readinessEvaluator;
    }

    [HttpGet("metrics")]
    public ActionResult<RuntimeDecisionMetricsSnapshot> GetMetrics()
    {
        if (!_metricsEndpointOptions.Value.Enabled)
        {
            return NotFound();
        }

        return Ok(
            _metrics.Snapshot());
    }

    [HttpGet("status")]
    public ActionResult<RuntimeRolloutStatusResponse> GetStatus()
    {
        if (!_metricsEndpointOptions.Value.Enabled)
        {
            return NotFound();
        }

        var runtimeOptions =
            _runtimeDecisionOptions.Value;

        var response =
            new RuntimeRolloutStatusResponse
            {
                RuntimeDecisionEnabled =
                    runtimeOptions.Enabled,

                CanaryOnly =
                    runtimeOptions.CanaryOnly,

                AllowedClientIds =
                    NormalizeAllowedClientIds(
                        runtimeOptions.AllowedClientIds),

                MetricsEndpointEnabled =
                    _metricsEndpointOptions.Value.Enabled,

                Metrics =
                    _metrics.Snapshot()
            };

        return Ok(response);
    }

    [HttpGet("readiness")]
    public ActionResult<RuntimeRolloutReadinessResponse> GetReadiness()
    {
        if (!_metricsEndpointOptions.Value.Enabled)
        {
            return NotFound();
        }

        var response =
            _readinessEvaluator.Evaluate(
                _runtimeDecisionOptions.Value,
                _metrics.Snapshot(),
                _readinessOptions.Value);

        return Ok(response);
    }

    [HttpGet("observation")]
    public ActionResult<RuntimeRolloutObservationSummaryResponse> GetObservation()
    {
        if (!_metricsEndpointOptions.Value.Enabled)
        {
            return NotFound();
        }

        var runtimeOptions =
            _runtimeDecisionOptions.Value;

        // Um único snapshot é reutilizado em toda a resposta para evitar
        // divergência entre métricas e readiness durante a mesma requisição.
        var metricsSnapshot =
            _metrics.Snapshot();

        var readiness =
            _readinessEvaluator.Evaluate(
                runtimeOptions,
                metricsSnapshot,
                _readinessOptions.Value);

        var response =
            new RuntimeRolloutObservationSummaryResponse
            {
                GeneratedAtUtc =
                    DateTimeOffset.UtcNow,

                RuntimeDecisionEnabled =
                    runtimeOptions.Enabled,

                CanaryOnly =
                    runtimeOptions.CanaryOnly,

                AllowedClientIds =
                    NormalizeAllowedClientIds(
                        runtimeOptions.AllowedClientIds),

                MetricsEndpointEnabled =
                    _metricsEndpointOptions.Value.Enabled,

                Metrics =
                    metricsSnapshot,

                Readiness =
                    readiness
            };

        return Ok(response);
    }

    private static string[] NormalizeAllowedClientIds(
        string[]? allowedClientIds)
    {
        return allowedClientIds
            ?.Where(clientId =>
                !string.IsNullOrWhiteSpace(clientId))
            .Select(clientId =>
                clientId.Trim())
            .ToArray()
        ?? Array.Empty<string>();
    }
}