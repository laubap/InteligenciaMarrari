using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/dashboard-geral")]
public class DashboardGeralController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardGeralController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult ObterDashboard()
    {
        var totalProcessos = _context.ProcessosIa.Count();
        var totalEquipamentos = _context.EquipamentosIa.Count();
        var totalTagsConfiguradas = _context.TagsIaConfig.Count();
        var totalAnomalias = _context.AnomaliasDetectadas.Count(x => x.EhAnomalia);

        var ultimasAnomalias = _context.AnomaliasDetectadas
            .Where(x => x.EhAnomalia)
            .OrderByDescending(x => x.DataDeteccao)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.ClienteId,
                x.TagName,
                Valor = x.ValorRecebido,
                x.Score,
                x.EhAnomalia,
                RiscoProcesso = x.ScoreRiscoProcesso,
                x.ClassificacaoRisco,
                x.DataDeteccao
            })
            .ToList();

        var processos = _context.ProcessosIa
            .OrderBy(x => x.Nome)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.ClienteId,
                x.Nome,
                x.Area,
                x.Criticidade
            })
            .ToList();

        var equipamentos = _context.EquipamentosIa
            .OrderBy(x => x.Nome)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.ClienteId,
                x.Nome,
                x.Area,
                x.TipoEquipamento,
                x.Criticidade
            })
            .ToList();

        var saudeGeral = totalAnomalias == 0
            ? 100
            : Math.Max(0, 100 - (totalAnomalias * 5));

        return Ok(new
        {
            saudeGeral,
            totalProcessos,
            totalEquipamentos,
            totalTagsConfiguradas,
            totalAnomalias,
            processos,
            equipamentos,
            ultimasAnomalias
        });
    }
}