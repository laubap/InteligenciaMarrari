using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/conhecimento-ia")]
public class ConhecimentoIaController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConhecimentoIaController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Obter()
    {
        var totalAnomalias = await _context.AnomaliasDetectadas.CountAsync();
        var totalFeedbacks = await _context.FeedbackAnomaliasIa.CountAsync();

        var tiposFalhaAprendidos = await _context.FeedbackAnomaliasIa
            .Where(x => !string.IsNullOrWhiteSpace(x.Motivo))
            .Select(x => x.Motivo)
            .Distinct()
            .CountAsync();

        var principaisMotivos = await _context.FeedbackAnomaliasIa
            .Where(x => !string.IsNullOrWhiteSpace(x.Motivo))
            .GroupBy(x => x.Motivo)
            .Select(x => new
            {
                motivo = x.Key,
                total = x.Count()
            })
            .OrderByDescending(x => x.total)
            .Take(5)
            .ToListAsync();

        var tagsComMaisAnomalias = await _context.AnomaliasDetectadas
            .GroupBy(x => x.TagName)
            .Select(x => new
            {
                tagName = x.Key,
                total = x.Count()
            })
            .OrderByDescending(x => x.total)
            .Take(5)
            .ToListAsync();

        var anomaliasPorDia = await _context.AnomaliasDetectadas
            .GroupBy(x => x.DataDeteccao.Date)
            .Select(x => new
            {
                data = x.Key,
                total = x.Count()
            })
            .OrderBy(x => x.data)
            .Take(14)
            .ToListAsync();

        var distribuicaoRisco = await _context.AnomaliasDetectadas
            .GroupBy(x => x.ClassificacaoRisco ?? "sem risco")
            .Select(x => new
            {
                risco = x.Key,
                total = x.Count()
            })
            .OrderByDescending(x => x.total)
            .ToListAsync();

        var ultimosFeedbacks = await _context.FeedbackAnomaliasIa
            .OrderByDescending(x => x.DataRegistro)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.TagName,
                x.Motivo,
                x.Descricao,
                x.AcaoTomada,
                x.DataRegistro
            })
            .ToListAsync();

        var percentualFeedback =
            totalAnomalias == 0
                ? 0
                : Math.Round((double)totalFeedbacks / totalAnomalias * 100, 1);

        var confiancaIa = CalcularConfiancaIa(totalAnomalias, totalFeedbacks, tiposFalhaAprendidos);

        return Ok(new
        {
            totalAnomalias,
            totalFeedbacks,
            tiposFalhaAprendidos,
            percentualFeedback,
            confiancaIa,

            principaisMotivos,
            tagsComMaisAnomalias,
            anomaliasPorDia,
            distribuicaoRisco,
            ultimosFeedbacks,

            mensagem =
                "A IA Marrari está acumulando conhecimento com base nas anomalias detectadas e nos feedbacks registrados pelos operadores."
        });
    }

    private int CalcularConfiancaIa(
        int totalAnomalias,
        int totalFeedbacks,
        int tiposFalhaAprendidos)
    {
        if (totalAnomalias == 0)
            return 0;

        var baseFeedback = Math.Min(60, totalFeedbacks * 5);
        var baseDiversidade = Math.Min(25, tiposFalhaAprendidos * 5);
        var baseExperiencia = Math.Min(15, totalAnomalias);

        return baseFeedback + baseDiversidade + baseExperiencia;
    }
}