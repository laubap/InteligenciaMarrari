using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Controller para consultar as relações de uma tag com processos e equipamentos.
// Ele busca as conexões da tag nos relacionamentos existentes e retorna
// listas resumidas de processos e equipamentos associados.
[ApiController]
[Route("api/tags")]
public class TagRelacoesController : ControllerBase
{
    private readonly AppDbContext _context;

    public TagRelacoesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{tagName}/relacoes")]
    // Endpoint: GET /api/tags/{tagName}/relacoes[?clienteId=...]
    // Retorna as relações da tag com processos e equipamentos.
    // P35: com clienteId, só as relações desse cliente. Sem clienteId:
    // - tag em um cliente só (ou sem relações): resposta igual à de antes;
    // - tag em mais de um cliente: 400, para não misturar clientes na resposta.
    public IActionResult BuscarRelacoes(string tagName, [FromQuery] string? clienteId = null)
    {
        var filtrarCliente = clienteId != null;

        if (!filtrarCliente)
        {
            var clientesDaTag = _context.ProcessoTagsIa
                .Where(x => x.TagName == tagName)
                .Select(x => x.ClienteId)
                .Distinct()
                .ToList()
                .Union(_context.EquipamentoTagsIa
                    .Where(x => x.TagName == tagName)
                    .Select(x => x.ClienteId)
                    .Distinct()
                    .ToList())
                .Count();

            if (clientesDaTag > 1)
            {
                return BadRequest(new
                {
                    erro = "A tag existe em mais de um cliente. Informe o cliente em ?clienteId=."
                });
            }
        }

        var processos = _context.ProcessoTagsIa
            .Include(x => x.ProcessoIa)
            .Where(x => x.TagName == tagName)
            .Where(x => !filtrarCliente || x.ClienteId == clienteId)
            .Select(x => new
            {
                id = x.ProcessoIaId,
                nome = x.ProcessoIa != null ? x.ProcessoIa.Nome : null,
                papelDaTag = x.PapelDaTag
            })
            .ToList();

        var equipamentos = _context.EquipamentoTagsIa
            .Include(x => x.EquipamentoIa)
            .Where(x => x.TagName == tagName)
            .Where(x => !filtrarCliente || x.ClienteId == clienteId)
            .Select(x => new
            {
                id = x.EquipamentoIaId,
                nome = x.EquipamentoIa != null ? x.EquipamentoIa.Nome : null,
                papelDaTag = x.PapelDaTag
            })
            .ToList();

        return Ok(new
        {
            tagName,
            processos,
            equipamentos
        });
    }
}