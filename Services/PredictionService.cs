using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Runtime;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Orchestration;

public class PredictionService
{
    private const double LimiteEnsembleInterpretacao = 50.0;

    private readonly AnomaliaHistoricoService _historicoService;

    private readonly PredictionContextLoader _predictionContextLoader;

    private readonly EnsembleDetectionService _ensembleDetectionService;

    private readonly PredictionDetectionPreparationService
        _predictionDetectionPreparationService;

    private readonly PredictionInterpretationService
        _predictionInterpretationService;

    private readonly PredictionRuntimeDecisionService
        _predictionRuntimeDecisionService;

    private readonly PredictionPostDecisionAnalysisService
        _predictionPostDecisionAnalysisService;

    private readonly RuntimeFunctionalDecisionContextProvider
        _runtimeFunctionalDecisionContextProvider;

    private readonly MultivariateShadowRuntimeCoordinator?
        _multivariateShadowRuntimeCoordinator;

    private readonly PredictionMultivariateDecisionService?
        _predictionMultivariateDecisionService;

    private readonly PredictionResponseBuilder
        _predictionResponseBuilder;

    public PredictionService(
        PredictionContextLoader predictionContextLoader,
        PredictionPostDecisionAnalysisService predictionPostDecisionAnalysisService,
        AnomaliaHistoricoService historicoService,
        PredictionResponseBuilder predictionResponseBuilder,
        EnsembleDetectionService ensembleDetectionService,
        PredictionDetectionPreparationService predictionDetectionPreparationService,
        PredictionInterpretationService predictionInterpretationService,
        RuntimeFunctionalDecisionContextProvider runtimeFunctionalDecisionContextProvider,
        PredictionRuntimeDecisionService predictionRuntimeDecisionService,
        MultivariateShadowRuntimeCoordinator? multivariateShadowRuntimeCoordinator = null,
        PredictionMultivariateDecisionService? predictionMultivariateDecisionService = null)
    {
        _predictionContextLoader =
            predictionContextLoader;

        _predictionPostDecisionAnalysisService =
            predictionPostDecisionAnalysisService;

        _historicoService =
            historicoService;

        _predictionResponseBuilder =
            predictionResponseBuilder;

        _ensembleDetectionService =
            ensembleDetectionService;

        _predictionDetectionPreparationService =
            predictionDetectionPreparationService;

        _predictionInterpretationService =
            predictionInterpretationService;

        _runtimeFunctionalDecisionContextProvider =
            runtimeFunctionalDecisionContextProvider;

        _predictionRuntimeDecisionService =
            predictionRuntimeDecisionService;

        _multivariateShadowRuntimeCoordinator =
            multivariateShadowRuntimeCoordinator;

        _predictionMultivariateDecisionService =
            predictionMultivariateDecisionService;
    }

