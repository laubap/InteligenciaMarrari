using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Runtime;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;
using IAApi.Services.AnomalyDetection.Presentation;

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

    private readonly AppDbContext _context;
    private readonly AnaliseRelacionadasService _analiseRelacionadasService;
    private readonly AnomaliaHistoricoService _historicoService;
    private readonly RiskScoreService _riskScoreService;
    private readonly ExplicacaoRiscoService _explicacaoRiscoService;
    private readonly TendenciaRiscoService _tendenciaRiscoService;
    private readonly TendenciaValorService _tendenciaValorService;

    private readonly InterpretacaoDependenciasService
        _interpretacaoDependenciasService;

    private readonly ProcessoAnaliseService _processoAnaliseService;

    private readonly FeedbackAnomaliaAnaliseService
        _feedbackAnomaliaAnaliseService;

    private readonly EnsembleDetectionService _ensembleDetectionService;
    private readonly IHistoricoService _servicoHistorico;

    // Baseline condicionado por regime operacional.
    // Mantido opcional para preservar testes e construções manuais antigas.
    private readonly OperationalRegimeBaselineProvider?
        _operationalRegimeBaselineProvider;

    // B3.25 - reconhecimento comportamental em shadow.
// Nesta etapa não possui autoridade sobre o baseline dos detectores.
    private readonly BehaviorRuntimeBaselineProvider?
        _behaviorRuntimeBaselineProvider;

    private readonly AnomalyInterpretationContextBuilder
        _interpretationContextBuilder;

    private readonly IAnomalyInterpretationService
        _anomalyInterpretationService;

    // B3.18 â€” ponte operacional do runtime real.
    // O DetectorResult Legacy permanece imutÃ¡vel.
    private readonly RuntimeAnomalyDecisionResolver
        _runtimeDecisionResolver;

    private readonly IOptions<RuntimeAnomalyDecisionOptions>
        _runtimeDecisionOptions;

    private readonly RuntimeFunctionalDecisionCandidateFactory
        _runtimeFunctionalDecisionCandidateFactory;

    private readonly RuntimeFunctionalDecisionContextProvider
        _runtimeFunctionalDecisionContextProvider;

    private readonly RuntimeDecisionMetrics
        _runtimeDecisionMetrics;

    // B3.24.2 / MV01 — caminho multivariado somente observacional.
    // Pode ser null em testes antigos construídos manualmente.
    private readonly MultivariateShadowRuntimeCoordinator?
        _multivariateShadowRuntimeCoordinator;

    // B3.24.2 / MV06 — observação da fusão counterfactual.
    // Nunca altera a decisão operacional nesta etapa.
    private readonly MultivariateDecisionCounterfactualObserver?
        _multivariateDecisionCounterfactualObserver;

    // B3.24.2 / MV06.3 — resolver da promoção operacional.
    // Continua protegido por feature flag própria, desligada por padrão.
    private readonly MultivariateOperationalFusionResolver?
        _multivariateOperationalFusionResolver;

    private readonly IOptions<MultivariateOperationalPromotionOptions>?
        _multivariateOperationalPromotionOptions;

    // B3.24.2 / MV06.3C — interpretação coerente quando a
    // autoridade operacional vier do multivariado.
    private readonly MultivariateOperationalInterpretationFactory?
        _multivariateOperationalInterpretationFactory;

    // B3.24.2 / MV07.5 — trava de readiness antes de qualquer
    // autoridade operacional multivariada real.
    //
    // Mantidos opcionais para preservar testes/construções manuais antigos.
    private readonly MultivariateOperationalReadinessService?
        _multivariateOperationalReadinessService;

    private readonly IOptions<MultivariateOperationalReadinessOptions>?
        _multivariateOperationalReadinessOptions;

    private readonly PredictionDependencyMapper
    _predictionDependencyMapper;

    public PredictionService(
        AppDbContext context,
        AnaliseRelacionadasService analiseRelacionadasService,
        AnomaliaHistoricoService historicoService,
        RiskScoreService riskScoreService,
        ExplicacaoRiscoService explicacaoRiscoService,
        TendenciaRiscoService tendenciaRiscoService,
        TendenciaValorService tendenciaValorService,
        InterpretacaoDependenciasService interpretacaoDependenciasService,
        PredictionDependencyMapper predictionDependencyMapper,
        ProcessoAnaliseService processoAnaliseService,
        FeedbackAnomaliaAnaliseService feedbackAnomaliaAnaliseService,
        EnsembleDetectionService ensembleDetectionService,
        IHistoricoService servicoHistorico,
        AnomalyInterpretationContextBuilder interpretationContextBuilder,
        IAnomalyInterpretationService anomalyInterpretationService,
        RuntimeAnomalyDecisionResolver runtimeDecisionResolver,
        IOptions<RuntimeAnomalyDecisionOptions> runtimeDecisionOptions,
        RuntimeFunctionalDecisionCandidateFactory runtimeFunctionalDecisionCandidateFactory,
        RuntimeFunctionalDecisionContextProvider runtimeFunctionalDecisionContextProvider,
        RuntimeDecisionMetrics runtimeDecisionMetrics,
        MultivariateShadowRuntimeCoordinator? multivariateShadowRuntimeCoordinator = null,
        MultivariateDecisionCounterfactualObserver? multivariateDecisionCounterfactualObserver = null,
        MultivariateOperationalFusionResolver? multivariateOperationalFusionResolver = null,
        IOptions<MultivariateOperationalPromotionOptions>? multivariateOperationalPromotionOptions = null,
        MultivariateOperationalInterpretationFactory? multivariateOperationalInterpretationFactory = null,
        MultivariateOperationalReadinessService? multivariateOperationalReadinessService = null,
        IOptions<MultivariateOperationalReadinessOptions>? multivariateOperationalReadinessOptions = null,
        OperationalRegimeBaselineProvider? operationalRegimeBaselineProvider = null,
        BehaviorRuntimeBaselineProvider? behaviorRuntimeBaselineProvider = null)
    {
        _context = context;

        _analiseRelacionadasService =
            analiseRelacionadasService;

        _historicoService =
            historicoService;

        _riskScoreService =
            riskScoreService;

        _explicacaoRiscoService =
            explicacaoRiscoService;

        _tendenciaRiscoService =
            tendenciaRiscoService;

        _tendenciaValorService =
            tendenciaValorService;

        _interpretacaoDependenciasService =
            interpretacaoDependenciasService;

        _processoAnaliseService =
            processoAnaliseService;

        _feedbackAnomaliaAnaliseService =
            feedbackAnomaliaAnaliseService;

        _ensembleDetectionService =
            ensembleDetectionService;

        _servicoHistorico =
            servicoHistorico;

        _interpretationContextBuilder =
            interpretationContextBuilder;

        _anomalyInterpretationService =
            anomalyInterpretationService;

        _runtimeDecisionResolver =
            runtimeDecisionResolver;

        _runtimeDecisionOptions =
            runtimeDecisionOptions;

        _runtimeFunctionalDecisionCandidateFactory =
            runtimeFunctionalDecisionCandidateFactory;

        _runtimeFunctionalDecisionContextProvider =
            runtimeFunctionalDecisionContextProvider;

        _runtimeDecisionMetrics =
            runtimeDecisionMetrics;

        _multivariateShadowRuntimeCoordinator =
            multivariateShadowRuntimeCoordinator;

        _multivariateDecisionCounterfactualObserver =
            multivariateDecisionCounterfactualObserver;

        _multivariateOperationalFusionResolver =
            multivariateOperationalFusionResolver;

        _multivariateOperationalPromotionOptions =
            multivariateOperationalPromotionOptions;

        _multivariateOperationalInterpretationFactory =
            multivariateOperationalInterpretationFactory;

        _multivariateOperationalReadinessService =
            multivariateOperationalReadinessService;

        _multivariateOperationalReadinessOptions =
            multivariateOperationalReadinessOptions;

        _operationalRegimeBaselineProvider =
            operationalRegimeBaselineProvider;

        _behaviorRuntimeBaselineProvider =
            behaviorRuntimeBaselineProvider;

        _predictionDependencyMapper =
            predictionDependencyMapper;
    }

    public async Task<AnomaliaResponse> Detectar(
        AnomaliaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var config = _context.TagsIaConfig
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.TagName == request.TagName &&
                x.IaAtiva);

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

        var perfil = _context.PerfisIa
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.TagName == request.TagName);

        if (perfil == null)
        {
            throw new Exception(
                $"Perfil da tag {request.TagName} " +
                "ainda nÃ£o foi treinado. Execute o " +
                "treinamento de perfil antes da detecÃ§Ã£o."
            );
        }

        var contexto = _context.TagsContextoIa
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.TagName == request.TagName);

        var processo =
            _processoAnaliseService.BuscarProcessoDaTag(
                request.ClienteId,
                request.TagName
            );

        var tipoTag =
            contexto?.TipoRepresentacao
            ?? "perfil-comportamental";

        /*
         * Busca o histÃ³rico real da tag no Bridge.
         *
         * O segundo parÃ¢metro existe para manter compatibilidade
         * com a interface IHistoricoService. Atualmente o Bridge
         * utiliza apenas o nome da tag para buscar o histÃ³rico.
         */
        var historicoBruto =
            _servicoHistorico.BuscarHistorico(
                request.ClienteId,
                request.TagName,
                tipoTag
            );

        if (historicoBruto == null)
        {
            throw new Exception(
                $"O serviÃ§o de histÃ³rico retornou uma resposta nula " +
                $"para a tag '{request.TagName}'."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        /*
         * A sanitizaÃ§Ã£o agora Ã© responsabilidade exclusiva de DetectorInput.
         *
         * Aqui apenas convertemos as leituras recebidas para o contrato
         * comum. Na atribuiÃ§Ã£o de Historico serÃ£o:
         * - removidos valores/timestamps invÃ¡lidos;
         * - consolidados timestamps duplicados;
         * - corrigida a ordenaÃ§Ã£o quando necessÃ¡rio;
         * - medidos cadÃªncia e gaps.
         */
        var historicoRecebido =
            historicoBruto
                .Where(leitura =>
                    leitura != null)
                .Select(leitura =>
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            leitura.DataHora,

                        Valor =
                            leitura.Valor
                    })
                .ToList();

        /*
         * Baseline padrão: mantém exatamente o perfil global já existente.
         *
         * Somente quando houver configuração válida e o provider conseguir
         * construir um baseline por regime é que estes valores são
         * substituídos antes da criação do DetectorInput.
         */
        var mediaHistoricaDetector =
            perfil.Media;

        var desvioPadraoHistoricoDetector =
            perfil.DesvioPadrao;

        var variacaoMinimaRelevanteDetector =
            perfil.VariacaoMinimaRelevante;

        var confiancaVariacaoMinimaDetector =
            perfil.ConfiancaDeadband;

        var baselineRegimeAplicado =
            false;

        var baselineComportamentalAplicado =
            false;

        var comportamentoReconhecido =
            false;

        string? codigoComportamentoReconhecido =
            null;

        string? classificacaoComportamentoReconhecido =
            null;

        string? comportamentoAplicado =
            null;

        int? snapshotComportamentalAplicado =
            null;

        double? valorRegimeOperacional =
            null;

        if (_operationalRegimeBaselineProvider is not null)
        {
            try
            {
                var regimeBaseline =
                    _operationalRegimeBaselineProvider.Resolve(
                        request.ClienteId,
                        request.TagName,
                        historicoBruto,
                        dataHoraAtual);

                if (regimeBaseline.Applied &&
                    regimeBaseline.Statistics is not null)
                {
                    historicoRecebido =
                        regimeBaseline.History
                            .ToList();

                    mediaHistoricaDetector =
                        regimeBaseline.Statistics.Media;

                    desvioPadraoHistoricoDetector =
                        regimeBaseline.Statistics.DesvioPadrao;

                    variacaoMinimaRelevanteDetector =
                        regimeBaseline.Statistics.DeadbandRecomendado;

                    confiancaVariacaoMinimaDetector =
                        regimeBaseline.Statistics.ConfiancaDeadband;

                    baselineRegimeAplicado =
                        true;

                    valorRegimeOperacional =
                        regimeBaseline.CurrentRegimeValue;

                    Console.WriteLine(
                        $"BASELINE POR REGIME APLICADO " +
                        $"Cliente={request.ClienteId} " +
                        $"Tag={request.TagName} " +
                        $"Regime={valorRegimeOperacional?.ToString("G6") ?? "-"} " +
                        $"Amostras={regimeBaseline.SelectedSamples} " +
                        $"Media={mediaHistoricaDetector:G6} " +
                        $"Desvio={desvioPadraoHistoricoDetector:G6} " +
                        $"Deadband={variacaoMinimaRelevanteDetector:G6} " +
                        $"Confianca={confiancaVariacaoMinimaDetector:F1}");
                }
            }
            catch (Exception ex)
            {
                /*
                 * Fail-open obrigatório:
                 * qualquer falha inesperada da nova funcionalidade
                 * preserva o perfil/histórico global já existente.
                 */
                Console.WriteLine(
                    $"BASELINE POR REGIME FAIL-OPEN " +
                    $"Cliente={request.ClienteId} " +
                    $"Tag={request.TagName} " +
                    $"Erro={ex.Message}");
            }
        }

        /*
 * B3.25 - MEMÓRIA COMPORTAMENTAL EM SHADOW.
 *
 * A prioridade continua sendo:
 *
 * regime operacional explícito
 *     >
 * comportamento aprendido
 *     >
 * baseline global
 *
 * Nesta primeira etapa o comportamento aprendido é apenas
 * reconhecido e observado.
 *
 * Nenhuma estatística do detector é alterada.
 */
if (!baselineRegimeAplicado &&
    _behaviorRuntimeBaselineProvider is not null)
{
    try
    {
        var behaviorShadow =
            await _behaviorRuntimeBaselineProvider.ResolveAsync(
                request.ClienteId,
                request.TagName,
                historicoBruto,
                dataHoraAtual,
                cancellationToken);

        comportamentoReconhecido =
            behaviorShadow.Recognized;

        codigoComportamentoReconhecido =
            behaviorShadow.BehaviorCode;

        classificacaoComportamentoReconhecido =
            behaviorShadow.BehaviorClassification;

        Console.WriteLine(
            $"B3.25 BEHAVIOR SHADOW " +
            $"Cliente={request.ClienteId} " +
            $"Tag={request.TagName} " +
            $"Recognized={behaviorShadow.Recognized} " +
            $"Behavior={behaviorShadow.BehaviorCode ?? "-"} " +
            $"Classification={behaviorShadow.BehaviorClassification ?? "-"} " +
            $"CanApplyBaseline={behaviorShadow.CanApplyBaseline} " +
            $"Snapshot={behaviorShadow.SnapshotVersion?.ToString() ?? "-"} " +
            $"Compatibility=" +
            $"{behaviorShadow.CompatibilityConfidence?.ToString("F4") ?? "-"} " +
            $"Reason={behaviorShadow.Reason}");

            /*
 * B3.25 - BASELINE COMPORTAMENTAL COUNTERFACTUAL.
 *
 * Só observamos o baseline que poderia ser utilizado quando
 * o comportamento reconhecido está semanticamente autorizado
 * como baseline normal.
 *
 * Nenhum valor abaixo é aplicado ao DetectorInput nesta etapa.
 */
/*
 * B3.25 - APLICAÇÃO DO BASELINE COMPORTAMENTAL.
 *
 * Só possui autoridade quando:
 *
 * - o comportamento foi reconhecido;
 * - está semanticamente autorizado como Normal;
 * - todas as estatísticas necessárias são válidas;
 * - existe histórico comportamental utilizável.
 *
 * Este bloco só é alcançado quando nenhum regime operacional
 * explícito foi aplicado.
 */
if (behaviorShadow.Recognized &&
    behaviorShadow.CanApplyBaseline &&
    behaviorShadow.Media is double behaviorMedia &&
    behaviorShadow.DesvioPadrao is double behaviorDesvio &&
    behaviorShadow.DeadbandRecomendado is double behaviorDeadband &&
    behaviorShadow.ConfiancaDeadband is double behaviorConfianca &&
    double.IsFinite(behaviorMedia) &&
    double.IsFinite(behaviorDesvio) &&
    double.IsFinite(behaviorDeadband) &&
    double.IsFinite(behaviorConfianca) &&
    behaviorDesvio >= 0 &&
    behaviorDeadband >= 0 &&
    behaviorShadow.History.Count > 0)
{
    /*
     * Materializa o histórico antes de alterar qualquer variável
     * operacional. Assim, uma eventual falha nesta preparação ainda
     * preserva integralmente o baseline global.
     */
    var behaviorHistory =
        behaviorShadow.History.ToList();

    historicoRecebido =
        behaviorHistory;

    mediaHistoricaDetector =
        behaviorMedia;

    desvioPadraoHistoricoDetector =
        behaviorDesvio;

    variacaoMinimaRelevanteDetector =
        behaviorDeadband;

    confiancaVariacaoMinimaDetector =
        behaviorConfianca;

    baselineComportamentalAplicado =
        true;

    comportamentoAplicado =
        behaviorShadow.BehaviorCode;

    snapshotComportamentalAplicado =
        behaviorShadow.SnapshotVersion;

    Console.WriteLine(
        $"B3.25 BEHAVIOR BASELINE APPLIED " +
        $"Cliente={request.ClienteId} " +
        $"Tag={request.TagName} " +
        $"Behavior={comportamentoAplicado ?? "-"} " +
        $"Snapshot={snapshotComportamentalAplicado?.ToString() ?? "-"} " +
        $"Media={mediaHistoricaDetector:G6} " +
        $"Desvio={desvioPadraoHistoricoDetector:G6} " +
        $"Deadband={variacaoMinimaRelevanteDetector:G6} " +
        $"Confianca={confiancaVariacaoMinimaDetector:F1} " +
        $"Amostras={behaviorHistory.Count}");
}
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception ex)
    {
        /*
         * Fail-open:
         * qualquer falha no reconhecimento comportamental
         * preserva integralmente o runtime existente.
         */
        Console.WriteLine(
            $"B3.25 BEHAVIOR SHADOW FAIL-OPEN " +
            $"Cliente={request.ClienteId} " +
            $"Tag={request.TagName} " +
            $"Erro={ex.Message}");
    }
}

        var entradaDetector =
            new DetectorInput
            {
        ClienteId =
            request.ClienteId,

        TagName =
            request.TagName,

        TipoTag =
            tipoTag,

        ValorAtual =
            request.Valor,

        DataHoraAtual =
            dataHoraAtual,

        MediaHistorica =
            mediaHistoricaDetector,

        DesvioPadraoHistorico =
            desvioPadraoHistoricoDetector,

        /*
         * B3.24.1
         *
         * Preserva no runtime o deadband aprendido durante
         * o treinamento do perfil.
         *
         * Sem estes campos, o BaselineProtectionProfile
         * ignora o valor persistido e recalcula o P95
         * diretamente do histórico da detecção.
         */
        VariacaoMinimaRelevante =
            variacaoMinimaRelevanteDetector,

        ConfiancaVariacaoMinima =
            confiancaVariacaoMinimaDetector,

        VariacaoMinimaDefinidaPeloCliente =
            false,

        Historico =
            historicoRecebido
    };
        var historicoDetector =
            entradaDetector.Historico;

        var qualidadeHistorico =
            entradaDetector.QualidadeHistorico;

        Console.WriteLine("================================");
        Console.WriteLine("HISTÃ“RICO PREPARADO PARA DETECÃ‡ÃƒO");
        Console.WriteLine($"Cliente: {request.ClienteId}");
        Console.WriteLine($"Tag: {request.TagName}");
        Console.WriteLine($"Tipo: {tipoTag}");
var origemBaselineDetector =
    baselineRegimeAplicado
        ? "regime operacional"
        : baselineComportamentalAplicado
            ? "comportamento aprendido"
            : "global";

Console.WriteLine(
    $"Baseline estatístico: {origemBaselineDetector}");

    if (baselineComportamentalAplicado)
{
    Console.WriteLine(
        $"Comportamento aplicado: " +
        $"{comportamentoAplicado ?? "-"} " +
        $"Snapshot={snapshotComportamentalAplicado?.ToString() ?? "-"}");
}

        if (baselineRegimeAplicado)
        {
            Console.WriteLine(
                $"Valor do regime operacional: " +
                $"{valorRegimeOperacional?.ToString("G6") ?? "-"}");
        }

        Console.WriteLine(
            $"Registros recebidos: " +
            $"{qualidadeHistorico.OriginalPoints}");
        Console.WriteLine(
            $"Registros vÃ¡lidos: " +
            $"{qualidadeHistorico.ValidPoints}");
        Console.WriteLine(
            $"Valores invÃ¡lidos removidos: " +
            $"{qualidadeHistorico.InvalidValues}");
        Console.WriteLine(
            $"Timestamps invÃ¡lidos removidos: " +
            $"{qualidadeHistorico.InvalidTimestamps}");
        Console.WriteLine(
            $"Timestamps duplicados: " +
            $"{qualidadeHistorico.DuplicateTimestamps}");
        Console.WriteLine(
            $"TransiÃ§Ãµes fora de ordem: " +
            $"{qualidadeHistorico.OutOfOrderTransitions}");
        Console.WriteLine(
            $"CadÃªncia mediana: " +
            $"{qualidadeHistorico.MedianIntervalMinutes:F3} min");
        Console.WriteLine(
            $"Maior gap: " +
            $"{qualidadeHistorico.LargestGapMinutes:F3} min");
        Console.WriteLine(
            $"Gap crÃ­tico: " +
            $"{qualidadeHistorico.HasCriticalGap}");

        if (historicoDetector.Count > 0)
        {
            Console.WriteLine(
                $"Primeiro registro: " +
                $"{historicoDetector.First().DataHora:yyyy-MM-dd HH:mm:ss}"
            );

            Console.WriteLine(
                $"Ãšltimo registro: " +
                $"{historicoDetector.Last().DataHora:yyyy-MM-dd HH:mm:ss}"
            );
        }

        Console.WriteLine("================================");

        if (historicoDetector.Count < 10)
        {
            throw new Exception(
                $"HistÃ³rico insuficiente para analisar a tag " +
                $"'{request.TagName}'. Foram encontrados apenas " +
                $"{historicoDetector.Count} pontos vÃ¡lidos."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

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

        var runtimeFunctionalCandidate =
            _runtimeFunctionalDecisionCandidateFactory.Create(
                resultadoDetector,
                runtimeFunctionalContext);

        var runtimeDecision =
            _runtimeDecisionResolver.Resolve(
                resultadoDetector,
                _runtimeDecisionOptions.Value,
                request.ClienteId,
                runtimeFunctionalCandidate);

        _runtimeDecisionMetrics.Record(
            runtimeDecision);

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
        MultivariateDecisionCounterfactual?
            multivariateCounterfactual =
                null;

        if (_multivariateDecisionCounterfactualObserver is not null &&
            multivariateShadowResults.Count > 0)
        {
            try
            {
                multivariateCounterfactual =
                    _multivariateDecisionCounterfactualObserver.Observe(
                        runtimeDecision,
                        request.ClienteId,
                        request.TagName,
                        dataHoraAtual,
                        multivariateShadowResults);
            }
            catch (Exception ex)
            {
                // Fail-open obrigatório:
                // observabilidade multivariada jamais pode derrubar
                // a detecção operacional existente.
                Console.WriteLine(
                    $"B3.24.2 MULTIVARIATE FUSION COUNTERFACTUAL FAIL-OPEN " +
                    $"Cliente={request.ClienteId} " +
                    $"Tag={request.TagName} " +
                    $"Erro={ex.Message}");
            }
        }

        MultivariateOperationalFusionResult?
            multivariateOperationalFusion =
                null;

        if (_multivariateOperationalFusionResolver is not null &&
            _multivariateOperationalPromotionOptions is not null &&
            multivariateCounterfactual is not null)
        {
            try
            {
                var resolvedFusion =
                    _multivariateOperationalFusionResolver.Resolve(
                        runtimeDecision,
                        multivariateCounterfactual,
                        _multivariateOperationalPromotionOptions.Value,
                        request.ClienteId,
                        resultadoDetector.Score);

                /*
                 * MV06.3C + MV07.5
                 *
                 * Uma promoção real só pode entrar no runtime quando:
                 * 1) existe interpretação multivariada coerente;
                 * 2) existe evidência multivariada identificável;
                 * 3) o grupo/modelo está READY para autoridade operacional.
                 *
                 * Importante:
                 * - o resolver estável NÃO é alterado;
                 * - feature OFF continua sem consultar readiness;
                 * - qualquer falha de readiness preserva a RuntimeDecision
                 *   operacional já calculada pelo caminho existente.
                 */
                var podeAplicarResolvedFusion =
                    true;

                var fusionLogSource =
                    resolvedFusion.Source;

                if (resolvedFusion.PromotionApplied)
                {
                    if (_multivariateOperationalInterpretationFactory is null ||
                        resolvedFusion.MultivariateEvidence is null)
                    {
                        podeAplicarResolvedFusion =
                            false;

                        fusionLogSource =
                            "MultivariateInterpretationUnavailable";

                        Console.WriteLine(
                            $"B3.24.2 MULTIVARIATE OPERATIONAL FUSION BLOCKED " +
                            $"Cliente={request.ClienteId} " +
                            $"Tag={request.TagName} " +
                            "Reason=MultivariateInterpretationUnavailable");
                    }
                    else if (_multivariateOperationalReadinessService is null ||
                             _multivariateOperationalReadinessOptions is null)
                    {
                        /*
                         * Fail-closed para a NOVA autoridade multivariada:
                         * se a trava de readiness não estiver disponível,
                         * a promoção não entra.
                         *
                         * O runtime univariado existente continua funcionando.
                         */
                        podeAplicarResolvedFusion =
                            false;

                        fusionLogSource =
                            "MultivariateOperationalReadinessUnavailable";

                        Console.WriteLine(
                            $"B3.24.2 MULTIVARIATE OPERATIONAL READINESS BLOCKED " +
                            $"Cliente={request.ClienteId} " +
                            $"Tag={request.TagName} " +
                            $"Grupo={resolvedFusion.MultivariateEvidence.GroupName} " +
                            "Reason=ReadinessServiceUnavailable");
                    }
                    else
                    {
                        var readiness =
                            await _multivariateOperationalReadinessService
                                .AvaliarAsync(
                                    request.ClienteId,
                                    resolvedFusion.MultivariateEvidence.GroupName,
                                    _multivariateOperationalReadinessOptions.Value,
                                    cancellationToken:
                                        cancellationToken);

                        if (!readiness.ReadyForOperationalAuthority)
                        {
                            podeAplicarResolvedFusion =
                                false;

                            fusionLogSource =
                                "MultivariateOperationalReadinessBlocked";

                            var blockers =
                                readiness.Blockers.Count == 0
                                    ? "-"
                                    : string.Join(
                                        ",",
                                        readiness.Blockers);

                            Console.WriteLine(
                                $"B3.24.2 MULTIVARIATE OPERATIONAL READINESS BLOCKED " +
                                $"Cliente={request.ClienteId} " +
                                $"Tag={request.TagName} " +
                                $"Grupo={resolvedFusion.MultivariateEvidence.GroupName} " +
                                $"Blockers={blockers}");
                        }
                        else
                        {
                            Console.WriteLine(
                                $"B3.24.2 MULTIVARIATE OPERATIONAL READINESS READY " +
                                $"Cliente={request.ClienteId} " +
                                $"Tag={request.TagName} " +
                                $"Grupo={resolvedFusion.MultivariateEvidence.GroupName} " +
                                $"SampleCount={readiness.ModelSampleCount}");
                        }
                    }
                }

                if (podeAplicarResolvedFusion)
                {
                    multivariateOperationalFusion =
                        resolvedFusion;

                    runtimeDecision =
                        resolvedFusion.Decision;
                }

                Console.WriteLine(
                    $"B3.24.2 MULTIVARIATE OPERATIONAL FUSION " +
                    $"Cliente={request.ClienteId} " +
                    $"Tag={request.TagName} " +
                    $"Applied={multivariateOperationalFusion?.PromotionApplied ?? false} " +
                    $"Source={fusionLogSource} " +
                    $"EmitAnomaly={runtimeDecision.EmitAnomaly} " +
                    $"EmitNewEvidence={runtimeDecision.EmitNewEvidence} " +
                    $"UnivariateScore={resolvedFusion.UnivariateScore:F4} " +
                    $"MultivariateScore=" +
                    $"{resolvedFusion.MultivariateEvidence?.Score.ToString("F2") ?? "-"}");
            }
            catch (Exception ex)
            {
                /*
                 * Fail-open da autoridade multivariada:
                 * qualquer falha preserva a RuntimeDecision que já havia
                 * sido calculada pelo caminho operacional existente.
                 */
                Console.WriteLine(
                    $"B3.24.2 MULTIVARIATE OPERATIONAL FUSION FAIL-OPEN " +
                    $"Cliente={request.ClienteId} " +
                    $"Tag={request.TagName} " +
                    $"Erro={ex.Message}");
            }
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
AnomalyInterpretationResult? resultadoInterpretacao =
    null;

/*
 * Se a autoridade veio do multivariado, não chamamos a interpretação
 * univariada/LLM. A interpretação multivariada será construída logo após
 * o cálculo de risco, pois sua severidade vem da classificação operacional.
 */
if (!multivariatePromotionApplied)
{
    if (runtimeDecision.RequiresNewInterpretation)
    {
        var contextoInterpretacao =
            await _interpretationContextBuilder.CriarAsync(
                entradaDetector,
                resultadoDetector,
                LimiteEnsembleInterpretacao,
                runtimeDecision.EmitAnomaly,
                cancellationToken
            );

        resultadoInterpretacao =
            await _anomalyInterpretationService.InterpretarAsync(
                contextoInterpretacao,
                cancellationToken
            );
    }
    else if (runtimeDecision.IsAnomalyContinuation)
    {
        /*
         * B3.22.5 V3d.2
         *
         * A condição continua operacionalmente anômala, porém não existe
         * nova evidência funcional. Preservamos a anomalia na resposta,
         * risco e persistência, mas não repetimos interpretação por LLM.
         */
        var contextoContinuacao =
            await _interpretationContextBuilder.CriarAsync(
                entradaDetector,
                resultadoDetector,
                LimiteEnsembleInterpretacao,
                runtimeDecision.EmitAnomaly,
                cancellationToken
            );

        var increasingNoiseContinuation =
            string.Equals(
                runtimeDecision.Source,
                "IncreasingNoiseContinuation",
                StringComparison.OrdinalIgnoreCase);

        resultadoInterpretacao =
            new AnomalyInterpretationResult
            {
                Resumo =
                    "A anomalia permanece ativa e está em acompanhamento.",

                InterpretacaoTecnica =
                    increasingNoiseContinuation
                        ? "A expansão sustentada da variabilidade permanece ativa. " +
                          "Esta leitura representa a continuidade da mesma condição " +
                          "já identificada, sem nova evidência funcional independente."
                        : "A condição anômala previamente identificada permanece ativa. " +
                          "Esta leitura é uma continuação e não representa uma nova " +
                          "evidência funcional independente.",

                Evidencias =
                    increasingNoiseContinuation
                        ? new List<string>
                        {
                            "A evidência de aumento sustentado de variabilidade permanece ativa.",
                            "Nenhuma nova interpretação foi iniciada para esta leitura de continuação."
                        }
                        : new List<string>
                        {
                            "A condição funcional de anomalia permanece ativa.",
                            "Nenhuma nova interpretação foi iniciada para esta leitura de continuação."
                        },

                PossiveisCausas =
                    new(),

                Recomendacoes =
                    new()
                    {
                        "Continuar acompanhando a evolução das próximas leituras."
                    },

                Severidade =
                    contextoContinuacao.Severidade,

                GeradoPorLlm =
                    false,

                ModeloUtilizado =
                    "Continuação determinística",

                DataGeracao =
                    DateTime.UtcNow
            };
    }
    else
    {
        var comportamentoAnormalConhecido =
            comportamentoReconhecido &&
            string.Equals(
                classificacaoComportamentoReconhecido,
                "KnownAbnormal",
                StringComparison.Ordinal);

        resultadoInterpretacao =
            new AnomalyInterpretationResult
            {
                Resumo =
                    comportamentoAnormalConhecido
                        ? $"Comportamento anormal conhecido " +
                          $"{codigoComportamentoReconhecido ?? "-"} reconhecido, " +
                          "sem nova anomalia detectada."
                        : "Nenhuma anomalia foi identificada.",

                InterpretacaoTecnica =
                    comportamentoAnormalConhecido
                        ? $"O padrão atual corresponde ao comportamento " +
                          $"anormal conhecido {codigoComportamentoReconhecido ?? "-"}. " +
                          "Os detectores não identificaram nova evidência funcional " +
                          "de anomalia nesta leitura."
                        : "Os detectores indicaram que o comportamento atual da tag " +
                          "permanece dentro do padrão esperado.",

                Evidencias =
                    comportamentoAnormalConhecido
                        ? new List<string>
                        {
                            $"Comportamento reconhecido: " +
                            $"{codigoComportamentoReconhecido ?? "-"}.",
                            "Classificação semântica: KnownAbnormal.",
                            "Nenhum detector identificou nova evidência " +
                            "funcional de anomalia nesta leitura."
                        }
                        : new List<string>
                        {
                            "Nenhum detector identificou comportamento anômalo."
                        },

                PossiveisCausas =
                    new(),

                Recomendacoes =
                    comportamentoAnormalConhecido
                        ? new List<string>
                        {
                            "Continuar acompanhando o comportamento conhecido " +
                            "e observar novas evidências dos detectores."
                        }
                        : new List<string>
                        {
                            "Continuar monitorando normalmente."
                        },

                Severidade =
                    "Normal",

                GeradoPorLlm =
                    false,

                ModeloUtilizado =
                    comportamentoAnormalConhecido
                        ? "Contexto comportamental determinístico"
                        : "Não utilizado",

                DataGeracao =
                    DateTime.UtcNow
            };
    }
}

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

        var riscoProcesso =
            _riskScoreService.Calcular(
                ehAnomalia,
                score,
                contexto?.Criticidade,
                analiseRelacionadas
            );

        var fatoresRisco =
            _explicacaoRiscoService.GerarFatores(
                score,
                contexto,
                analiseRelacionadas,
                riscoProcesso
            );

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

        var tendenciaRisco =
            _tendenciaRiscoService.Analisar(
                request.ClienteId,
                request.TagName
            );

        var tendenciaValor =
            _tendenciaValorService.Analisar(
                request.ClienteId,
                request.TagName,
                tipoTag
            );

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
            resultadoDetector.DetalhesDetectores
                .Select(detector =>
                    new DetectorExecutadoDto
                    {
                        Nome =
                            detector.Nome,

                        Aplicavel =
                            detector.Aplicavel,

                        VotouAnomalia =
                            detector.VotouAnomalia,

                        ScoreNormalizado =
                            detector.ScoreNormalizado,

                        ScoreOriginal =
                            detector.ScoreOriginal,

                        Peso =
                            detector.Peso,

                        Explicacao =
                            detector.Explicacao,

                        Metricas =
                            detector.Metricas
                            ?? new Dictionary<string, double>()
                    })
                .ToList();

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

            Resumo = new ResumoAnomaliaDto
            {
                Status = ehAnomalia
                    ? "Anomalia detectada"
                    : comportamentoReconhecido &&
                      string.Equals(
                          classificacaoComportamentoReconhecido,
                          "KnownAbnormal",
                          StringComparison.Ordinal)
                        ? "Comportamento anormal conhecido"
                        : "Comportamento normal",

                Conclusao =
                    riscoProcesso.Mensagem
            },

            Perfil = new PerfilComportamentalDto
            {
                ValorRecebido =
                    request.Valor,

                MediaHistorica =
                    mediaHistoricaDetector,

                LimiteInferior =
                    limiteInferior,

                LimiteSuperior =
                    limiteSuperior,

                Score =
                    score
            },

            Contexto = contexto == null
                ? null
                : new ContextoOperacionalDto
                {
                    Descricao =
                        contexto.Descricao,

                    Criticidade =
                        contexto.Criticidade,

                    Equipamento =
                        contexto.Equipamento,

                    Area =
                        contexto.Area,

                    ObservacaoModoOperacao =
                        contexto.ObservacaoModoOperacao,

                    Impactos =
                       PredictionResponseFormatter.LerLista( contexto.Impactos),

                    CausasProvaveis =
                        PredictionResponseFormatter.LerLista(contexto.CausasProvaveis),

                    AcoesRecomendadas =
                        PredictionResponseFormatter.LerLista(contexto.AcoesRecomendadas),

                    ModosOperacao =
                        PredictionResponseFormatter.LerLista(contexto.ModosOperacao)
                },

            Interpretacao = new InterpretacaoAnomaliaDto
            {
                Resumo =
                    resultadoInterpretacao.Resumo,

                InterpretacaoTecnica =
                    resultadoInterpretacao.InterpretacaoTecnica,

                Evidencias =
                    resultadoInterpretacao.Evidencias
                    ?? new List<string>(),

                PossiveisCausas =
                    resultadoInterpretacao.PossiveisCausas
                    ?? new List<string>(),

                Recomendacoes =
                    resultadoInterpretacao.Recomendacoes
                    ?? new List<string>(),

                Severidade =
                    resultadoInterpretacao.Severidade,

                GeradoPorLlm =
                    resultadoInterpretacao.GeradoPorLlm,

                ModeloUtilizado =
                    resultadoInterpretacao.ModeloUtilizado,

                DataGeracao =
                    resultadoInterpretacao.DataGeracao
            },

            Processo = processo == null
                ? null
                : new ProcessoDto
                {
                    Nome =
                        processo.Nome,

                    Descricao =
                        processo.Descricao,

                    Area =
                        processo.Area,

                    Criticidade =
                        processo.Criticidade,

                    Objetivo =
                        processo.Objetivo,

                    CondicaoNormal =
                        processo.CondicaoNormal,

                    ConsequenciasFalha =
                        processo.ConsequenciasFalha,

                    ProcedimentoRecomendado =
                        processo.ProcedimentoRecomendado
                },

Dependencias =
    _predictionDependencyMapper.Mapear(
        analiseRelacionadas,
        contexto),

            Risco = new RiscoProcessoDto
            {
                Score =
                    riscoProcesso.ScoreRisco,

                Classificacao =
                    riscoProcesso.Classificacao,

                Mensagem =
                    riscoProcesso.Mensagem,

                Fatores =
                    fatoresRisco
            },

            Tendencias = new TendenciasDto
            {
                TendenciaRisco =
                    tendenciaRisco.Tendencia,

                MensagemTendenciaRisco =
                    tendenciaRisco.Mensagem,

                TendenciaValor =
                    tendenciaValor.Tendencia,

                MensagemTendenciaValor =
                    tendenciaValor.Mensagem
            },

            Multivariado =
                multivariatePromotionApplied &&
                multivariateOperationalEvidence is not null
                    ? new MultivariateAnomalyDto
                    {
                        PromocaoOperacional =
                            true,

                        Origem =
                            runtimeDecision.Source,

                        Grupo =
                            multivariateOperationalEvidence.GroupName,

                        MinuteIndex =
                            multivariateOperationalEvidence.MinuteIndex,

                        IdadeMinutos =
                            multivariateOperationalEvidence.AgeMinutes,

                        ScoreNormalizado =
                            multivariateOperationalEvidence.Score,

                        LimiteScore =
                            multivariateOperationalEvidence.Threshold,

                        MahalanobisSquared =
                            multivariateOperationalEvidence.MahalanobisSquared,

                        ScoreUnivariado =
                            score,

                        Tags =
                            multivariateOperationalEvidence.Tags.ToList()
                    }
                    : null,

            Detectores =
                detectoresExecutados
        };
    }
}