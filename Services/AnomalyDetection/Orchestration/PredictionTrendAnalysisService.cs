using IAApi.Services.Diagnostics;
namespace IAApi.Services.AnomalyDetection.Orchestration;

public sealed class PredictionTrendAnalysisService
{
    private readonly PerformanceDiagnostics? _performanceDiagnostics;

    private readonly TendenciaRiscoService
        _tendenciaRiscoService;

    private readonly TendenciaValorService
        _tendenciaValorService;

    public PredictionTrendAnalysisService(
        TendenciaRiscoService tendenciaRiscoService,
        TendenciaValorService tendenciaValorService,
        PerformanceDiagnostics? performanceDiagnostics = null)
    {
        _performanceDiagnostics = performanceDiagnostics;

        _tendenciaRiscoService =
            tendenciaRiscoService;

        _tendenciaValorService =
            tendenciaValorService;
    }

    public PredictionTrendAnalysisResult Analisar(
        string clienteId,
        string tagName,
        string tipoTag,
        PredictionRawHistorySnapshot? rawHistorySnapshot = null)
    {
        var tendenciaRisco =
            PerformanceDiagnostics.Measure(
            _performanceDiagnostics, clienteId, tagName, "Trend.Risk",
            () => _tendenciaRiscoService.Analisar(
                clienteId,
                tagName));

        var tendenciaValor =
            PerformanceDiagnostics.Measure(
            _performanceDiagnostics, clienteId, tagName, "Trend.Value",
            () => _tendenciaValorService.Analisar(
                clienteId,
                tagName,
                tipoTag,
                rawHistorySnapshot));

        return new PredictionTrendAnalysisResult(
            tendenciaRisco,
            tendenciaValor);
    }
}

public sealed record PredictionTrendAnalysisResult(
    ResultadoTendenciaRisco TendenciaRisco,
    ResultadoTendenciaValor TendenciaValor);
