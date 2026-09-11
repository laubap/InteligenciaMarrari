using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/anomaly-runtime")]
public sealed class RuntimeRolloutHistoryController : ControllerBase
{
    private readonly RuntimeRolloutHistoryService _historyService;
    private readonly IOptions<RuntimeDecisionMetricsEndpointOptions> _endpointOptions;

    public RuntimeRolloutHistoryController(
        RuntimeRolloutHistoryService historyService,
        IOptions<RuntimeDecisionMetricsEndpointOptions> endpointOptions)
    {
        _historyService = historyService;
        _endpointOptions = endpointOptions;
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<RuntimeRolloutHistoryItemResponse>>> GetHistory(
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (!_endpointOptions.Value.Enabled)
            return NotFound();

        var history = await _historyService.GetLatestAsync(limit, cancellationToken);

        return Ok(history);
    }
}