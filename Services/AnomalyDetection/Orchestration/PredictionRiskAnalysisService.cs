namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionRiskAnalysisService
{
    private readonly RiskScoreService
        _riskScoreService;

    private readonly ExplicacaoRiscoService
        _explicacaoRiscoService;

    public PredictionRiskAnalysisService(
        RiskScoreService riskScoreService,
        ExplicacaoRiscoService explicacaoRiscoService)
    {
        _riskScoreService =
            riskScoreService;

        _explicacaoRiscoService =
            explicacaoRiscoService;
    }

    public PredictionRiskAnalysisResult Analisar(
        bool ehAnomalia,
        double score,
        TagContextoIa? contexto,
        AnaliseRelacionadasResultado analiseRelacionadas)
    {
        var risco =
            _riskScoreService.Calcular(
                ehAnomalia,
                score,
                contexto?.Criticidade,
                analiseRelacionadas);

        var fatores =
            _explicacaoRiscoService.GerarFatores(
                score,
                contexto,
                analiseRelacionadas,
                risco);

        return new PredictionRiskAnalysisResult(
            risco,
            fatores);
    }
}

public sealed record PredictionRiskAnalysisResult(
    ResultadoRiscoProcesso Risco,
    List<string> Fatores);