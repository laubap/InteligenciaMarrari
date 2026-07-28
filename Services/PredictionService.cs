using System.Text.Json;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;

// Serviço principal de detecção de anomalias.
public class PredictionService
{
    /*
     * Limite usado somente para classificar a severidade da interpretação.
     *
     * O ensemble continua sendo o único responsável por decidir se existe
     * ou não uma anomalia.
     *
     * Caso o limite real usado pelo EnsembleDetectionService seja diferente
     * de 50, altere este valor para manter a classificação de severidade
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
    private readonly BridgeValorAtualService _bridgeValorAtualService;

    private readonly FeedbackAnomaliaAnaliseService
        _feedbackAnomaliaAnaliseService;

    private readonly EnsembleDetectionService _ensembleDetectionService;
    private readonly IHistoricoService _servicoHistorico;

    private readonly AnomalyInterpretationContextBuilder
        _interpretationContextBuilder;

    private readonly IAnomalyInterpretationService
        _anomalyInterpretationService;

    public PredictionService(
        AppDbContext context,
        AnaliseRelacionadasService analiseRelacionadasService,
        AnomaliaHistoricoService historicoService,
        RiskScoreService riskScoreService,
        ExplicacaoRiscoService explicacaoRiscoService,
        TendenciaRiscoService tendenciaRiscoService,
        TendenciaValorService tendenciaValorService,
        InterpretacaoDependenciasService interpretacaoDependenciasService,
        ProcessoAnaliseService processoAnaliseService,
        BridgeValorAtualService bridgeValorAtualService,
        FeedbackAnomaliaAnaliseService feedbackAnomaliaAnaliseService,
        EnsembleDetectionService ensembleDetectionService,
        IHistoricoService servicoHistorico,
        AnomalyInterpretationContextBuilder interpretationContextBuilder,
        IAnomalyInterpretationService anomalyInterpretationService)
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

        _bridgeValorAtualService =
            bridgeValorAtualService;

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
                $"IA não configurada para a tag " +
                $"{request.TagName} do cliente " +
                $"{request.ClienteId}."
            );
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
                "ainda não foi treinado. Execute o " +
                "treinamento de perfil antes da detecção."
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
         * Busca o histórico real da tag no Bridge.
         *
         * O segundo parâmetro existe para manter compatibilidade
         * com a interface IHistoricoService. Atualmente o Bridge
         * utiliza apenas o nome da tag para buscar o histórico.
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
                $"O serviço de histórico retornou uma resposta nula " +
                $"para a tag '{request.TagName}'."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        /*
         * Remove leituras inválidas, elimina timestamps duplicados
         * e ordena o histórico cronologicamente.
         */
        var historicoDetector = historicoBruto
            .Where(leitura =>
                leitura != null &&
                !float.IsNaN(leitura.Valor) &&
                !float.IsInfinity(leitura.Valor))
            .GroupBy(leitura => leitura.DataHora)
            .Select(grupo => grupo.Last())
            .OrderBy(leitura => leitura.DataHora)
            .Select(leitura => new HistoricoDetectorPoint
            {
                DataHora = leitura.DataHora,
                Valor = leitura.Valor
            })
            .ToList();

        Console.WriteLine("================================");
        Console.WriteLine("HISTÓRICO PREPARADO PARA DETECÇÃO");
        Console.WriteLine($"Cliente: {request.ClienteId}");
        Console.WriteLine($"Tag: {request.TagName}");
        Console.WriteLine($"Tipo: {tipoTag}");
        Console.WriteLine($"Registros brutos: {historicoBruto.Count}");
        Console.WriteLine($"Registros válidos: {historicoDetector.Count}");

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

        var entradaDetector = new DetectorInput
        {
            ClienteId = request.ClienteId,
            TagName = request.TagName,
            TipoTag = tipoTag,

            ValorAtual = request.Valor,
            DataHoraAtual = DateTime.Now,

            MediaHistorica = perfil.Media,
            DesvioPadraoHistorico = perfil.DesvioPadrao,

            Historico = historicoDetector
        };

        cancellationToken.ThrowIfCancellationRequested();

        /*
         * O ensemble executa os detectores e continua sendo o único
         * responsável pela decisão de anomalia.
         */
        var resultadoDetector =
            _ensembleDetectionService.Detectar(
                entradaDetector
            );

        /*
         * Constrói o contexto consolidado para interpretação.
         *
         * Essa etapa combina:
         * - resultado técnico do ensemble;
         * - dados históricos;
         * - informações operacionais cadastradas para a tag.
         */
AnomalyInterpretationResult resultadoInterpretacao;

if (resultadoDetector.EhAnomalia)
{
    var contextoInterpretacao =
        await _interpretationContextBuilder.CriarAsync(
            entradaDetector,
            resultadoDetector,
            LimiteEnsembleInterpretacao,
            cancellationToken
        );

    resultadoInterpretacao =
        await _anomalyInterpretationService.InterpretarAsync(
            contextoInterpretacao,
            cancellationToken
        );
}
else
{
    resultadoInterpretacao = new AnomalyInterpretationResult
    {
        Resumo =
            "Nenhuma anomalia foi identificada.",

        InterpretacaoTecnica =
            "Os detectores indicaram que o comportamento atual da tag permanece dentro do padrão esperado.",

        Evidencias = new List<string>
        {
            "Nenhum detector identificou comportamento anômalo."
        },

        PossiveisCausas = new(),

        Recomendacoes = new()
        {
            "Continuar monitorando normalmente."
        },

        Severidade = "Normal",

        GeradoPorLlm = false,

        ModeloUtilizado = "Não utilizado",

        DataGeracao = DateTime.UtcNow
    };
}

