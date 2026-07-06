using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[ApiController]
[Route("api/anomalias")]
public class AnomaliaController : ControllerBase
{
    private readonly PredictionService _predictionService;
    private readonly AppDbContext _context;
    private readonly FeedbackAnomaliaAnaliseService _feedbackAnomaliaAnaliseService;

    public AnomaliaController(
        PredictionService predictionService,
        AppDbContext context,
        FeedbackAnomaliaAnaliseService feedbackAnomaliaAnaliseService)
    {
        _predictionService = predictionService;
        _context = context;
        _feedbackAnomaliaAnaliseService = feedbackAnomaliaAnaliseService;
    }

    [HttpPost("tag")]
    public async Task<IActionResult> DetectarTag([FromBody] AnomaliaRequest request)
    {
        try
        {
            var resposta = await _predictionService.Detectar(request);
            return Ok(resposta);
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPost("tag/valor-atual")]
    public async Task<IActionResult> DetectarTagValorAtual([FromBody] AnomaliaValorAtualRequest request)
    {
        try
        {
            var resposta = await _predictionService.DetectarValorAtual(
                request.ClienteId,
                request.TagName
            );

            return Ok(resposta);
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var registros = await _context.AnomaliasDetectadas
            .OrderByDescending(x => x.DataDeteccao)
            .ToListAsync();

        var anomalias = registros.Select(x => new
        {
            x.Id,
            x.ClienteId,
            x.TagName,
            x.TipoTag,
            Valor = x.ValorRecebido,
            x.Score,
            x.EhAnomalia,
            x.Mensagem,
            x.Criticidade,
            RiscoProcesso = x.ScoreRiscoProcesso,
            x.ClassificacaoRisco,
            x.TendenciaRisco,
            x.TendenciaValor,
            x.DataDeteccao,

            FeedbackSemelhante = _feedbackAnomaliaAnaliseService.GerarAnaliseFeedbacks(
                x.ClienteId,
                x.TagName,
                x.Score,
                x.ValorRecebido
            )
        }).ToList();

        return Ok(anomalias);
    }
}

public class AnomaliaValorAtualRequest
{
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";
}