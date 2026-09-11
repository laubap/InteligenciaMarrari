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
        var totalAnomalias = await _context.AnomaliasDetectadas.CountAsync(x => x.EhAnomalia);

        var feedbacks = await _context.FeedbackAnomaliasIa
            .Include(x => x.MotivoAnomaliaIA)
            .ToListAsync();

        var totalFeedbacks = feedbacks.Count;

        var tiposFalhaAprendidos = feedbacks
            .Where(x => x.MotivoAnomaliaIA != null)
            .Select(x => x.MotivoAnomaliaIA!.Nome)
            .Distinct()
            .Count();

        var principaisMotivos = feedbacks
            .Where(x => x.MotivoAnomaliaIA != null)
            .GroupBy(x => x.MotivoAnomaliaIA!.Nome)
            .Select(x => new
            {
                motivo = x.Key,
                total = x.Count()
            })
            .OrderByDescending(x => x.total)
            .Take(5)
            .ToList();

        var tagsComMaisAnomalias = await _context.AnomaliasDetectadas
            .Where(x => x.EhAnomalia)
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
            .Where(x => x.EhAnomalia)
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

        var ultimosFeedbacks = feedbacks
            .OrderByDescending(x => x.DataRegistro)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.TagName,
                motivo = x.MotivoAnomaliaIA?.Nome ?? "Motivo não informado",
                categoria = x.MotivoAnomaliaIA?.CategoriaFalhaIA?.Nome,
                x.Descricao,
                x.AcaoTomada,
                x.DataRegistro
            })
            .ToList();

        var percentualFeedback =
            totalAnomalias == 0
                ? 0
                : Math.Round((double)totalFeedbacks / totalAnomalias * 100, 1);

        var confiancaIa = CalcularConfiancaIa(
            totalAnomalias,
            totalFeedbacks,
            tiposFalhaAprendidos
        );

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