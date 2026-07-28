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
    public async Task<IActionResult> SalvarFeedback(
        int anomaliaId,
        [FromBody] FeedbackAnomaliaRequest request)
    {
        var anomalia = await _context.AnomaliasDetectadas
        .Where(x =>
            x.Status != StatusAnomalia.Descartada)
            .FirstOrDefaultAsync(x => x.Id == anomaliaId);

        if (anomalia == null)
        {
            return NotFound(new
            {
                erro = "Anomalia não encontrada."
            });
        }

        var motivoExiste = await _context.MotivosAnomaliaIA
            .AnyAsync(x => x.Id == request.MotivoAnomaliaIAId);

        if (!motivoExiste)
        {
            return BadRequest(new
            {
                erro = "O tipo de falha informado não existe."
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

            AvaliacaoDeteccao = request.AvaliacaoDeteccao,
            AvaliacaoCausa = request.AvaliacaoCausa,
            AvaliacaoAcao = request.AvaliacaoAcao,

            DataRegistro = DateTime.Now
        };

        _context.FeedbackAnomaliasIa.Add(feedback);
                if (
            request.AvaliacaoDeteccao ==
            AvaliacaoIa.Incorreto
        )
        {
            anomalia.Status =
                StatusAnomalia.Descartada;
        }
        else
        {
            anomalia.Status =
                StatusAnomalia.Confirmada;
        }
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensagem = "Feedback da anomalia registrado com sucesso.",
            feedback = new
            {
                feedback.Id,
                feedback.AnomaliaId,
                feedback.ClienteId,
                feedback.TagName,
                feedback.MotivoAnomaliaIAId,
                feedback.Descricao,
                feedback.AcaoTomada,
                feedback.AvaliacaoDeteccao,
                feedback.AvaliacaoCausa,
                feedback.AvaliacaoAcao,
                feedback.DataRegistro
            }
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

                x.AvaliacaoDeteccao,
                x.AvaliacaoCausa,
                x.AvaliacaoAcao,

                PontuacaoDeteccao = ConverterAvaliacaoEmPontuacao(x.AvaliacaoDeteccao),
                PontuacaoCausa = ConverterAvaliacaoEmPontuacao(x.AvaliacaoCausa),
                PontuacaoAcao = ConverterAvaliacaoEmPontuacao(x.AvaliacaoAcao),

                x.DataRegistro
            })
            .ToListAsync();

        return Ok(feedbacks);
    }

    private static double ConverterAvaliacaoEmPontuacao(AvaliacaoIa avaliacao)
    {
        return avaliacao switch
        {
            AvaliacaoIa.Correto => 1,
            AvaliacaoIa.Parcial => 0.5,
            AvaliacaoIa.Incorreto => 0,
            _ => 0
        };
    }
}

public class FeedbackAnomaliaRequest
{
    public int MotivoAnomaliaIAId { get; set; }

    public string? Descricao { get; set; }

    public string? AcaoTomada { get; set; }

    public AvaliacaoIa AvaliacaoDeteccao { get; set; } =
        AvaliacaoIa.NaoAvaliado;

    public AvaliacaoIa AvaliacaoCausa { get; set; } =
        AvaliacaoIa.NaoAvaliado;

    public AvaliacaoIa AvaliacaoAcao { get; set; } =
        AvaliacaoIa.NaoAvaliado;
}