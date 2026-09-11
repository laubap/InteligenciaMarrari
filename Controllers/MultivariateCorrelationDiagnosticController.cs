using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/diagnosticos-multivariados")]
public sealed class
    MultivariateCorrelationDiagnosticController
    : ControllerBase
{
    private readonly
        MultivariateCorrelationDiagnosticService
        _service;

    public MultivariateCorrelationDiagnosticController(
        MultivariateCorrelationDiagnosticService service)
    {
        _service =
            service ??
            throw new ArgumentNullException(
                nameof(service));
    }

    /// <summary>
    /// Diagnóstico temporário para descobrir pares de tags
    /// fortemente correlacionados no histórico operacional.
    ///
    /// Não cria grupo, não treina modelo e não altera o runtime.
    /// </summary>
    [HttpPost("correlacoes")]
    public ActionResult<
        MultivariateCorrelationDiagnosticResponse>
        Analisar(
            [FromBody]
            MultivariateCorrelationDiagnosticRequest request)
    {
        try
        {
            return Ok(
                _service.Analisar(
                    request));
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
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                new
                {
                    erro =
                        "Falha ao calcular " +
                        "correlações.",

                    detalhe =
                        ex.Message
                });
        }
    }
}
