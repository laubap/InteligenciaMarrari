using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/processos")]
public class ProcessoIaController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProcessoIaController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public IActionResult Salvar([FromBody] ProcessoIaRequest request)
    {
        var processo = _context.ProcessosIa
            .Include(x => x.Tags)
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.Nome == request.Nome);

        if (processo == null)
        {
            processo = new ProcessoIa
            {
                ClienteId = request.ClienteId,
                Nome = request.Nome,
                DataCriacao = DateTime.Now
            };

            _context.ProcessosIa.Add(processo);
        }

        processo.Descricao = request.Descricao;
        processo.Area = request.Area;
        processo.Criticidade = request.Criticidade;
        processo.Objetivo = request.Objetivo;
        processo.CondicaoNormal = request.CondicaoNormal;
        processo.ConsequenciasFalha = request.ConsequenciasFalha;
        processo.ProcedimentoRecomendado = request.ProcedimentoRecomendado;
        processo.DataAtualizacao = DateTime.Now;

        using var transaction = _context.Database.BeginTransaction();

        try
        {
            _context.SaveChanges();

            var tagsSolicitadas =
                request.Tags ?? new List<ProcessoTagRequest>();

            var duplicadasNoMesmoProcesso =
                tagsSolicitadas
                    .Where(x => !string.IsNullOrWhiteSpace(x.TagName))
                    .GroupBy(
                        x => x.TagName.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Where(x => x.Count() > 1)
                    .Select(x => x.Key)
                    .ToList();

            if (duplicadasNoMesmoProcesso.Count > 0)
            {
                transaction.Rollback();

                return BadRequest(new
                {
                    erro =
                        "A mesma tag não pode ser enviada mais de uma vez no mesmo processo.",
                    tags = duplicadasNoMesmoProcesso
                });
            }

            var tagsAtuais = _context.ProcessoTagsIa
                .Where(x => x.ProcessoIaId == processo.Id)
                .ToList();

            _context.ProcessoTagsIa.RemoveRange(tagsAtuais);
            _context.SaveChanges();

            foreach (var tag in tagsSolicitadas)
            {
                var tagName = tag.TagName?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(tagName))
                {
                    transaction.Rollback();

                    return BadRequest(new
                    {
                        erro = "O nome da tag é obrigatório."
                    });
                }

                if (tag.Principal)
                {
                    var principaisAnteriores =
                        _context.ProcessoTagsIa
                            .Where(x =>
                                x.ClienteId == request.ClienteId &&
                                x.TagName == tagName &&
                                x.Principal)
                            .ToList();

                    foreach (var anterior in principaisAnteriores)
                    {
                        anterior.Principal = false;
                    }
                }

                _context.ProcessoTagsIa.Add(new ProcessoTagIa
                {
                    ProcessoIaId = processo.Id,
                    ClienteId = request.ClienteId,
                    TagName = tagName,
                    PapelDaTag = tag.PapelDaTag,
                    Principal = tag.Principal
                });
            }

            _context.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return Ok(new
        {
            mensagem = "Processo salvo com sucesso.",
            processo.Id,
            processo.ClienteId,
            processo.Nome,
            totalTags = request.Tags?.Count ?? 0
        });
    }

    [HttpGet("{clienteId}")]
    public IActionResult ListarPorCliente(string clienteId)
    {
        var processos = _context.ProcessosIa
            .Include(x => x.Tags)
            .Where(x => x.ClienteId == clienteId)
            .ToList();

        return Ok(processos);
    }
}

public class ProcessoIaRequest
{
    public string ClienteId { get; set; } = "";
    public string Nome { get; set; } = "";

    public string? Descricao { get; set; }
    public string? Area { get; set; }
    public string? Criticidade { get; set; }
    public string? Objetivo { get; set; }
    public string? CondicaoNormal { get; set; }
    public string? ConsequenciasFalha { get; set; }
    public string? ProcedimentoRecomendado { get; set; }

    public List<ProcessoTagRequest>? Tags { get; set; }
}

public class ProcessoTagRequest
{
    public string TagName { get; set; } = "";
    public string? PapelDaTag { get; set; }

    public bool Principal { get; set; }
}