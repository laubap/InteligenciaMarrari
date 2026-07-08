using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/acoes-ia")]
public class AcoesIAController : ControllerBase
{
    private readonly AppDbContext _context;

    public AcoesIAController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("motivo/{motivoId}")]
    public async Task<IActionResult> ListarPorMotivo(int motivoId)
    {
        var acoes = await _context.AcoesRecomendadasIA
            .Where(x => x.MotivoAnomaliaIAId == motivoId && x.Ativa)
            .OrderBy(x => x.Descricao)
            .Select(x => new
            {
                x.Id,
                x.MotivoAnomaliaIAId,
                x.ClienteId,
                x.Descricao,
                x.Ativa,
                x.DataCriacao,
                x.DataAtualizacao
            })
            .ToListAsync();

        return Ok(acoes);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] AcaoRecomendadaIARequest request)
    {
        var motivo = await _context.MotivosAnomaliaIA
            .FirstOrDefaultAsync(x => x.Id == request.MotivoAnomaliaIAId);

        if (motivo == null)
            return NotFound(new { erro = "Motivo não encontrado." });

        var acao = new AcaoRecomendadaIA
        {
            ClienteId = request.ClienteId,
            MotivoAnomaliaIAId = request.MotivoAnomaliaIAId,
            Descricao = request.Descricao,
            Ativa = true,
            DataCriacao = DateTime.Now,
            DataAtualizacao = DateTime.Now
        };

        _context.AcoesRecomendadasIA.Add(acao);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Ação recomendada criada com sucesso.",
            acao = new
            {
                acao.Id,
                acao.ClienteId,
                acao.MotivoAnomaliaIAId,
                acao.Descricao,
                acao.Ativa,
                acao.DataCriacao,
                acao.DataAtualizacao
            }
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(
        int id,
        [FromBody] AcaoRecomendadaIARequest request)
    {
        var acao = await _context.AcoesRecomendadasIA
            .FirstOrDefaultAsync(x => x.Id == id);

        if (acao == null)
            return NotFound(new { erro = "Ação recomendada não encontrada." });

        acao.Descricao = request.Descricao;
        acao.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Ação recomendada atualizada com sucesso.",
            acao = new
            {
                acao.Id,
                acao.ClienteId,
                acao.MotivoAnomaliaIAId,
                acao.Descricao,
                acao.Ativa,
                acao.DataCriacao,
                acao.DataAtualizacao
            }
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Desativar(int id)
    {
        var acao = await _context.AcoesRecomendadasIA
            .FirstOrDefaultAsync(x => x.Id == id);

        if (acao == null)
            return NotFound(new { erro = "Ação recomendada não encontrada." });

        acao.Ativa = false;
        acao.DataAtualizacao = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Ação recomendada desativada com sucesso."
        });
    }
}

public class AcaoRecomendadaIARequest
{
    public string ClienteId { get; set; } = "";
    public int MotivoAnomaliaIAId { get; set; }
    public string Descricao { get; set; } = "";
}