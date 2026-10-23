using System.Diagnostics;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services.AnomalyDetection.Observability;
using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Runtime;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Orchestration;

public class PredictionService
{
    private readonly PredictionContextLoader _predictionContextLoader;

    private readonly IIntelligenceLabDiagnosticCollector? _diagnosticCollector;
    private readonly DiagnosticVersionMetadata? _diagnosticVersion;

    private readonly PredictionDetectionPreparationService
        _predictionDetectionPreparationService;

    private readonly PredictionPostDecisionAnalysisService
        _predictionPostDecisionAnalysisService;

    private readonly PredictionDecisionOrchestrationService
        _predictionDecisionOrchestrationService;

    private readonly MultivariateShadowRuntimeCoordinator?
        _multivariateShadowRuntimeCoordinator;

    private readonly PredictionFinalizationService
        _predictionFinalizationService;

    public PredictionService(
        PredictionContextLoader predictionContextLoader,
        PredictionPostDecisionAnalysisService predictionPostDecisionAnalysisService,
        PredictionFinalizationService predictionFinalizationService,
        PredictionDecisionOrchestrationService predictionDecisionOrchestrationService,
        PredictionDetectionPreparationService predictionDetectionPreparationService,
        MultivariateShadowRuntimeCoordinator? multivariateShadowRuntimeCoordinator = null,
        IIntelligenceLabDiagnosticCollector? diagnosticCollector = null,
        DiagnosticVersionMetadata? diagnosticVersion = null)
    {
        _predictionContextLoader =
            predictionContextLoader;

        _predictionPostDecisionAnalysisService =
            predictionPostDecisionAnalysisService;

        _predictionFinalizationService =
            predictionFinalizationService;

        _predictionDecisionOrchestrationService =
            predictionDecisionOrchestrationService;

        _predictionDetectionPreparationService =
            predictionDetectionPreparationService;

        _multivariateShadowRuntimeCoordinator =
            multivariateShadowRuntimeCoordinator;

        _diagnosticCollector = diagnosticCollector;
        _diagnosticVersion = diagnosticVersion;
    }

    public async Task<AnomaliaResponse> Detectar(
        AnomaliaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        // Nenhum cronometro ou coleta na configuracao padrao.
        var diagnosticTimer =
            _diagnosticCollector is not null && _diagnosticVersion is not null
                ? Stopwatch.StartNew()
                : null;
        var diagnosticReceivedAtUtc = diagnosticTimer is not null
            ? DateTimeOffset.UtcNow
            : (DateTimeOffset?)null;

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

var decisionOrchestration =
    await _predictionDecisionOrchestrationService.ExecutarAsync(
        request,
        entradaDetector,
        dataHoraAtual,
        multivariateShadowResults,
        baselineComportamentalAplicado,
        comportamentoReconhecido,
        codigoComportamentoReconhecido,
        classificacaoComportamentoReconhecido,
        _predictionPostDecisionAnalysisService,
        cancellationToken);

        var resultadoDetector =
            decisionOrchestration.ResultadoDetector;

        var runtimeDecision =
            decisionOrchestration.RuntimeDecision;

        var runtimeAudit =
            decisionOrchestration.RuntimeAudit;

        var resultadoInterpretacao =
            decisionOrchestration.ResultadoInterpretacao;

        var multivariatePromotionApplied =
            decisionOrchestration.MultivariatePromotionApplied;

        var multivariateOperationalEvidence =
            decisionOrchestration.MultivariateOperationalEvidence;

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

        cancellationToken.ThrowIfCancellationRequested();

        var response = _predictionFinalizationService.Finalizar(
            new PredictionFinalizationInput(
                request,
                tipoTag,
                ehAnomalia,
                score,
                comportamentoReconhecido,
                classificacaoComportamentoReconhecido,
                mediaHistoricaDetector,
                limiteInferior,
                limiteSuperior,
                contexto,
                processo,
                postDecisionAnalysis,
                multivariatePromotionApplied,
                multivariateOperationalEvidence,
                runtimeDecision.Source,
                resultadoDetector,
                runtimeAudit));

        // A decisao e a persistencia operacional ja foram finalizadas.
        // Uma falha na telemetria nunca pode alterar o retorno HTTP.
        if (diagnosticTimer is not null)
        {
            diagnosticTimer.Stop();
            PredictionDiagnosticCapture.TryCapture(
                _diagnosticCollector,
                _diagnosticVersion,
                request,
                dataHoraAtual,
                preparation.BaselineEfetivo,
                resultadoDetector,
                runtimeDecision,
                diagnosticTimer.Elapsed,
                diagnosticReceivedAtUtc!.Value);
        }

        return response;
    }
}