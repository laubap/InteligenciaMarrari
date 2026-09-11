using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// B3.24.2 / MV07.3
///
/// Endpoint somente de observabilidade do readiness multivariado.
/// Não altera decisão operacional e não habilita promoção.
/// </summary>
[ApiController]
[Route("api/grupos-analise")]
public sealed class MultivariateOperationalReadinessController
    : ControllerBase
{
    private readonly MultivariateOperationalReadinessService
        _service;

    private readonly IOptions<
        MultivariateOperationalReadinessOptions>
        _options;

    public MultivariateOperationalReadinessController(
        MultivariateOperationalReadinessService service,
        IOptions<MultivariateOperationalReadinessOptions> options)
    {
        _service =
            service ??
            throw new ArgumentNullException(
                nameof(service));

        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));
    }

    /// <summary>
    /// Explica se um grupo multivariado está apto a receber
    /// autoridade operacional e lista blockers/warnings.
    ///
    /// Este endpoint é somente leitura.
    /// </summary>
    [HttpGet(
        "cliente/{clienteId}/grupo/{groupName}/readiness")]
    public async Task<
        ActionResult<MultivariateOperationalReadinessResult>>
        Obter(
            string clienteId,
            string groupName,
            CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _service.AvaliarAsync(
                    clienteId,
                    groupName,
                    _options.Value,
                    cancellationToken:
                        cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    erro =
                        ex.Message
                });
        }
    }
}