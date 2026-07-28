using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/tags/historico")]
public class TagHistoricoController : ControllerBase
{
    private readonly IHistoricoService _historicoService;

    public TagHistoricoController(
        IHistoricoService historicoService)
    {
        _historicoService = historicoService;
    }

    [HttpGet("{clienteId}/{tagName}")]
    public IActionResult BuscarHistorico(
        string clienteId,
        string tagName,
        [FromQuery] int horas = 6)
    {
        try
        {
            const string tipoTag = "perfil-comportamental";

            var historico = _historicoService
                .BuscarHistorico(
                    clienteId,
                    tagName,
                    tipoTag)
                .Where(x =>
                    !float.IsNaN(x.Valor) &&
                    !float.IsInfinity(x.Valor))
                .OrderBy(x => x.DataHora)
                .ToList();

            if (historico.Count == 0)
            {
                return NotFound(new
                {
                    erro = "Nenhum histórico encontrado para esta tag."
                });
            }

            var inicio =
                DateTime.Now.AddHours(-horas);

            var leituras = historico
                .Where(x =>
                    x.DataHora >= inicio)
                .Select(x => new
                {
                    clienteId,
                    tagName,
                    dataHora = x.DataHora,
                    valor = x.Valor
                })
                .ToList();

            // Caso não existam leituras nas últimas horas,
            // devolve as últimas 20 disponíveis.
            if (leituras.Count == 0)
            {
                leituras = historico
                    .TakeLast(20)
                    .Select(x => new
                    {
                        clienteId,
                        tagName,
                        dataHora = x.DataHora,
                        valor = x.Valor
                    })
                    .ToList();
            }

            return Ok(new
            {
                clienteId,
                tagName,
                horas,
                quantidadeLeituras = leituras.Count,
                historico = leituras
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                erro = ex.Message
            });
        }
    }
}