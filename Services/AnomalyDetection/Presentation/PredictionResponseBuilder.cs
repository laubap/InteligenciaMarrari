using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Multivariate;

namespace IAApi.Services.AnomalyDetection.Presentation;

public sealed class PredictionResponseBuilder
{
    private readonly PredictionDependencyMapper
        _predictionDependencyMapper;

    public PredictionResponseBuilder(
        PredictionDependencyMapper predictionDependencyMapper)
    {
        _predictionDependencyMapper =
            predictionDependencyMapper;
    }

    public AnomaliaResponse Build(
        AnomaliaRequest request,
        string tipoTag,
        bool ehAnomalia,
        double score,
        string mensagem,
        FeedbackSemelhanteDto? analiseFeedbacks,
        bool comportamentoReconhecido,
        string? classificacaoComportamentoReconhecido,
        double mediaHistoricaDetector,
        double limiteInferior,
        double limiteSuperior,
        TagContextoIa? contexto,
        AnomalyInterpretationResult resultadoInterpretacao,
        ProcessoIa? processo,
        AnaliseRelacionadasResultado analiseRelacionadas,
        ResultadoRiscoProcesso riscoProcesso,
        List<string> fatoresRisco,
        ResultadoTendenciaRisco tendenciaRisco,
        ResultadoTendenciaValor tendenciaValor,
        bool multivariatePromotionApplied,
        MultivariateDecisionEvidence? multivariateOperationalEvidence,
        string runtimeDecisionSource,
        List<DetectorExecutadoDto> detectoresExecutados)
    {
        return new AnomaliaResponse
        {
            ClienteId =
                request.ClienteId,

            TagName =
                request.TagName,

            TipoTag =
                tipoTag,

            EhAnomalia =
                ehAnomalia,

            Score =
                (float)score,

            Mensagem =
                mensagem,

            FeedbackSemelhante =
                analiseFeedbacks,

            Resumo =
                PredictionResponseMapper.MapearResumo(
                    ehAnomalia,
                    comportamentoReconhecido,
                    classificacaoComportamentoReconhecido,
                    riscoProcesso.Mensagem),

            Perfil =
                PredictionResponseMapper.MapearPerfil(
                    request,
                    mediaHistoricaDetector,
                    limiteInferior,
                    limiteSuperior,
                    score),

            Contexto =
                PredictionResponseMapper.MapearContexto(
                    contexto),

            Interpretacao =
                PredictionResponseMapper.MapearInterpretacao(
                    resultadoInterpretacao),

            Processo =
                PredictionResponseMapper.MapearProcesso(
                    processo),

            Dependencias =
                _predictionDependencyMapper.Mapear(
                    analiseRelacionadas,
                    contexto),

            Risco =
                PredictionResponseMapper.MapearRisco(
                    riscoProcesso,
                    fatoresRisco),

            Tendencias =
                PredictionResponseMapper.MapearTendencias(
                    tendenciaRisco,
                    tendenciaValor),

            Multivariado =
                PredictionResponseMapper.MapearMultivariado(
                    multivariatePromotionApplied,
                    multivariateOperationalEvidence,
                    runtimeDecisionSource,
                    score),

            Detectores =
                detectoresExecutados
        };
    }
}