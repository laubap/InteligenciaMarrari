using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

/// <summary>
/// Resultado da coleta de histórico longo utilizada pela memória comportamental.
///
/// Nesta etapa o histórico é apenas:
/// - buscado no Bridge/PowerServer;
/// - convertido para o formato central de detecção;
/// - sanitizado.
///
/// Nenhuma descoberta de comportamento ou limpeza estatística por IQR
/// acontece aqui.
/// </summary>
public sealed record BehaviorHistoryCollectionResult
{
    public string ClienteId { get; init; } = "";

    public string TagName { get; init; } = "";

    public DateTime PeriodoInicio { get; init; }

    public DateTime PeriodoFim { get; init; }

    public IReadOnlyList<LeituraBruta> Historico { get; init; } =
        Array.Empty<LeituraBruta>();

    public DetectionInputQuality Qualidade { get; init; } =
        DetectionInputQuality.Empty;

    public int QuantidadeLeituras =>
        Historico.Count;
}

/// <summary>
/// Coleta histórico longo de uma tag para o pipeline de memória comportamental.
///
/// Este serviço não altera o fluxo de detecção em tempo real.
/// Ele utiliza BuscarHistoricoPorPeriodo exclusivamente para aprendizagem
/// e descoberta de comportamentos históricos.
///
/// Regra temporal desta etapa:
/// a memória comportamental preserva exatamente o relógio entregue pelo
/// historiador. Não converte fuso e não altera DateTimeKind.
/// </summary>
public sealed class BehaviorHistoryCollectionService
{
    private readonly IHistoricoService _historicoService;

    public BehaviorHistoryCollectionService(
        IHistoricoService historicoService)
    {
        _historicoService =
            historicoService ??
            throw new ArgumentNullException(
                nameof(historicoService));
    }

    public BehaviorHistoryCollectionResult Collect(
        string clienteId,
        string tagName,
        DateTime inicio,
        DateTime fim)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            throw new ArgumentException(
                "ClienteId é obrigatório.",
                nameof(clienteId));
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException(
                "TagName é obrigatória.",
                nameof(tagName));
        }

        if (fim <= inicio)
        {
            throw new ArgumentException(
                "O fim do período deve ser maior que o início.",
                nameof(fim));
        }

        var historicoBruto =
            _historicoService.BuscarHistoricoPorPeriodo(
                clienteId,
                tagName,
                inicio,
                fim)
            ?? new List<LeituraBruta>();

        var pontosDetector =
            historicoBruto
                .Where(leitura =>
                    leitura is not null)
                .Select(leitura =>
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            leitura.DataHora,

                        Valor =
                            leitura.Valor
                    })
                .ToArray();

        var sanitizado =
            DetectionHistorySanitizer.Sanitize(
                pontosDetector);

        var historicoSanitizado =
            sanitizado.History
                .Select(ponto =>
                    new LeituraBruta
                    {
                        DataHora =
                            ponto.DataHora,

                        Valor =
                            (float)ponto.Valor
                    })
                .ToArray();

        return new BehaviorHistoryCollectionResult
        {
            ClienteId =
                clienteId,

            TagName =
                tagName,

            PeriodoInicio =
                inicio,

            PeriodoFim =
                fim,

            Historico =
                historicoSanitizado,

            Qualidade =
                sanitizado.Quality
        };
    }
}
