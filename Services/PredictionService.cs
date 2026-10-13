using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Runtime;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Orchestration;

// ServiÃ§o principal de detecÃ§Ã£o de anomalias.
public class PredictionService
{
    /*
     * Limite usado somente para classificar a severidade da interpretaÃ§Ã£o.
     *
     * O ensemble continua sendo o Ãºnico responsÃ¡vel por decidir se existe
     * ou nÃ£o uma anomalia.
     *
     * Caso o limite real usado pelo EnsembleDetectionService seja diferente
     * de 50, altere este valor para manter a classificaÃ§Ã£o de severidade
     * consistente com o ensemble.
     */
    private const double LimiteEnsembleInterpretacao = 50.0;

    private readonly AnaliseRelacionadasService _analiseRelacionadasService;
    private readonly AnomaliaHistoricoService _historicoService;
    private readonly PredictionRiskAnalysisService _predictionRiskAnalysisService;
    private readonly PredictionTrendAnalysisService _predictionTrendAnalysisService;

    private readonly PredictionContextLoader _predictionContextLoader;

    private readonly FeedbackAnomaliaAnaliseService
        _feedbackAnomaliaAnaliseService;

    private readonly EnsembleDetectionService _ensembleDetectionService;

    private readonly PredictionDetectionPreparationService
        _predictionDetectionPreparationService;

private readonly PredictionInterpretationService
    _predictionInterpretationService;

    // B3.18 â€” ponte operacional do runtime real.
    // O DetectorResult Legacy permanece imutÃ¡vel.
    private readonly PredictionRuntimeDecisionService
    _predictionRuntimeDecisionService;

    private readonly RuntimeFunctionalDecisionContextProvider
        _runtimeFunctionalDecisionContextProvider;

    // B3.24.2 / MV01 — caminho multivariado somente observacional.
    // Pode ser null em testes antigos construídos manualmente.
    private readonly MultivariateShadowRuntimeCoordinator?
        _multivariateShadowRuntimeCoordinator;

    // B3.24.2 / MV06 — observação da fusão counterfactual.
    // Nunca altera a decisão operacional nesta etapa.

    // B3.24.2 / MV06.3C — interpretação coerente quando a
    // autoridade operacional vier do multivariado.
    private readonly MultivariateOperationalInterpretationFactory?
        _multivariateOperationalInterpretationFactory;

    // B3.24.2 / MV07.5 — trava de readiness antes de qualquer
    // autoridade operacional multivariada real.
    //
    // Mantidos opcionais para preservar testes/construções manuais antigos.
private readonly PredictionMultivariateDecisionService?
    _predictionMultivariateDecisionService;

private readonly PredictionResponseBuilder
    _predictionResponseBuilder;

