using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/anomalias")]
public class FeedbackAnomaliaController : ControllerBase
{
    private readonly AppDbContext _context;

    public FeedbackAnomaliaController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("{anomaliaId}/feedback")]
    public IActionResult SalvarFeedback(
        int anomaliaId,
        [FromBody] FeedbackAnomaliaRequest request)
    {
        var anomalia = _context.AnomaliasDetectadas
            .FirstOrDefault(x => x.Id == anomaliaId);

        if (anomalia == null)
        {
            return NotFound(new
            {
                erro = "Anomalia não encontrada."
            });
        }

        var feedback = new FeedbackAnomaliaIa
        {
            AnomaliaId = anomalia.Id,
            ClienteId = anomalia.ClienteId,
            TagName = anomalia.TagName,
            MotivoAnomaliaIAId = request.MotivoAnomaliaIAId,
            Descricao = request.Descricao,
            AcaoTomada = request.AcaoTomada,
            DataRegistro = DateTime.Now
        };

        _context.FeedbackAnomaliasIa.Add(feedback);
        _context.SaveChanges();

        return Ok(new
        {
            mensagem = "Feedback da anomalia registrado com sucesso.",
            feedback.Id,
            feedback.AnomaliaId,
            feedback.TagName,
            feedback.DataRegistro
        });
    }

    [HttpGet("{anomaliaId}/feedback")]
    public async Task<IActionResult> BuscarFeedbacks(int anomaliaId)
    {
        var feedbacks = await _context.FeedbackAnomaliasIa
            .Include(x => x.MotivoAnomaliaIA)
                .ThenInclude(x => x.CategoriaFalhaIA)
            .Where(x => x.AnomaliaId == anomaliaId)
            .OrderByDescending(x => x.DataRegistro)
            .Select(x => new
            {
                x.Id,
                x.AnomaliaId,
                x.TagName,

                CategoriaFalha =
                    x.MotivoAnomaliaIA != null &&
                    x.MotivoAnomaliaIA.CategoriaFalhaIA != null
                        ? x.MotivoAnomaliaIA.CategoriaFalhaIA.Nome
                        : null,

                TipoFalha =
                    x.MotivoAnomaliaIA != null
                        ? x.MotivoAnomaliaIA.Nome
                        : null,

                x.Descricao,
                x.AcaoTomada,
                x.DataRegistro
            })
            .ToListAsync();

        return Ok(feedbacks);
    }
}

public class FeedbackAnomaliaRequest
{
    public int MotivoAnomaliaIAId { get; set; }
    public string? Descricao { get; set; }
    public string? AcaoTomada { get; set; }
}