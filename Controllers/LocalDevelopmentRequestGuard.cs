using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace IAApi.Controllers;

/// <summary>
/// Regra comum para endpoints que so podem responder em desenvolvimento e
/// para a propria maquina: ambiente Development e pedido vindo de loopback.
/// Extraida de <see cref="IntelligenceLabLocalReadAccess"/> (que continua com a
/// mesma regra) para ser reaproveitada pelo DevController (P33).
/// Nao substitui autenticacao de usuarios nem autorizacao por cliente.
/// </summary>
internal static class LocalDevelopmentRequestGuard
{
    public static bool IsAllowed(HttpContext httpContext, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(environment);

        return environment.IsDevelopment() &&
            httpContext.Connection.RemoteIpAddress is { } remoteAddress &&
            IPAddress.IsLoopback(remoteAddress);
    }
}
