using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionDecisionOrchestrationService
{
    private const double LimiteEnsembleInterpretacao = 50.0;

    private readonly EnsembleDetectionService
        _ensembleDetectionService;

    private readonly PredictionInterpretationService
        _predictionInterpretationService;

    private readonly RuntimeFunctionalDecisionContextProvider
        _runtimeFunctionalDecisionContextProvider;

    private readonly PredictionRuntimeDecisionService
        _predictionRuntimeDecisionService;

    private readonly PredictionMultivariateDecisionService?
        _predictionMultivariateDecisionService;

    public PredictionDecisionOrchestrationService(
        EnsembleDetectionService ensembleDetectionService,
        PredictionInterpretationService predictionInterpretationService,
        RuntimeFunctionalDecisionContextProvider runtimeFunctionalDecisionContextProvider,
        PredictionRuntimeDecisionService predictionRuntimeDecisionService,
        PredictionMultivariateDecisionService? predictionMultivariateDecisionService = null)
    {
        _ensembleDetectionService =
            ensembleDetectionService;

        _predictionInterpretationService =
            predictionInterpretationService;

        _runtimeFunctionalDecisionContextProvider =
            runtimeFunctionalDecisionContextProvider;

        _predictionRuntimeDecisionService =
            predictionRuntimeDecisionService;

        _predictionMultivariateDecisionService =
            predictionMultivariateDecisionService;
    }

public async Task<PredictionDecisionOrchestrationResult> ExecutarAsync(
    AnomaliaRequest request,
    DetectorInput entradaDetector,
    DateTime dataHoraAtual,
    IReadOnlyList<MultivariateDetectionResult> multivariateShadowResults,
    bool baselineComportamentalAplicado,
    bool comportamentoReconhecido,
    string? codigoComportamentoReconhecido,
    string? classificacaoComportamentoReconhecido,
    PredictionPostDecisionAnalysisService predictionPostDecisionAnalysisService,
    CancellationToken cancellationToken = default)
    {
        var resultadoDetector =
            _ensembleDetectionService.Detectar(
                entradaDetector);

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
                    predictionPostDecisionAnalysisService
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

        return new PredictionDecisionOrchestrationResult(
            resultadoDetector,
            runtimeDecision,
            runtimeAudit,
            resultadoInterpretacao,
            multivariatePromotionApplied,
            multivariateOperationalEvidence);
    }
}

public sealed record PredictionDecisionOrchestrationResult(
    DetectorResult ResultadoDetector,
    RuntimeAnomalyDecision RuntimeDecision,
    RuntimeDecisionAuditData RuntimeAudit,
    AnomalyInterpretationResult? ResultadoInterpretacao,
    bool MultivariatePromotionApplied,
    MultivariateDecisionEvidence? MultivariateOperationalEvidence);