    public PredictionService(
        PredictionContextLoader predictionContextLoader,
        AnaliseRelacionadasService analiseRelacionadasService,
        AnomaliaHistoricoService historicoService,
        PredictionRiskAnalysisService predictionRiskAnalysisService,
        PredictionTrendAnalysisService predictionTrendAnalysisService,
        PredictionResponseBuilder predictionResponseBuilder,
        FeedbackAnomaliaAnaliseService feedbackAnomaliaAnaliseService,
        EnsembleDetectionService ensembleDetectionService,
        PredictionDetectionPreparationService predictionDetectionPreparationService,
        PredictionInterpretationService predictionInterpretationService,
        RuntimeFunctionalDecisionContextProvider runtimeFunctionalDecisionContextProvider,
        PredictionRuntimeDecisionService predictionRuntimeDecisionService,
MultivariateShadowRuntimeCoordinator?
    multivariateShadowRuntimeCoordinator = null,

PredictionMultivariateDecisionService?
    predictionMultivariateDecisionService = null,

MultivariateOperationalInterpretationFactory?
    multivariateOperationalInterpretationFactory = null)
    {
        _predictionContextLoader =
    predictionContextLoader;

        _analiseRelacionadasService =
            analiseRelacionadasService;

        _historicoService =
            historicoService;

        _predictionRiskAnalysisService =
            predictionRiskAnalysisService;

        _predictionTrendAnalysisService =
            predictionTrendAnalysisService;

        _feedbackAnomaliaAnaliseService =
            feedbackAnomaliaAnaliseService;

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

        _multivariateOperationalInterpretationFactory =
            multivariateOperationalInterpretationFactory;



        _predictionResponseBuilder =
            predictionResponseBuilder;
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

        /*
         * B3.24.2 / MV01 — MULTIVARIATE SHADOW.
         *
         * A leitura entra no caminho multivariado assim que confirmamos
         * que a tag está ativa. O coordinator é fail-open e não participa
         * de EhAnomalia, Score, Risco, RuntimeDecision ou LLM.
         *
         * Executamos antes da exigência do perfil univariado para que uma
         * falha posterior no caminho univariado não faça o buffer perder
         * uma leitura multivariada válida.
         */
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

        /*
         * O ensemble executa os detectores e continua sendo o Ãºnico
         * responsÃ¡vel pela decisÃ£o de anomalia.
         */
        var resultadoDetector =
            _ensembleDetectionService.Detectar(
                entradaDetector
            );

        /*
         * B3.18 â€” contexto funcional REAL do runtime.
         *
         * Mantemos o DetectorResult Legacy imutÃ¡vel e alimentamos a mesma
         * cadeia de regime/candidate/incidente validada no benchmark.
         *
         * O provider mantÃ©m estado efÃªmero por cliente/tag e devolve apenas
         * o contexto necessÃ¡rio para a decisÃ£o funcional.
         */
        var runtimeFunctionalContext =
            _runtimeFunctionalDecisionContextProvider.Evaluate(
                entradaDetector,
                resultadoDetector);

        /*
         * B3.25 - CONTEXTO COMPORTAMENTAL PROPAGADO AO PONTO DE DECISÃO.
         *
         * Nesta etapa a informação é apenas observacional.
         * Ela NÃO altera:
         * - RuntimeFunctionalDecisionContext;
         * - RuntimeFunctionalDecisionCandidate;
         * - RuntimeDecision;
         * - EhAnomalia;
         * - score;
         * - severidade;
         * - lifecycle de incidente.
         *
         * O objetivo é deixar explícito, no mesmo ponto em que o runtime
         * funcional é avaliado, qual comportamento foi reconhecido e qual
         * sua classificação semântica.
         */
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

        /*
         * B3.24.2 / MV06.2 + MV06.3B
         *
         * 1) Calcula a decisão counterfactual.
         * 2) Passa essa proposta pelo resolver operacional independente.
         *
         * A promoção operacional continua protegida por feature flag própria.
         * Enquanto Enabled=false, o resolver devolve a RuntimeDecision atual
         * sem nenhuma alteração.
         */
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
                _multivariateOperationalInterpretationFactory is not null,
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

        /*
         * B3.19 â€” snapshot de auditoria.
         *
         * A decisÃ£o Legacy original, a decisÃ£o operacional e o contexto
         * funcional sÃ£o preservados separadamente para persistÃªncia.
         */
        var runtimeAudit =
            RuntimeDecisionAuditData.From(
                runtimeDecision,
                runtimeFunctionalContext);

        /*
         * ConstrÃ³i o contexto consolidado para interpretaÃ§Ã£o.
         *
         * Essa etapa combina:
         * - resultado tÃ©cnico do ensemble;
         * - dados histÃ³ricos;
         * - informaÃ§Ãµes operacionais cadastradas para a tag.
         */
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

        /*
         * A partir daqui os efeitos externos usam a decisÃ£o operacional.
         *
         * Na etapa atual da B3.18 ela Ã© semanticamente idÃªntica ao Legacy.
         * O valor original continua disponÃ­vel em
         * resultadoDetector.EhAnomalia e nÃ£o Ã© alterado.
         */
        var ehAnomalia =
            runtimeDecision.EmitAnomaly;

        var score =
            resultadoDetector.Score;

        cancellationToken.ThrowIfCancellationRequested();

        var analiseRelacionadas =
            await _analiseRelacionadasService.Analisar(
                request.ClienteId,
                request.TagName
            );

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
            /*
             * O score usado pelo RiskScoreService continua sendo o score
             * univariado. O score 0–100 do multivariado permanece somente
             * na evidência multivariada.
             */
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
                    request.Valor
                );

        /*
         * A mensagem principal continua sendo gerada pela lÃ³gica
         * existente para preservar compatibilidade com o sistema.
         *
         * A interpretaÃ§Ã£o detalhada serÃ¡ retornada separadamente
         * na propriedade Interpretacao.
         */
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
                    tendenciaValor
                );

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