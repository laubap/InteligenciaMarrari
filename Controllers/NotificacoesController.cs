using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/notificacoes")]
public class NotificacoesController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificacoesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var notificacoes = await _context.AnomaliasDetectadas
            .Where(x =>
                x.EhAnomalia ||
                x.ClassificacaoRisco == "alto" ||
                x.ClassificacaoRisco == "critico" ||
                x.ClassificacaoRisco == "crítico")
            .OrderByDescending(x => x.DataDeteccao)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                Titulo = "Anomalia detectada",
                Mensagem = x.Mensagem,
                x.TagName,
                Valor = x.ValorRecebido,
                Score = x.Score,
                Risco = x.ClassificacaoRisco,
                Data = x.DataDeteccao
            })
            .ToListAsync();

        return Ok(notificacoes);
    }
}