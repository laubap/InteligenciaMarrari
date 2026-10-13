using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Presentation;

namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionPostDecisionAnalysisService
{
    private readonly AnaliseRelacionadasService
        _analiseRelacionadasService;

    private readonly PredictionRiskAnalysisService
        _predictionRiskAnalysisService;

    private readonly PredictionTrendAnalysisService
        _predictionTrendAnalysisService;

    private readonly FeedbackAnomaliaAnaliseService
        _feedbackAnomaliaAnaliseService;

    private readonly MultivariateOperationalInterpretationFactory?
        _multivariateOperationalInterpretationFactory;

    public PredictionPostDecisionAnalysisService(
        AnaliseRelacionadasService analiseRelacionadasService,
        PredictionRiskAnalysisService predictionRiskAnalysisService,
        PredictionTrendAnalysisService predictionTrendAnalysisService,
        FeedbackAnomaliaAnaliseService feedbackAnomaliaAnaliseService,
        MultivariateOperationalInterpretationFactory?
            multivariateOperationalInterpretationFactory = null)
    {
        _analiseRelacionadasService =
            analiseRelacionadasService;

        _predictionRiskAnalysisService =
            predictionRiskAnalysisService;

        _predictionTrendAnalysisService =
            predictionTrendAnalysisService;

        _feedbackAnomaliaAnaliseService =
            feedbackAnomaliaAnaliseService;

        _multivariateOperationalInterpretationFactory =
            multivariateOperationalInterpretationFactory;
    }

    public bool MultivariateInterpretationAvailable =>
        _multivariateOperationalInterpretationFactory is not null;

    public async Task<PredictionPostDecisionAnalysisResult>
        AnalisarAsync(
            AnomaliaRequest request,
            string tipoTag,
            bool ehAnomalia,
            double score,
            TagContextoIa? contexto,
            AnomalyInterpretationResult? resultadoInterpretacao,
            bool multivariatePromotionApplied,
            MultivariateDecisionEvidence?
                multivariateOperationalEvidence)
    {
        var analiseRelacionadas =
            await _analiseRelacionadasService.Analisar(
                request.ClienteId,
                request.TagName);

        var analiseRisco =
            _predictionRiskAnalysisService.Analisar(
                ehAnomalia,
                score,
                contexto,
                analiseRelacionadas);

        var riscoProcesso =
            analiseRisco.Risco;

        var fatoresRisco =
            analiseRisco.Fatores;

        if (multivariatePromotionApplied &&
            multivariateOperationalEvidence is not null)
        {
            resultadoInterpretacao =
                _multivariateOperationalInterpretationFactory!.Create(
                    multivariateOperationalEvidence,
                    score,
                    PredictionResponseFormatter.MapearSeveridadeRisco(
                        riscoProcesso.Classificacao));

            fatoresRisco.Add(
                $"Anomalia multivariada operacional no grupo " +
                $"'{multivariateOperationalEvidence.GroupName}' " +
                $"(score {multivariateOperationalEvidence.Score:F2}, " +
                $"limite {multivariateOperationalEvidence.Threshold:F2}).");
        }

        if (resultadoInterpretacao is null)
        {
            throw new InvalidOperationException(
                "Não foi possível construir a interpretação operacional.");
        }

        var analiseTendencias =
            _predictionTrendAnalysisService.Analisar(
                request.ClienteId,
                request.TagName,
                tipoTag);

        var tendenciaRisco =
            analiseTendencias.TendenciaRisco;

        var tendenciaValor =
            analiseTendencias.TendenciaValor;

        var analiseFeedbacks =
            _feedbackAnomaliaAnaliseService
                .GerarAnaliseFeedbacks(
                    request.ClienteId,
                    request.TagName,
                    score,
                    request.Valor);

        var mensagem =
            multivariatePromotionApplied &&
            multivariateOperationalEvidence is not null
                ? PredictionResponseFormatter.MontarMensagemMultivariada(
                    request,
                    contexto,
                    riscoProcesso,
                    multivariateOperationalEvidence)
                : PredictionResponseFormatter.MontarMensagemCliente(
                    request,
                    contexto,
                    ehAnomalia,
                    score,
                    riscoProcesso,
                    tendenciaValor);

        return new PredictionPostDecisionAnalysisResult(
            analiseRelacionadas,
            riscoProcesso,
            fatoresRisco,
            resultadoInterpretacao,
            tendenciaRisco,
            tendenciaValor,
            analiseFeedbacks,
            mensagem);
    }
}

public sealed record PredictionPostDecisionAnalysisResult(
    AnaliseRelacionadasResultado AnaliseRelacionadas,
    ResultadoRiscoProcesso RiscoProcesso,
    List<string> FatoresRisco,
    AnomalyInterpretationResult ResultadoInterpretacao,
    ResultadoTendenciaRisco TendenciaRisco,
    ResultadoTendenciaValor TendenciaValor,
    FeedbackSemelhanteDto? AnaliseFeedbacks,
    string Mensagem);