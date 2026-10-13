using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionFinalizationService
{
    private readonly AnomaliaHistoricoService
        _historicoService;

    private readonly PredictionResponseBuilder
        _predictionResponseBuilder;

    public PredictionFinalizationService(
        AnomaliaHistoricoService historicoService,
        PredictionResponseBuilder predictionResponseBuilder)
    {
        _historicoService =
            historicoService;

        _predictionResponseBuilder =
            predictionResponseBuilder;
    }

    public AnomaliaResponse Finalizar(
        PredictionFinalizationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var postDecisionAnalysis =
            input.PostDecisionAnalysis;

        _historicoService.Registrar(
            input.Request.ClienteId,
            input.Request.TagName,
            input.TipoTag,
            input.Request.Valor,
            input.Score,
            input.EhAnomalia,
            postDecisionAnalysis.Mensagem,
            input.Contexto?.Criticidade,
            postDecisionAnalysis.AnaliseRelacionadas,
            postDecisionAnalysis.RiscoProcesso.ScoreRisco,
            postDecisionAnalysis.RiscoProcesso.Classificacao,
            postDecisionAnalysis.TendenciaRisco.Tendencia,
            postDecisionAnalysis.TendenciaValor.Tendencia,
            input.RuntimeAudit);

        var detectoresExecutados =
            PredictionResponseMapper.MapearDetectores(
                input.ResultadoDetector.DetalhesDetectores);

        LogInterpretacaoFinal(
            postDecisionAnalysis.ResultadoInterpretacao);

        return _predictionResponseBuilder.Build(
            input.Request,
            input.TipoTag,
            input.EhAnomalia,
            input.Score,
            postDecisionAnalysis.Mensagem,
            postDecisionAnalysis.AnaliseFeedbacks,
            input.ComportamentoReconhecido,
            input.ClassificacaoComportamentoReconhecido,
            input.MediaHistoricaDetector,
            input.LimiteInferior,
            input.LimiteSuperior,
            input.Contexto,
            postDecisionAnalysis.ResultadoInterpretacao,
            input.Processo,
            postDecisionAnalysis.AnaliseRelacionadas,
            postDecisionAnalysis.RiscoProcesso,
            postDecisionAnalysis.FatoresRisco,
            postDecisionAnalysis.TendenciaRisco,
            postDecisionAnalysis.TendenciaValor,
            input.MultivariatePromotionApplied,
            input.MultivariateOperationalEvidence,
            input.RuntimeDecisionSource,
            detectoresExecutados);
    }

    private static void LogInterpretacaoFinal(
        AnomalyInterpretationResult resultadoInterpretacao)
    {
        Console.WriteLine();
        Console.WriteLine("========== INTERPRETAÃ‡ÃƒO FINAL ==========");
        Console.WriteLine($"Resumo: {resultadoInterpretacao.Resumo}");
        Console.WriteLine($"InterpretaÃ§Ã£o: {resultadoInterpretacao.InterpretacaoTecnica}");
        Console.WriteLine($"Severidade: {resultadoInterpretacao.Severidade}");
        Console.WriteLine($"Gerado por LLM: {resultadoInterpretacao.GeradoPorLlm}");

        Console.WriteLine("EvidÃªncias:");
        foreach (var item in resultadoInterpretacao.Evidencias)
        {
            Console.WriteLine($" - {item}");
        }

        Console.WriteLine("PossÃ­veis causas:");
        foreach (var item in resultadoInterpretacao.PossiveisCausas)
        {
            Console.WriteLine($" - {item}");
        }

        Console.WriteLine("RecomendaÃ§Ãµes:");
        foreach (var item in resultadoInterpretacao.Recomendacoes)
        {
            Console.WriteLine($" - {item}");
        }

        Console.WriteLine("=========================================");
        Console.WriteLine();
    }
}

public sealed record PredictionFinalizationInput(
    AnomaliaRequest Request,
    string TipoTag,
    bool EhAnomalia,
    double Score,
    bool ComportamentoReconhecido,
    string? ClassificacaoComportamentoReconhecido,
    double MediaHistoricaDetector,
    double LimiteInferior,
    double LimiteSuperior,
    TagContextoIa? Contexto,
    ProcessoIa? Processo,
    PredictionPostDecisionAnalysisResult PostDecisionAnalysis,
    bool MultivariatePromotionApplied,
    MultivariateDecisionEvidence? MultivariateOperationalEvidence,
    string RuntimeDecisionSource,
    DetectorResult ResultadoDetector,
    RuntimeDecisionAuditData RuntimeAudit);