        var limiteInferior =
            resultadoDetector.LimiteInferior;

        var limiteSuperior =
            resultadoDetector.LimiteSuperior;

        var ehAnomalia =
            resultadoDetector.EhAnomalia;

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
         * A mensagem principal continua sendo gerada pela lógica
         * existente para preservar compatibilidade com o sistema.
         *
         * A interpretação detalhada será retornada separadamente
         * na propriedade Interpretacao.
         */
        var mensagem =
            MontarMensagemCliente(
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
            tendenciaValor.Tendencia
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
Console.WriteLine("========== INTERPRETAÇÃO FINAL ==========");
Console.WriteLine($"Resumo: {resultadoInterpretacao.Resumo}");
Console.WriteLine($"Interpretação: {resultadoInterpretacao.InterpretacaoTecnica}");
Console.WriteLine($"Severidade: {resultadoInterpretacao.Severidade}");
Console.WriteLine($"Gerado por LLM: {resultadoInterpretacao.GeradoPorLlm}");

Console.WriteLine("Evidências:");
foreach (var item in resultadoInterpretacao.Evidencias)
{
    Console.WriteLine($" - {item}");
}

Console.WriteLine("Possíveis causas:");
foreach (var item in resultadoInterpretacao.PossiveisCausas)
{
    Console.WriteLine($" - {item}");
}

Console.WriteLine("Recomendações:");
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
                    : "Comportamento normal",

                Conclusao =
                    riscoProcesso.Mensagem
            },

            Perfil = new PerfilComportamentalDto
            {
                ValorRecebido =
                    request.Valor,

                MediaHistorica =
                    perfil.Media,

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
                        LerLista(contexto.Impactos),

                    CausasProvaveis =
                        LerLista(contexto.CausasProvaveis),

                    AcoesRecomendadas =
                        LerLista(contexto.AcoesRecomendadas),

                    ModosOperacao =
                        LerLista(contexto.ModosOperacao)
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
                analiseRelacionadas.Resultados
                    .Select(item =>
                        new DependenciaAnalisadaDto
                        {
                            TagName =
                                item.TagName,

                            Descricao =
                                item.Descricao,

                            ValorAtual =
                                item.ValorAtual,

                            Media =
                                item.Media,

                            LimiteInferior =
                                item.LimiteInferior,

                            LimiteSuperior =
                                item.LimiteSuperior,

                            EhAnomalia =
                                item.EhAnomalia,

                            Status = item.EhAnomalia
                                ? "anômala"
                                : "normal",

                            TipoRelacao =
                                item.TipoRelacao,

                            Impacto =
                                item.Impacto,

                            Criticidade =
                                item.Criticidade,

                            Equipamento =
                                item.Equipamento,

                            Area =
                                item.Area,

                            Interpretacao =
                                _interpretacaoDependenciasService
                                    .Interpretar(
                                        item,
                                        contexto
                                    )
                        })
                    .ToList(),

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

            Detectores =
                detectoresExecutados
        };
    }

    public async Task<AnomaliaResponse> DetectarValorAtual(
        string clienteId,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var valores =
            await _bridgeValorAtualService
                .BuscarValoresAtuais(
                    new List<string>
                    {
                        tagName
                    });

        cancellationToken.ThrowIfCancellationRequested();

        var valorAtual =
            valores.FirstOrDefault(x =>
                string.Equals(
                    x.TagName,
                    tagName,
                    StringComparison.OrdinalIgnoreCase
                ));

        if (
            valorAtual == null ||
            !valorAtual.Encontrado)
        {
            throw new Exception(
                $"Valor atual da tag {tagName} " +
                "não encontrado no Bridge."
            );
        }

        var request = new AnomaliaRequest
        {
            ClienteId =
                clienteId,

            TagName =
                tagName,

            Valor =
                (float)valorAtual.Valor
        };

        return await Detectar(
            request,
            cancellationToken
        );
    }

    private string MontarMensagemCliente(
        AnomaliaRequest request,
        TagContextoIa? contexto,
        bool ehAnomalia,
        double score,
        ResultadoRiscoProcesso riscoProcesso,
        ResultadoTendenciaValor tendenciaValor)
    {
        var descricaoTag =
            !string.IsNullOrWhiteSpace(
                contexto?.Descricao)
                ? contexto.Descricao
                : request.TagName;

        if (!ehAnomalia)
        {
            return
                $"{descricaoTag} está operando " +
                "dentro do comportamento esperado. " +
                "Nenhuma ação imediata é necessária.";
        }

        var risco =
            !string.IsNullOrWhiteSpace(
                riscoProcesso.Classificacao)
                ? riscoProcesso.Classificacao
                : "não classificado";

        return
            $"{descricaoTag} apresentou comportamento " +
            $"fora do padrão esperado. Risco: {risco}. " +
            "Recomenda-se verificar a condição " +
            "operacional da tag.";
    }

    private static List<string> LerLista(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return
                JsonSerializer.Deserialize<List<string>>(json)
                ?? new List<string>();
        }
        catch
        {
            return json
                .Split(';', ',')
                .Select(x => x.Trim())
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }
}