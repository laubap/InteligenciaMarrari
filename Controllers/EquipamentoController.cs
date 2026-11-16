using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Controller para gerenciar equipamentos e suas tags no sistema.
// Ele permite cadastrar equipamentos, listar todos os equipamentos
// e vincular tags a um equipamento existente.
[ApiController]
[Route("api/equipamentos")]
public class EquipamentoController : ControllerBase
{
    private readonly AppDbContext _context;

    public EquipamentoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    // Endpoint: POST /api/equipamentos
    // Recebe um EquipamentoRequest para cadastrar um novo equipamento no banco.
    // A entidade EquipamentoIa é criada e persistida em EquipamentosIa.
    public IActionResult Salvar([FromBody] EquipamentoRequest request)
    {
        var equipamento = new EquipamentoIa
        {
            ClienteId = request.ClienteId,
            Nome = request.Nome,
            Descricao = request.Descricao,
            Area = request.Area,
            TipoEquipamento = request.TipoEquipamento,
            Criticidade = request.Criticidade,
            Fabricante = request.Fabricante,
            Modelo = request.Modelo,
            Observacoes = request.Observacoes,
            DataUltimaManutencao = request.DataUltimaManutencao,
            DataCriacao = DateTime.Now,
            DataAtualizacao = DateTime.Now
        };

        _context.EquipamentosIa.Add(equipamento);
        _context.SaveChanges();

        return Ok(new
        {
            mensagem = "Equipamento cadastrado com sucesso.",
            equipamento.Id,
            equipamento.Nome
        });
    }

    [HttpGet]
    // Endpoint: GET /api/equipamentos
    // Retorna a lista completa de equipamentos, incluindo as tags e processos relacionados.
    public IActionResult Listar()
    {
        return Ok(
            _context.EquipamentosIa
                .Include(x => x.Tags)
                .Include(x => x.Processos)
                .ToList()
        );
    }

    [HttpPost("{equipamentoId}/tags")]
    // Endpoint: POST /api/equipamentos/{equipamentoId}/tags
    // Vincula uma tag ao equipamento informado ou atualiza os dados da tag
    // caso ela já exista para esse equipamento.
    public IActionResult VincularTag(
        int equipamentoId,
        [FromBody] EquipamentoTagRequest request)
    {
        var equipamento = _context.EquipamentosIa
            .FirstOrDefault(x => x.Id == equipamentoId);

        if (equipamento == null)
            return NotFound(new { erro = "Equipamento não encontrado." });

        // P36: o vínculo só vale para o cliente dono do equipamento.
        // Vale para criar e para atualizar um vínculo existente.
        if (request.ClienteId != equipamento.ClienteId)
            return BadRequest(new { erro = "O cliente informado não é o cliente do equipamento." });

        var existente = _context.EquipamentoTagsIa
            .FirstOrDefault(x =>
                x.EquipamentoIaId == equipamentoId &&
                x.TagName == request.TagName);

        if (existente != null)
        {
            existente.PapelDaTag = request.PapelDaTag;
            existente.Escopo = request.Escopo;

            _context.SaveChanges();

            return Ok(new
            {
                mensagem = "Tag do equipamento atualizada com sucesso.",
                equipamentoId,
                request.TagName,
                request.PapelDaTag,
                request.Escopo
            });
        }

        var tag = new EquipamentoTagIa
        {
            EquipamentoIaId = equipamentoId,
            ClienteId = request.ClienteId,
            TagName = request.TagName,
            PapelDaTag = request.PapelDaTag,
            Escopo = request.Escopo
        };

        _context.EquipamentoTagsIa.Add(tag);
        _context.SaveChanges();

        return Ok(new
        {
            mensagem = "Tag vinculada ao equipamento com sucesso.",
            equipamentoId,
            tag.TagName,
            tag.PapelDaTag,
            tag.Escopo
        });
    }
}

public class EquipamentoRequest
{
    // Dados recebidos para cadastro de um novo equipamento.
    public string ClienteId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public string? Area { get; set; }
    public string? TipoEquipamento { get; set; }
    public string? Criticidade { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public string? Observacoes { get; set; }
    public DateTime? DataUltimaManutencao { get; set; }
}

public class EquipamentoTagRequest
{
    // Dados recebidos para vincular ou atualizar uma tag de equipamento.
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";
    public string? PapelDaTag { get; set; }
    public string? Escopo { get; set; }
}