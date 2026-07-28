using IAApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/tags/contexto")]
public class TagContextoController : ControllerBase
{
    private readonly TagContextoIaService _tagContextoService;

    public TagContextoController(
        TagContextoIaService tagContextoService)
    {
        _tagContextoService = tagContextoService;
    }

    [HttpPost]
    public async Task<IActionResult> SalvarContexto(
        [FromBody] TagContextoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var contexto = await _tagContextoService.SalvarAsync(
                request,
                cancellationToken);

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
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                mensagem = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ObterContexto(
        [FromQuery] string clienteId,
        [FromQuery] string tagName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clienteId) ||
            string.IsNullOrWhiteSpace(tagName))
        {
            return BadRequest(new
            {
                mensagem = "ClienteId e TagName são obrigatórios."
            });
        }

        var contexto = await _tagContextoService.ObterAsync(
            clienteId,
            tagName,
            cancellationToken);

        if (contexto == null)
        {
            return NotFound(new
            {
                mensagem = "Contexto da tag não encontrado.",
                clienteId,
                tagName
            });
        }

        return Ok(new
        {
            contexto.Id,
            contexto.ClienteId,
            contexto.TagName,
            contexto.TipoRepresentacao,
            contexto.Descricao,
            contexto.Criticidade,

            Impactos = DesserializarLista(contexto.Impactos),
            ModosOperacao = DesserializarLista(contexto.ModosOperacao),
            CausasProvaveis = DesserializarLista(contexto.CausasProvaveis),
            AcoesRecomendadas = DesserializarLista(contexto.AcoesRecomendadas),

            contexto.Equipamento,
            contexto.Area,
            contexto.ObservacaoModoOperacao,
            contexto.ContextoCompleto,
            contexto.DataCriacao,
            contexto.DataAtualizacao
        });
    }

    private static List<string> DesserializarLista(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)
                   ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
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