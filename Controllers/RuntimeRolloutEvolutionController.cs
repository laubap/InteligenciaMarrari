using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/anomaly-runtime")]
public sealed class RuntimeRolloutEvolutionController : ControllerBase
{
    private readonly RuntimeRolloutEvolutionService
        _evolutionService;

    private readonly IOptions<RuntimeDecisionMetricsEndpointOptions>
        _endpointOptions;

    public RuntimeRolloutEvolutionController(
        RuntimeRolloutEvolutionService evolutionService,
        IOptions<RuntimeDecisionMetricsEndpointOptions> endpointOptions)
    {
        _evolutionService = evolutionService;
        _endpointOptions = endpointOptions;
    }

    [HttpGet("evolution")]
    public async Task<ActionResult<RuntimeRolloutEvolutionResponse>> GetEvolution(
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (!_endpointOptions.Value.Enabled)
        {
            return NotFound();
        }

        var response =
            await _evolutionService.GetEvolutionAsync(
                limit,
                cancellationToken);

        return Ok(response);
    }
}