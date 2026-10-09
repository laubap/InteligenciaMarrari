namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionTrendAnalysisService
{
    private readonly TendenciaRiscoService
        _tendenciaRiscoService;

    private readonly TendenciaValorService
        _tendenciaValorService;

    public PredictionTrendAnalysisService(
        TendenciaRiscoService tendenciaRiscoService,
        TendenciaValorService tendenciaValorService)
    {
        _tendenciaRiscoService =
            tendenciaRiscoService;

        _tendenciaValorService =
            tendenciaValorService;
    }

    public PredictionTrendAnalysisResult Analisar(
        string clienteId,
        string tagName,
        string tipoTag)
    {
        var tendenciaRisco =
            _tendenciaRiscoService.Analisar(
                clienteId,
                tagName);

        var tendenciaValor =
            _tendenciaValorService.Analisar(
                clienteId,
                tagName,
                tipoTag);

        return new PredictionTrendAnalysisResult(
            tendenciaRisco,
            tendenciaValor);
    }
}

public sealed record PredictionTrendAnalysisResult(
    ResultadoTendenciaRisco TendenciaRisco,
    ResultadoTendenciaValor TendenciaValor);