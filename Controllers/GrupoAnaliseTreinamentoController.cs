using Microsoft.AspNetCore.Mvc;
using IAApi.Services.AnomalyDetection.Multivariate;

[ApiController]
[Route("api/grupos-analise")]
public sealed class GrupoAnaliseTreinamentoController : ControllerBase
{
    private readonly OperationalMultivariateTrainingService _trainingService;

    public GrupoAnaliseTreinamentoController(
        OperationalMultivariateTrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    /// <summary>
    /// Treina o modelo Mahalanobis do grupo usando histórico real do
    /// IHistoricoService no período solicitado.
    /// </summary>
    [HttpPost("{id:int}/treinar")]
    public async Task<ActionResult<OperationalMultivariateTrainingResult>> Treinar(
        int id,
        [FromBody] GrupoAnaliseTreinamentoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _trainingService.TreinarAsync(
                id,
                request.ClienteId,
                request.Inicio,
                request.Fim,
                cancellationToken);

            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { erro = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { erro = ex.Message });
        }
    }
}

public sealed class GrupoAnaliseTreinamentoRequest
{
    public string ClienteId { get; set; } = "";
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
}