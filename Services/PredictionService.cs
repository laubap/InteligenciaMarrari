using System.Text.Json;

// Serviço principal de detecção de anomalias.
public class PredictionService
{
    private readonly AppDbContext _context;
    private readonly AnaliseRelacionadasService _analiseRelacionadasService;
    private readonly AnomaliaHistoricoService _historicoService;
    private readonly RiskScoreService _riskScoreService;
    private readonly ExplicacaoRiscoService _explicacaoRiscoService;
    private readonly TendenciaRiscoService _tendenciaRiscoService;
    private readonly TendenciaValorService _tendenciaValorService;
    private readonly InterpretacaoDependenciasService _interpretacaoDependenciasService;
    private readonly ProcessoAnaliseService _processoAnaliseService;
    private readonly BridgeValorAtualService _bridgeValorAtualService;
    private readonly FeedbackAnomaliaAnaliseService _feedbackAnomaliaAnaliseService;

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
        FeedbackAnomaliaAnaliseService feedbackAnomaliaAnaliseService)
    {
        _context = context;
        _analiseRelacionadasService = analiseRelacionadasService;
        _historicoService = historicoService;
        _riskScoreService = riskScoreService;
        _explicacaoRiscoService = explicacaoRiscoService;
        _tendenciaRiscoService = tendenciaRiscoService;
        _tendenciaValorService = tendenciaValorService;
        _interpretacaoDependenciasService = interpretacaoDependenciasService;
        _processoAnaliseService = processoAnaliseService;
        _bridgeValorAtualService = bridgeValorAtualService;
        _feedbackAnomaliaAnaliseService = feedbackAnomaliaAnaliseService;
    }

    public async Task<AnomaliaResponse> Detectar(AnomaliaRequest request)
    {
        var config = _context.TagsIaConfig
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.TagName == request.TagName &&
                x.IaAtiva);

        if (config == null)
            throw new Exception($"IA não configurada para a tag {request.TagName} do cliente {request.ClienteId}.");

        var perfil = _context.PerfisIa
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.TagName == request.TagName);

        if (perfil == null)
            throw new Exception($"Perfil da tag {request.TagName} ainda não foi treinado. Execute o treinamento de perfil antes da detecção.");

        var contexto = _context.TagsContextoIa
            .FirstOrDefault(x =>
                x.ClienteId == request.ClienteId &&
                x.TagName == request.TagName);

        var processo = _processoAnaliseService.BuscarProcessoDaTag(
            request.ClienteId,
            request.TagName
        );

        var limiteInferior = perfil.Media - (3 * perfil.DesvioPadrao);
        var limiteSuperior = perfil.Media + (3 * perfil.DesvioPadrao);

        var ehAnomalia =
            request.Valor < limiteInferior ||
            request.Valor > limiteSuperior;

        var distanciaDaMedia = Math.Abs(request.Valor - perfil.Media);

        var score = perfil.DesvioPadrao > 0
            ? distanciaDaMedia / perfil.DesvioPadrao
            : 0;

        var analiseRelacionadas = await _analiseRelacionadasService.Analisar(
            request.ClienteId,
            request.TagName
        );

        var riscoProcesso = _riskScoreService.Calcular(
            ehAnomalia,
            score,
            contexto?.Criticidade,
            analiseRelacionadas
        );

        var fatoresRisco = _explicacaoRiscoService.GerarFatores(
            score,
            contexto,
            analiseRelacionadas,
            riscoProcesso
        );

        var tendenciaRisco = _tendenciaRiscoService.Analisar(
            request.ClienteId,
            request.TagName
        );

        var tendenciaValor = _tendenciaValorService.Analisar(
            request.TagName,
            contexto?.TipoRepresentacao ?? "perfil-comportamental"
        );

        var analiseFeedbacks = _feedbackAnomaliaAnaliseService.GerarAnaliseFeedbacks(
            request.ClienteId,
            request.TagName,
            score,
            request.Valor
        );

        var mensagem = MontarMensagemCliente(
            request,
            contexto,
            ehAnomalia,
            score,
            riscoProcesso,
            tendenciaValor
        );



        _historicoService.Registrar(
            request.ClienteId,
            request.TagName,
            contexto?.TipoRepresentacao ?? "perfil-comportamental",
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

        return new AnomaliaResponse
        {
            ClienteId = request.ClienteId,
            TagName = request.TagName,
            TipoTag = contexto?.TipoRepresentacao ?? "perfil-comportamental",
            EhAnomalia = ehAnomalia,
            Score = (float)score,

            Mensagem = mensagem,

            FeedbackSemelhante = analiseFeedbacks,

            Resumo = new ResumoAnomaliaDto
            {
                Status = ehAnomalia ? "Anomalia detectada" : "Comportamento normal",
                Conclusao = riscoProcesso.Mensagem
            },

            Perfil = new PerfilComportamentalDto
            {
                ValorRecebido = request.Valor,
                MediaHistorica = perfil.Media,
                LimiteInferior = limiteInferior,
                LimiteSuperior = limiteSuperior,
                Score = score
            },

            Contexto = contexto == null ? null : new ContextoOperacionalDto
            {
                Descricao = contexto.Descricao,
                Criticidade = contexto.Criticidade,
                Equipamento = contexto.Equipamento,
                Area = contexto.Area,
                ObservacaoModoOperacao = contexto.ObservacaoModoOperacao,
                Impactos = LerLista(contexto.Impactos),
                CausasProvaveis = LerLista(contexto.CausasProvaveis),
                AcoesRecomendadas = LerLista(contexto.AcoesRecomendadas),
                ModosOperacao = LerLista(contexto.ModosOperacao)
            },

            Processo = processo == null
                ? null
                : new ProcessoDto
                {
                    Nome = processo.Nome,
                    Descricao = processo.Descricao,
                    Area = processo.Area,
                    Criticidade = processo.Criticidade,
                    Objetivo = processo.Objetivo,
                    CondicaoNormal = processo.CondicaoNormal,
                    ConsequenciasFalha = processo.ConsequenciasFalha,
                    ProcedimentoRecomendado = processo.ProcedimentoRecomendado
                },

            Dependencias = analiseRelacionadas.Resultados.Select(item => new DependenciaAnalisadaDto
            {
                TagName = item.TagName,
                Descricao = item.Descricao,
                ValorAtual = item.ValorAtual,
                Media = item.Media,
                LimiteInferior = item.LimiteInferior,
                LimiteSuperior = item.LimiteSuperior,
                EhAnomalia = item.EhAnomalia,
                Status = item.EhAnomalia ? "anômala" : "normal",
                TipoRelacao = item.TipoRelacao,
                Impacto = item.Impacto,
                Criticidade = item.Criticidade,
                Equipamento = item.Equipamento,
                Area = item.Area,
                Interpretacao = _interpretacaoDependenciasService.Interpretar(item, contexto)
            }).ToList(),

            Risco = new RiscoProcessoDto
            {
                Score = riscoProcesso.ScoreRisco,
                Classificacao = riscoProcesso.Classificacao,
                Mensagem = riscoProcesso.Mensagem,
                Fatores = fatoresRisco
            },

            Tendencias = new TendenciasDto
            {
                TendenciaRisco = tendenciaRisco.Tendencia,
                MensagemTendenciaRisco = tendenciaRisco.Mensagem,
                TendenciaValor = tendenciaValor.Tendencia,
                MensagemTendenciaValor = tendenciaValor.Mensagem
            },
        };
    }

    public async Task<AnomaliaResponse> DetectarValorAtual(string clienteId, string tagName)
    {
        var valores = await _bridgeValorAtualService.BuscarValoresAtuais(
            new List<string> { tagName }
        );

        var valorAtual = valores.FirstOrDefault(x =>
            string.Equals(x.TagName, tagName, StringComparison.OrdinalIgnoreCase));

        if (valorAtual == null || !valorAtual.Encontrado)
            throw new Exception($"Valor atual da tag {tagName} não encontrado no Bridge.");

        

        var request = new AnomaliaRequest
        {
            ClienteId = clienteId,
            TagName = tagName,
            Valor = (float)valorAtual.Valor
        };
        return await Detectar(request);
        
    }

private string MontarMensagemCliente(
    AnomaliaRequest request,
    TagContextoIa? contexto,
    bool ehAnomalia,
    double score,
    ResultadoRiscoProcesso riscoProcesso,
    ResultadoTendenciaValor tendenciaValor)
{
    var descricaoTag = !string.IsNullOrWhiteSpace(contexto?.Descricao)
        ? contexto.Descricao
        : request.TagName;

    if (!ehAnomalia)
    {
        return $"{descricaoTag} está operando dentro do comportamento esperado. Nenhuma ação imediata é necessária.";
    }

    var risco = !string.IsNullOrWhiteSpace(riscoProcesso.Classificacao)
        ? riscoProcesso.Classificacao
        : "não classificado";

    return $"{descricaoTag} apresentou comportamento fora do padrão esperado. Risco: {risco}. Recomenda-se verificar a condição operacional da tag.";
}
    private List<string> LerLista(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return json
                .Split(';', ',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }
}