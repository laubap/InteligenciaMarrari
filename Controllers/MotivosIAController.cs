using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/motivos-ia")]
public class MotivosIAController : ControllerBase
{
    private readonly AppDbContext _context;

    public MotivosIAController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{clienteId}")]
    public async Task<IActionResult> Listar(string clienteId)
    {
        var motivos = await _context.MotivosAnomaliaIA
            .Include(x => x.AcoesRecomendadas)
            .Where(x => x.ClienteId == clienteId && x.Ativo)
            .OrderBy(x => x.Nome)
             .Select(x => new
        {
            x.Id,
            x.ClienteId,
            x.Nome,
            x.Descricao,
            x.CategoriaFalhaIAId,
            AcoesRecomendadas = x.AcoesRecomendadas
                .Where(a => a.Ativa)
                .Select(a => new
                {
                    a.Id,
                    a.Descricao
                })
                .ToList()
        })
            .ToListAsync();

        return Ok(motivos);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] MotivoAnomaliaIARequest request)
    {
        var motivo = new MotivoAnomaliaIA
        {
            ClienteId = request.ClienteId,
            Nome = request.Nome,
            Descricao = request.Descricao,
            Ativo = true,
            DataCriacao = DateTime.Now,
            DataAtualizacao = DateTime.Now,
            CategoriaFalhaIAId = request.CategoriaFalhaIAId,
        };

        _context.MotivosAnomaliaIA.Add(motivo);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Motivo de anomalia criado com sucesso.",
            motivo
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(
        int id,
        [FromBody] MotivoAnomaliaIARequest request)
    {
        var motivo = await _context.MotivosAnomaliaIA
            .FirstOrDefaultAsync(x => x.Id == id);

        if (motivo == null)
            return NotFound(new { erro = "Motivo não encontrado." });

        motivo.Nome = request.Nome;
        motivo.Descricao = request.Descricao;
        motivo.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Motivo atualizado com sucesso.",
            motivo
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Desativar(int id)
    {
        var motivo = await _context.MotivosAnomaliaIA
            .FirstOrDefaultAsync(x => x.Id == id);

        if (motivo == null)
            return NotFound(new { erro = "Motivo não encontrado." });

        motivo.Ativo = false;
        motivo.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Motivo desativado com sucesso."
        });
    }
}

public class MotivoAnomaliaIARequest
{
    public string ClienteId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public int? CategoriaFalhaIAId { get; set; }
}