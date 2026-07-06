using Microsoft.AspNetCore.Mvc;

// Controller que fornece dados de dashboard para um processo.
// Ele combina a análise do processo com a previsão de falhas para retornar
// um resumo de saúde, risco e tags críticas.
[ApiController]
[Route("api/processos-dashboard")]
public class ProcessoDashboardController : ControllerBase
{
    private readonly ProcessoInteligenciaService _processoInteligenciaService;
    private readonly FailurePredictionService _failurePredictionService;

    public ProcessoDashboardController(
        ProcessoInteligenciaService processoInteligenciaService,
        FailurePredictionService failurePredictionService)
    {
        _processoInteligenciaService = processoInteligenciaService;
        _failurePredictionService = failurePredictionService;
    }

    [HttpGet("{processoId}")]
    // Endpoint: GET /api/processos-dashboard/{processoId}
    // Retorna um objeto de dashboard com métricas de saúde, tags, risco de falha,
    // resumo e interpretação para o processo informado.
    public async Task<IActionResult> Dashboard(int processoId)
    {
        try
        {
            var analise = await _processoInteligenciaService.AnalisarProcesso(processoId);

            var falha = _failurePredictionService.Calcular(analise);

            var dashboard = new
            {
                processoId = analise.ProcessoId,
                processo = analise.NomeProcesso,
                area = analise.Area,
                criticidade = analise.Criticidade,

                saude = analise.ScoreSaude,
                status = analise.ClassificacaoSaude,
                mensagemSaude = analise.MensagemSaude,

                totalTags = analise.TotalTags,
                tagsNormais = analise.Tags.Count(x => x.Status == "normal"),
                tagsAnomalas = analise.TotalTagsAnomalas,
                tagsSemPerfil = analise.TotalTagsSemPerfil,
                tagsSemValorAtual = analise.Tags.Count(x => x.Status == "sem_valor_atual"),

                probabilidadeFalha = new
                {
                    percentual = falha.Probabilidade,
                    classificacao = falha.Classificacao,
                    mensagem = falha.Mensagem
                },

                resumo = analise.Resumo,
                interpretacao = analise.Interpretacao,

                tags = analise.Tags
                    .Select(x => new
                    {
                        x.TagName,
                        x.Descricao,
                        x.PapelDaTag,
                        x.Criticidade,
                        x.Status,
                        x.Score,
                        x.Mensagem,
                        x.EhAnomalia
                    })
                    .ToList(),


                tagsCriticas = analise.Tags
                    .Where(x =>
                        x.EhAnomalia ||
                        x.Status == "sem_perfil" ||
                        x.Status == "sem_valor_atual")
                    .Select(x => new
                    {
                        x.TagName,
                        x.Descricao,
                        x.PapelDaTag,
                        x.Criticidade,
                        x.Status,
                        x.Score,
                        x.Mensagem
                    })
                    .ToList()
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                erro = ex.Message
            });
        }
    }
}