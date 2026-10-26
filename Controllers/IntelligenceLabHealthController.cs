using IAApi.Infrastructure.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IAApi.Controllers;

/// <summary>
/// Consulta local de saude da coleta, destinada exclusivamente a ensaios de
/// desenvolvimento. Nao publica diagnosticos, leituras ou identificadores de tags.
/// Ausencia de configuracao, acesso externo e ambiente nao-development falham fechados.
/// </summary>
[ApiController]
[Route("api/intelligence-lab")]
public sealed class IntelligenceLabHealthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly BoundedDiagnosticBuffer? _buffer;

    public IntelligenceLabHealthController(
        IEnumerable<BoundedDiagnosticBuffer> buffers,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(buffers);
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _buffer = buffers.SingleOrDefault();
    }

    /// <summary>
    /// Retorna apenas contadores agregados. Requer Development, cliente loopback,
    /// IntelligenceLab:Enabled=true e uma chave de acesso definida fora do repositorio.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(IntelligenceLabHealthSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IntelligenceLabHealthSnapshot> GetHealth(
        [FromHeader(Name = IntelligenceLabLocalReadAccess.KeyHeader)] string? accessKey)
    {
        var denial = IntelligenceLabLocalReadAccess.DenyIfUnauthorized(
            this, _configuration, _environment, _buffer, accessKey);
        if (denial is not null)
        {
            return denial;
        }

        return Ok(_buffer!.Health.Snapshot(_buffer));
    }

}
