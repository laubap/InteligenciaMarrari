using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/grupos-analise")]
public sealed class MultivariateHistoricalBacktestController
    : ControllerBase
{
    private readonly OperationalMultivariateBacktestService
        _backtestService;

    public MultivariateHistoricalBacktestController(
        OperationalMultivariateBacktestService backtestService)
    {
        _backtestService =
            backtestService ??
            throw new ArgumentNullException(
                nameof(backtestService));
    }

    /// <summary>
    /// B3.24.2 / MV08.2C
    ///
    /// Executa um backtest histórico diagnóstico para um grupo
    /// multivariado operacional usando o modelo persistido atual.
    ///
    /// Este endpoint:
    /// - não treina modelo;
    /// - não altera modelo;
    /// - não altera readiness;
    /// - não habilita promoção;
    /// - não altera RuntimeDecision;
    /// - não compartilha o buffer temporal do runtime.
    /// </summary>
    [HttpPost(
        "cliente/{clienteId}/grupo/{groupName}/backtest")]
    [ProducesResponseType(
        typeof(OperationalMultivariateBacktestResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<
        OperationalMultivariateBacktestResult>>
        ExecutarAsync(
            string clienteId,
            string groupName,
            [FromBody]
            MultivariateHistoricalBacktestRequest request,
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

        if (request.Inicio == default ||
            request.Fim == default)
        {
            return BadRequest(
                new
                {
                    erro =
                        "Inicio e Fim são obrigatórios."
                });
        }

        if (request.Fim <= request.Inicio)
        {
            return BadRequest(
                new
                {
                    erro =
                        "Fim deve ser maior que Inicio."
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
                await _backtestService
                    .ExecutarAsync(
                        clienteId,
                        groupName,
                        request.Inicio,
                        request.Fim,
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

public sealed class MultivariateHistoricalBacktestRequest
{
    public DateTime Inicio { get; set; }

    public DateTime Fim { get; set; }

    public double? Threshold { get; set; } =
        75.0;
}
