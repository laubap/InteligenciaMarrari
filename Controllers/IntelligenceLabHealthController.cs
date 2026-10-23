using System.Net;
using System.Security.Cryptography;
using System.Text;
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
    private const string KeyHeader = "X-Intelligence-Lab-Key";
    private const int MinimumKeyLength = 32;
    private const int MaximumProvidedKeyLength = 256;

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
        [FromHeader(Name = KeyHeader)] string? accessKey)
    {
        Response.Headers["Cache-Control"] = "no-store";

        if (!_environment.IsDevelopment() ||
            !_configuration.GetValue<bool>("IntelligenceLab:Enabled") ||
            _buffer is not { Enabled: true } ||
            HttpContext.Connection.RemoteIpAddress is not { } remoteAddress ||
            !IPAddress.IsLoopback(remoteAddress))
        {
            return NotFound();
        }

        var expectedKey = _configuration["IntelligenceLab:ReadAccessKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            expectedKey.Length < MinimumKeyLength)
        {
            // Sem chave explicitamente configurada, endpoint indisponivel.
            return NotFound();
        }

        if (string.IsNullOrEmpty(accessKey) ||
            accessKey.Length > MaximumProvidedKeyLength ||
            !FixedTimeKeyEquals(expectedKey, accessKey))
        {
            return Unauthorized();
        }

        return Ok(_buffer.Health.Snapshot(_buffer));
    }

    private static bool FixedTimeKeyEquals(string expected, string provided)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }
}
