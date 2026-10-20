using IAApi.Configuration.ExperimentalIncidents;
using IAApi.Application.Incidents.Experimental.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>Experimental, feature-flagged and strictly read-only Incident API.</summary>
[ApiController]
[Route("api/experimental/incidents")]
[Produces("application/json")]
public sealed class ExperimentalIncidentsController : ControllerBase
{
    private readonly IExperimentalIncidentQueryService _queries;
    private readonly ExperimentalIncidentApiOptions _options;

    public ExperimentalIncidentsController(IExperimentalIncidentQueryService queries,
        IOptions<ExperimentalIncidentApiOptions> options)
    {
        _queries = queries;
        _options = options.Value;
    }

    /// <summary>Experimental read-only paginated Incident list.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedIncidentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PagedIncidentResult> List(
        [FromQuery] ExperimentalIncidentListRequest request)
    {
        if (!_options.Enabled) return NotFound();
        try
        {
            var result = _queries.List(request);
            return result.IsValid ? Ok(result.Value) : BadRequest(new
                { error = result.ValidationError });
        }
        catch
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Experimental Incident read model is unavailable.");
        }
    }

    /// <summary>Experimental read-only Incident details with compact milestones.</summary>
    [HttpGet("{incidentId}")]
    [ProducesResponseType(typeof(IncidentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IncidentDetailsDto> Details(string incidentId)
    {
        if (!_options.Enabled) return NotFound();
        try
        {
            var result = _queries.Get(incidentId);
            return result is null ? NotFound() : Ok(result);
        }
        catch
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Experimental Incident read model is unavailable.");
        }
    }
}
