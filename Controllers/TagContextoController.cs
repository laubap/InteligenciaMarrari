using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/tags/contexto")]
public class TagContextoController : ControllerBase
{
    private readonly AppDbContext _context;

    public TagContextoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public IActionResult SalvarContexto([FromBody] TagContextoRequest request)
    {
        var contexto = _context.TagsContextoIa
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.TagName == request.TagName);

        if (contexto == null)
        {
            contexto = new TagContextoIa
            {
                ClienteId = request.ClienteId,
                TagName = request.TagName,
                DataCriacao = DateTime.Now
            };

            _context.TagsContextoIa.Add(contexto);
        }

        contexto.TipoRepresentacao = request.TipoRepresentacao;
        contexto.Descricao = request.Descricao;
        contexto.Criticidade = request.Criticidade;
        contexto.Equipamento = request.Equipamento;
        contexto.Area = request.Area;
        contexto.ObservacaoModoOperacao = request.ObservacaoModoOperacao;

        contexto.Impactos = JsonSerializer.Serialize(request.Impactos ?? new List<string>());
        contexto.ModosOperacao = JsonSerializer.Serialize(request.ModosOperacao ?? new List<string>());
        contexto.CausasProvaveis = JsonSerializer.Serialize(request.CausasProvaveis ?? new List<string>());
        contexto.AcoesRecomendadas = JsonSerializer.Serialize(request.AcoesRecomendadas ?? new List<string>());

        contexto.ContextoCompleto =
            !string.IsNullOrWhiteSpace(request.Descricao) &&
            !string.IsNullOrWhiteSpace(request.Criticidade);

        contexto.DataAtualizacao = DateTime.Now;

        _context.SaveChanges();

        return Ok(new
        {
            mensagem = "Contexto da tag salvo com sucesso.",
            contexto.ClienteId,
            contexto.TagName,
            contexto.ContextoCompleto,
            contexto.DataCriacao,
            contexto.DataAtualizacao
        });
    }
}

public class TagContextoRequest
{
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";

    public string? TipoRepresentacao { get; set; }
    public string? Descricao { get; set; }
    public string? Criticidade { get; set; }

    public List<string>? Impactos { get; set; }
    public List<string>? ModosOperacao { get; set; }
    public List<string>? CausasProvaveis { get; set; }
    public List<string>? AcoesRecomendadas { get; set; }

    public string? Equipamento { get; set; }
    public string? Area { get; set; }
    public string? ObservacaoModoOperacao { get; set; }
}