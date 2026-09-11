using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/grupos-analise")]
public sealed class MultivariateOperationalQualificationController
    : ControllerBase
{
    private readonly OperationalMultivariateQualificationService
        _qualificationService;

    public MultivariateOperationalQualificationController(
        OperationalMultivariateQualificationService qualificationService)
    {
        _qualificationService =
            qualificationService ??
            throw new ArgumentNullException(
                nameof(qualificationService));
    }

    /// <summary>
    /// B3.24.2 / MV08.4B
    ///
    /// Executa a qualificação diagnóstica de um grupo multivariado
    /// usando uma janela de referência e um holdout posterior.
    ///
    /// MV08.4D2A: a validação industrial NÃO é recebida como boolean
    /// manual; ela é derivada das revisões persistidas dos eventos.
    ///
    /// O endpoint NÃO:
    /// - adiciona o grupo em ApprovedGroups;
    /// - altera readiness;
    /// - habilita promoção;
    /// - altera RuntimeDecision;
    /// - treina ou sobrescreve modelo.
    /// </summary>
    [HttpPost(
        "cliente/{clienteId}/grupo/{groupName}/qualification")]
    [ProducesResponseType(
        typeof(OperationalMultivariateQualificationRunResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<
        OperationalMultivariateQualificationRunResult>>
        QualificarAsync(
            string clienteId,
            string groupName,
            [FromBody]
            MultivariateOperationalQualificationRequest request,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                clienteId))
        {
            return BadRequest(
                new
                {
                    erro =
                        "ClienteId é obrigatório."
                });
        }

        if (string.IsNullOrWhiteSpace(
                groupName))
        {
            return BadRequest(
                new
                {
                    erro =
                        "GroupName é obrigatório."
                });
        }

        if (request is null)
        {
            return BadRequest(
                new
                {
                    erro =
                        "O corpo da requisição é obrigatório."
                });
        }

        if (request.ReferenciaInicio == default ||
            request.ReferenciaFim == default ||
            request.HoldoutInicio == default ||
            request.HoldoutFim == default)
        {
            return BadRequest(
                new
                {
                    erro =
                        "As quatro datas de referência e holdout são obrigatórias."
                });
        }

        if (request.ReferenciaFim <=
            request.ReferenciaInicio)
        {
            return BadRequest(
                new
                {
                    erro =
                        "ReferenciaFim deve ser maior que ReferenciaInicio."
                });
        }

        if (request.HoldoutFim <=
            request.HoldoutInicio)
        {
            return BadRequest(
                new
                {
                    erro =
                        "HoldoutFim deve ser maior que HoldoutInicio."
                });
        }

        if (request.HoldoutInicio <
            request.ReferenciaFim)
        {
            return BadRequest(
                new
                {
                    erro =
                        "A janela de holdout não pode sobrepor a janela de referência."
                });
        }

        var threshold =
            request.Threshold ??
            75.0;

        if (!double.IsFinite(
                threshold) ||
            threshold <= 0 ||
            threshold >= 100)
        {
            return BadRequest(
                new
                {
                    erro =
                        "Threshold deve estar no intervalo (0, 100)."
                });
        }

        try
        {
            var result =
                await _qualificationService
                    .QualificarAsync(
                        clienteId,
                        groupName,
                        request.ReferenciaInicio,
                        request.ReferenciaFim,
                        request.HoldoutInicio,
                        request.HoldoutFim,
                        threshold,
                        cancellationToken);

            return Ok(
                result);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(
                new
                {
                    erro =
                        exception.Message
                });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new
                {
                    erro =
                        exception.Message
                });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    erro =
                        exception.Message
                });
        }
    }
}

public sealed class MultivariateOperationalQualificationRequest
{
    public DateTime ReferenciaInicio { get; set; }

    public DateTime ReferenciaFim { get; set; }

    public DateTime HoldoutInicio { get; set; }

    public DateTime HoldoutFim { get; set; }

    public double? Threshold { get; set; } =
        75.0;

}
