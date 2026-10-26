using System.Net;
using System.Security.Cryptography;
using System.Text;
using IAApi.Infrastructure.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IAApi.Controllers;

/// <summary>
/// Guarda unica para consultas locais do Intelligence Lab. Nao substitui
/// autenticacao de usuarios, autorizacao por tenant ou protecao de producao.
/// Nunca usar estes endpoints atras de um proxy acessivel remotamente.
/// </summary>
internal static class IntelligenceLabLocalReadAccess
{
    public const string KeyHeader = "X-Intelligence-Lab-Key";
    private const int MinimumKeyLength = 32;
    private const int MaximumProvidedKeyLength = 256;

    public static ActionResult? DenyIfUnauthorized(
        ControllerBase controller,
        IConfiguration configuration,
        IHostEnvironment environment,
        BoundedDiagnosticBuffer? buffer,
        string? accessKey)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        controller.Response.Headers["Cache-Control"] = "no-store";

        if (!environment.IsDevelopment() ||
            !configuration.GetValue<bool>("IntelligenceLab:Enabled") ||
            buffer is not { Enabled: true } ||
            controller.HttpContext.Connection.RemoteIpAddress is not { } remoteAddress ||
            !IPAddress.IsLoopback(remoteAddress))
        {
            return new NotFoundResult();
        }

        var expectedKey = configuration["IntelligenceLab:ReadAccessKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            expectedKey.Length < MinimumKeyLength)
        {
            return new NotFoundResult();
        }

        if (string.IsNullOrEmpty(accessKey) ||
            accessKey.Length > MaximumProvidedKeyLength ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey)),
                SHA256.HashData(Encoding.UTF8.GetBytes(accessKey))))
        {
            return new UnauthorizedResult();
        }

        return null;
    }
}