    public async Task<AnomaliaResponse> Detectar(
        AnomaliaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

var config =
    _predictionContextLoader.BuscarConfiguracaoAtiva(
        request.ClienteId,
        request.TagName);

        if (config == null)
        {
            throw new Exception(
                $"IA nÃ£o configurada para a tag " +
                $"{request.TagName} do cliente " +
                $"{request.ClienteId}."
            );
        }

        var dataHoraAtual =
            request.DataHora == default
                ? DateTime.Now
                : request.DataHora;

        IReadOnlyList<MultivariateDetectionResult>
            multivariateShadowResults =
                Array.Empty<MultivariateDetectionResult>();

        if (_multivariateShadowRuntimeCoordinator is not null)
        {
            multivariateShadowResults =
                await _multivariateShadowRuntimeCoordinator
                    .ProcessarLeituraAsync(
                        request.ClienteId,
                        request.TagName,
                        dataHoraAtual,
                        request.Valor,
                        cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

var perfil =
    _predictionContextLoader.BuscarPerfil(
        request.ClienteId,
        request.TagName);

        if (perfil == null)
        {
            throw new Exception(
                $"Perfil da tag {request.TagName} " +
                "ainda nÃ£o foi treinado. Execute o " +
                "treinamento de perfil antes da detecÃ§Ã£o."
            );
        }

var contextoOperacional =
    _predictionContextLoader.CarregarContextoOperacional(
        request.ClienteId,
        request.TagName);

var contexto =
    contextoOperacional.Contexto;

var processo =
    contextoOperacional.Processo;

        var tipoTag =
            contexto?.TipoRepresentacao
            ?? "perfil-comportamental";

        var preparation =
            await _predictionDetectionPreparationService.PrepararAsync(
                request,
                perfil,
                tipoTag,
                dataHoraAtual,
                cancellationToken);

        var entradaDetector =
            preparation.EntradaDetector;

        var baselineComportamentalAplicado =
            preparation.BaselineComportamentalAplicado;

        var comportamentoReconhecido =
            preparation.ComportamentoReconhecido;

        var codigoComportamentoReconhecido =
            preparation.CodigoComportamentoReconhecido;

        var classificacaoComportamentoReconhecido =
            preparation.ClassificacaoComportamentoReconhecido;

        var mediaHistoricaDetector =
            preparation.MediaHistoricaDetector;

        var resultadoDetector =
            _ensembleDetectionService.Detectar(
                entradaDetector
            );

        var runtimeFunctionalContext =
            _runtimeFunctionalDecisionContextProvider.Evaluate(
                entradaDetector,
                resultadoDetector);

        Console.WriteLine(
            $"B3.25 RUNTIME BEHAVIOR CONTEXT " +
            $"Cliente={request.ClienteId} " +
            $"Tag={request.TagName} " +
            $"Recognized={comportamentoReconhecido} " +
            $"Behavior={codigoComportamentoReconhecido ?? "-"} " +
            $"Classification={classificacaoComportamentoReconhecido ?? "-"} " +
            $"BaselineApplied={baselineComportamentalAplicado}");

var runtimeDecision =
    _predictionRuntimeDecisionService.Resolve(
        resultadoDetector,
        runtimeFunctionalContext,
        request.ClienteId);

 MultivariateOperationalFusionResult?
    multivariateOperationalFusion =
        null;

if (_predictionMultivariateDecisionService is not null)
{
    var multivariateDecision =
        await _predictionMultivariateDecisionService.ResolveAsync(
            runtimeDecision,
            request.ClienteId,
            request.TagName,
            dataHoraAtual,
            multivariateShadowResults,
            resultadoDetector.Score,
multivariateInterpretationAvailable:
    _predictionPostDecisionAnalysisService
        .MultivariateInterpretationAvailable,
            cancellationToken);

    runtimeDecision =
        multivariateDecision.RuntimeDecision;

    multivariateOperationalFusion =
        multivariateDecision.OperationalFusion;
}

var multivariatePromotionApplied =
    multivariateOperationalFusion?.PromotionApplied == true;

var multivariateOperationalEvidence =
    multivariateOperationalFusion?.MultivariateEvidence;

        var runtimeAudit =
            RuntimeDecisionAuditData.From(
                runtimeDecision,
                runtimeFunctionalContext);

var resultadoInterpretacao =
    await _predictionInterpretationService.InterpretarAsync(
        entradaDetector,
        resultadoDetector,
        runtimeDecision,
        LimiteEnsembleInterpretacao,
        multivariatePromotionApplied,
        comportamentoReconhecido,
        classificacaoComportamentoReconhecido,
        codigoComportamentoReconhecido,
        cancellationToken);

        var limiteInferior =
            resultadoDetector.LimiteInferior;

        var limiteSuperior =
            resultadoDetector.LimiteSuperior;

        var ehAnomalia =
            runtimeDecision.EmitAnomaly;

        var score =
            resultadoDetector.Score;

        cancellationToken.ThrowIfCancellationRequested();

        var postDecisionAnalysis =
            await _predictionPostDecisionAnalysisService.AnalisarAsync(
                request,
                tipoTag,
                ehAnomalia,
                score,
                contexto,
                resultadoInterpretacao,
                multivariatePromotionApplied,
                multivariateOperationalEvidence);

        var analiseRelacionadas =
            postDecisionAnalysis.AnaliseRelacionadas;

        var riscoProcesso =
            postDecisionAnalysis.RiscoProcesso;

        var fatoresRisco =
            postDecisionAnalysis.FatoresRisco;

        resultadoInterpretacao =
            postDecisionAnalysis.ResultadoInterpretacao;

        var tendenciaRisco =
            postDecisionAnalysis.TendenciaRisco;

        var tendenciaValor =
            postDecisionAnalysis.TendenciaValor;

        var analiseFeedbacks =
            postDecisionAnalysis.AnaliseFeedbacks;

        var mensagem =
            postDecisionAnalysis.Mensagem;

        cancellationToken.ThrowIfCancellationRequested();

        _historicoService.Registrar(
            request.ClienteId,
            request.TagName,
            tipoTag,
            request.Valor,
            score,
            ehAnomalia,
            mensagem,
            contexto?.Criticidade,
            analiseRelacionadas,
            riscoProcesso.ScoreRisco,
            riscoProcesso.Classificacao,
            tendenciaRisco.Tendencia,
            tendenciaValor.Tendencia,
            runtimeAudit
        );

var detectoresExecutados =
    PredictionResponseMapper.MapearDetectores(
        resultadoDetector.DetalhesDetectores);

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

return _predictionResponseBuilder.Build(
    request,
    tipoTag,
    ehAnomalia,
    score,
    mensagem,
    analiseFeedbacks,
    comportamentoReconhecido,
    classificacaoComportamentoReconhecido,
    mediaHistoricaDetector,
    limiteInferior,
    limiteSuperior,
    contexto,
    resultadoInterpretacao,
    processo,
    analiseRelacionadas,
    riscoProcesso,
    fatoresRisco,
    tendenciaRisco,
    tendenciaValor,
    multivariatePromotionApplied,
    multivariateOperationalEvidence,
    runtimeDecision.Source,
    detectoresExecutados);

    }
}