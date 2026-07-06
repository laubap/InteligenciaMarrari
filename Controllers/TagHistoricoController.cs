using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/tags/historico")]
public class TagHistoricoController : ControllerBase
{
    private readonly IHistoricoService _historicoService;

    public TagHistoricoController(IHistoricoService historicoService)
    {
        _historicoService = historicoService;
    }

    [HttpGet]
    public IActionResult BuscarHistorico(
        [FromQuery] string tagName,
        [FromQuery] int horas = 6)
    {
        try
        {
            var historico = _historicoService
                .BuscarHistorico(tagName, "perfil-comportamental")
                .Where(x => !float.IsNaN(x.Valor))
                .OrderBy(x => x.DataHora)
                .ToList();

            var inicio = DateTime.Now.AddHours(-horas);

            var leituras = historico
                .Where(x => x.DataHora >= inicio)
                .Select(x => new
                {
                    x.DataHora,
                    Valor = x.Valor
                })
                .ToList();

            return Ok(leituras);
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