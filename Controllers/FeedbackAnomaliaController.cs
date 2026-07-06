using Microsoft.AspNetCore.Mvc;

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
            Motivo = request.Motivo,
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
            feedback.Motivo,
            feedback.DataRegistro
        });
    }
}

public class FeedbackAnomaliaRequest
{
    public string Motivo { get; set; } = "";
    public string? Descricao { get; set; }
    public string? AcaoTomada { get; set; }
}