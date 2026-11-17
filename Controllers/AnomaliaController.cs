using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[ApiController]
[Route("api/anomalias")]
public class AnomaliaController : ControllerBase
{
    public const string MensagemErroAnalise =
        "Não foi possível analisar a leitura. O detalhe do erro está no log da IAApi.";

    private readonly PredictionService _predictionService;
    private readonly AppDbContext _context;
    private readonly FeedbackAnomaliaAnaliseService _feedbackAnomaliaAnaliseService;

    private readonly CurrentValuePredictionService
    _currentValuePredictionService;

    public AnomaliaController(
        PredictionService predictionService,
        AppDbContext context,
        FeedbackAnomaliaAnaliseService feedbackAnomaliaAnaliseService,
        CurrentValuePredictionService currentValuePredictionService)
    {
        _predictionService = predictionService;
        _context = context;
        _feedbackAnomaliaAnaliseService = feedbackAnomaliaAnaliseService;
        _currentValuePredictionService = currentValuePredictionService;
    }

[HttpPost("tag")]
public async Task<IActionResult> DetectarTag([FromBody] AnomaliaRequest request)
{
    try
    {
        Console.WriteLine("===== REQUEST =====");
        Console.WriteLine($"Cliente: '{request.ClienteId}'");
        Console.WriteLine($"Tag: '{request.TagName}'");

        var resposta = await _predictionService.Detectar(request);

        Console.WriteLine("=========== JSON DA RESPOSTA ===========");
Console.WriteLine(
    JsonSerializer.Serialize(
        resposta,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }));
Console.WriteLine("========================================");

        return Ok(resposta);
    }
    catch (Exception ex)
    {
        // O detalhe fica so no log do servidor (P39): o chamador recebe uma
        // mensagem generica, sem stack trace nem texto da excecao.
        Console.WriteLine(ex.ToString());

        return BadRequest(new
        {
            erro = MensagemErroAnalise
        });
    }
}
    [HttpPost("tag/valor-atual")]
    public async Task<IActionResult> DetectarTagValorAtual([FromBody] AnomaliaValorAtualRequest request)
    {
        try
        {
var resposta =
    await _currentValuePredictionService
        .DetectarValorAtual(
            request.ClienteId,
            request.TagName);

            return Ok(resposta);
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>
    /// Decisoes gravadas, da mais recente para a mais antiga.
    /// Sem parametros, devolve todas as linhas de todos os clientes (contrato
    /// original). Tarefa 34.2 (P42), parametros opcionais:
    ///   clienteId: so as linhas desse cliente (comparacao exata);
    ///   desde:     so DataDeteccao &gt;= desde, inclusivo. Mesma convencao de
    ///              DataDeteccao: hora local da IAApi, sem fuso;
    ///   limite:    no maximo N linhas (as mais recentes), N &gt;= 1.
    /// Tarefa 34.1: o FeedbackSemelhante sai igual ao de antes, mas os
    /// feedbacks vem de uma unica consulta (antes, uma por linha).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? clienteId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] int? limite = null)
    {
        if (limite is < 1)
        {
            return BadRequest(new { erro = "limite deve ser maior ou igual a 1." });
        }

        IQueryable<AnomaliaDetectada> consulta = _context.AnomaliasDetectadas;

        if (clienteId is not null)
            consulta = consulta.Where(x => x.ClienteId == clienteId);

        if (desde is not null)
        {
            var inicio = desde.Value;
            consulta = consulta.Where(x => x.DataDeteccao >= inicio);
        }

        consulta = consulta.OrderByDescending(x => x.DataDeteccao);

        if (limite is not null)
            consulta = consulta.Take(limite.Value);

        var registros = await consulta.ToListAsync();

        // Uma consulta de feedback para a lista inteira (antes: uma por linha).
        var feedbacksPorTag = registros.Count == 0
            ? new Dictionary<(string ClienteId, string TagName), List<FeedbackComparavel>>()
            : _feedbackAnomaliaAnaliseService.CarregarFeedbacksPorTag(clienteId);

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

            FeedbackSemelhante = feedbacksPorTag.TryGetValue((x.ClienteId, x.TagName), out var feedbacks)
                ? FeedbackAnomaliaAnaliseService.AnalisarFeedbacks(
                    feedbacks,
                    x.Score,
                    x.ValorRecebido)
                : null
        }).ToList();

        return Ok(anomalias);
    }
}

public class AnomaliaValorAtualRequest
{
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";
}