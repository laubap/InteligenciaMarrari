using IAApi.IntelligenceLab.Observability;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;

namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionDetectionPreparationService
{
    private readonly IHistoricoService _servicoHistorico;

    private readonly OperationalRegimeBaselineProvider?
        _operationalRegimeBaselineProvider;

    private readonly BehaviorRuntimeBaselineProvider?
        _behaviorRuntimeBaselineProvider;

    public PredictionDetectionPreparationService(
        IHistoricoService servicoHistorico,
        OperationalRegimeBaselineProvider?
            operationalRegimeBaselineProvider = null,
        BehaviorRuntimeBaselineProvider?
            behaviorRuntimeBaselineProvider = null)
    {
        _servicoHistorico =
            servicoHistorico;

        _operationalRegimeBaselineProvider =
            operationalRegimeBaselineProvider;

        _behaviorRuntimeBaselineProvider =
            behaviorRuntimeBaselineProvider;
    }

    public async Task<PredictionDetectionPreparationResult>
        PrepararAsync(
            AnomaliaRequest request,
            TagPerfilIa perfil,
            string tipoTag,
            DateTime dataHoraAtual,
            CancellationToken cancellationToken = default)
    {
        var historicoBruto =
            _servicoHistorico.BuscarHistorico(
                request.ClienteId,
                request.TagName,
                tipoTag);

        if (historicoBruto == null)
        {
            throw new Exception(
                $"O serviço de histórico retornou uma resposta nula " +
                $"para a tag '{request.TagName}'."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

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

        var baselineRegimeNaoAplicado =
            false;

        var baselineRegimeFalhou =
            false;

        var baselineComportamentalNaoAplicado =
            false;

        var baselineComportamentalFalhou =
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
                else
                {
                    baselineRegimeNaoAplicado =
                        true;
                }
            }
            catch (Exception ex)
            {
                baselineRegimeFalhou =
                    true;

                Console.WriteLine(
                    $"BASELINE POR REGIME FAIL-OPEN " +
                    $"Cliente={request.ClienteId} " +
                    $"Tag={request.TagName} " +
                    $"Erro={ex.Message}");
            }
        }

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
                else
                {
                    baselineComportamentalNaoAplicado =
                        true;
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                baselineComportamentalFalhou =
                    true;

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
        Console.WriteLine("HISTÓRICO PREPARADO PARA DETECÇÃO");
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
            $"Registros válidos: " +
            $"{qualidadeHistorico.ValidPoints}");

        Console.WriteLine(
            $"Valores inválidos removidos: " +
            $"{qualidadeHistorico.InvalidValues}");

        Console.WriteLine(
            $"Timestamps inválidos removidos: " +
            $"{qualidadeHistorico.InvalidTimestamps}");

        Console.WriteLine(
            $"Timestamps duplicados: " +
            $"{qualidadeHistorico.DuplicateTimestamps}");

        Console.WriteLine(
            $"Transições fora de ordem: " +
            $"{qualidadeHistorico.OutOfOrderTransitions}");

        Console.WriteLine(
            $"Cadência mediana: " +
            $"{qualidadeHistorico.MedianIntervalMinutes:F3} min");

        Console.WriteLine(
            $"Maior gap: " +
            $"{qualidadeHistorico.LargestGapMinutes:F3} min");

        Console.WriteLine(
            $"Gap crítico: " +
            $"{qualidadeHistorico.HasCriticalGap}");

        if (historicoDetector.Count > 0)
        {
            Console.WriteLine(
                $"Primeiro registro: " +
                $"{historicoDetector.First().DataHora:yyyy-MM-dd HH:mm:ss}"
            );

            Console.WriteLine(
                $"Último registro: " +
                $"{historicoDetector.Last().DataHora:yyyy-MM-dd HH:mm:ss}"
            );
        }

        Console.WriteLine("================================");

        if (historicoDetector.Count < 10)
        {
            throw new Exception(
                $"Histórico insuficiente para analisar a tag " +
                $"'{request.TagName}'. Foram encontrados apenas " +
                $"{historicoDetector.Count} pontos válidos."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        var baselineEfetivo =
            CriarBaselineEfetivo(
                entradaDetector,
                baselineRegimeAplicado,
                baselineComportamentalAplicado,
                valorRegimeOperacional,
                comportamentoAplicado,
                snapshotComportamentalAplicado,
                baselineRegimeNaoAplicado,
                baselineRegimeFalhou,
                baselineComportamentalNaoAplicado,
                baselineComportamentalFalhou);

        return new PredictionDetectionPreparationResult(
            entradaDetector,
            baselineComportamentalAplicado,
            comportamentoReconhecido,
            codigoComportamentoReconhecido,
            classificacaoComportamentoReconhecido,
            mediaHistoricaDetector,
            baselineEfetivo);
    }

    private static BaselineDiagnostic? CriarBaselineEfetivo(
        DetectorInput entradaDetector,
        bool baselineRegimeAplicado,
        bool baselineComportamentalAplicado,
        double? valorRegimeOperacional,
        string? comportamentoAplicado,
        int? snapshotComportamentalAplicado,
        bool baselineRegimeNaoAplicado,
        bool baselineRegimeFalhou,
        bool baselineComportamentalNaoAplicado,
        bool baselineComportamentalFalhou)
    {
        var kind =
            baselineRegimeAplicado
                ? BaselineDiagnosticKind.OperationalRegime
                : baselineComportamentalAplicado
                    ? BaselineDiagnosticKind.BehaviorMemory
                    : BaselineDiagnosticKind.Global;

        var reason =
            CriarMotivoBaselineEfetivo(
                kind,
                baselineRegimeNaoAplicado,
                baselineRegimeFalhou,
                baselineComportamentalNaoAplicado,
                baselineComportamentalFalhou);

        try
        {
            return new BaselineDiagnostic(
                kind,
                true,
                reason,
                entradaDetector.MediaHistorica,
                entradaDetector.DesvioPadraoHistorico,
                entradaDetector.VariacaoMinimaRelevante,
                entradaDetector.ConfiancaVariacaoMinima,
                entradaDetector.Historico.Count,
                baselineRegimeAplicado
                    ? valorRegimeOperacional
                    : null,
                baselineComportamentalAplicado
                    ? comportamentoAplicado
                    : null,
                baselineComportamentalAplicado
                    ? snapshotComportamentalAplicado
                    : null);
        }
        catch (ArgumentException)
        {
            /*
             * O contrato diagnóstico possui invariantes mais restritivas
             * do que o DetectorInput legado. A indisponibilidade de um
             * snapshot jamais pode interferir na detecção operacional.
             */
            return null;
        }
    }

    private static string CriarMotivoBaselineEfetivo(
        BaselineDiagnosticKind kind,
        bool baselineRegimeNaoAplicado,
        bool baselineRegimeFalhou,
        bool baselineComportamentalNaoAplicado,
        bool baselineComportamentalFalhou)
    {
        return kind switch
        {
            BaselineDiagnosticKind.OperationalRegime =>
                "Baseline de regime operacional aplicado.",

            BaselineDiagnosticKind.BehaviorMemory when baselineRegimeFalhou =>
                "Baseline de memória comportamental aplicado após falha na resolução do regime operacional.",

            BaselineDiagnosticKind.BehaviorMemory when baselineRegimeNaoAplicado =>
                "Baseline de memória comportamental aplicado após o regime operacional não ser aplicado.",

            BaselineDiagnosticKind.BehaviorMemory =>
                "Baseline de memória comportamental aplicado.",

            BaselineDiagnosticKind.Global when baselineRegimeFalhou &&
                baselineComportamentalFalhou =>
                "Baseline global do perfil ativo mantido após falhas nas resoluções de regime operacional e memória comportamental.",

            BaselineDiagnosticKind.Global when baselineRegimeFalhou =>
                "Baseline global do perfil ativo mantido após falha na resolução do regime operacional.",

            BaselineDiagnosticKind.Global when baselineComportamentalFalhou =>
                "Baseline global do perfil ativo mantido após falha na resolução da memória comportamental.",

            BaselineDiagnosticKind.Global when baselineRegimeNaoAplicado &&
                baselineComportamentalNaoAplicado =>
                "Baseline global do perfil ativo mantido porque os baselines de regime operacional e memória comportamental não foram aplicados.",

            BaselineDiagnosticKind.Global when baselineRegimeNaoAplicado =>
                "Baseline global do perfil ativo mantido porque o baseline de regime operacional não foi aplicado.",

            BaselineDiagnosticKind.Global when baselineComportamentalNaoAplicado =>
                "Baseline global do perfil ativo mantido porque o baseline de memória comportamental não foi aplicado.",

            _ =>
                "Baseline global do perfil ativo selecionado."
        };
    }
}

public sealed record PredictionDetectionPreparationResult(
    DetectorInput EntradaDetector,
    bool BaselineComportamentalAplicado,
    bool ComportamentoReconhecido,
    string? CodigoComportamentoReconhecido,
    string? ClassificacaoComportamentoReconhecido,
    double MediaHistoricaDetector,
    BaselineDiagnostic? BaselineEfetivo);
