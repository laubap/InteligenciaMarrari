using IAApi.Services.Diagnostics;
using IAApi.Services.AnomalyDetection.Orchestration;
// Serviço que analisa a tendência de valor de uma tag com base no histórico
// de leituras fornecido pelo histórico de tags. Retorna se o valor está
// estável, em alta, em queda ou se não há histórico suficiente.
public class TendenciaValorService
{
    private readonly PerformanceDiagnostics? _performanceDiagnostics;
    private readonly PredictionHistoryReuseOptions? _historyReuse;

    private readonly IHistoricoService _historicoService;

    public TendenciaValorService(IHistoricoService historicoService,
        PerformanceDiagnostics? performanceDiagnostics = null,
        PredictionHistoryReuseOptions? historyReuse = null)
    {
        _performanceDiagnostics = performanceDiagnostics;
        _historyReuse = historyReuse;

        _historicoService = historicoService;
    }

    // Analisa a tendência de valor para a tag e o tipo de tag informados.
    public ResultadoTendenciaValor Analisar(
        string clienteId,
        string tagName,
        string tipoTag,
        PredictionRawHistorySnapshot? rawHistorySnapshot = null)
    {
        if (_historyReuse?.Enabled == true
            && rawHistorySnapshot?.Matches(_historicoService, clienteId, tagName, tipoTag) == true)
        {
            var reusedHistory = PerformanceDiagnostics.Measure(
                _performanceDiagnostics, clienteId, tagName, "ValueTrend.ReuseRawHistory", () =>
                {
                    var history = rawHistorySnapshot.CopyReadings();
                    _performanceDiagnostics?.HistoryAcquisition("ReusedPredictionRaw",
                        rawHistorySnapshot.AcquisitionStartedUtc, rawHistorySnapshot.AcquisitionCompletedUtc,
                        history, rawHistorySnapshot.PredictionReference);
                    return history;
                });
            return Calcular(clienteId, tagName, reusedHistory);
        }

        var startedUtc = _performanceDiagnostics?.Enabled == true ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        var historico = PerformanceDiagnostics.Measure(
            _performanceDiagnostics, clienteId, tagName, "ValueTrend.History",
            () =>
            {
                var history = _historicoService.BuscarHistorico(clienteId, tagName, tipoTag);
                _performanceDiagnostics?.HistoryAcquisition("Fetched", startedUtc,
                    startedUtc is not null ? DateTimeOffset.UtcNow : null, history, null);
                return history;
            });

        return Calcular(clienteId, tagName, historico);
    }

    private ResultadoTendenciaValor Calcular(string clienteId, string tagName, List<LeituraBruta> historico)
    {
        return PerformanceDiagnostics.Measure(
            _performanceDiagnostics, clienteId, tagName, "ValueTrend.Calculate",
            () =>
            {
                var dados = historico
                    .OrderBy(x => x.DataHora)
                    .ToList();

                if (dados.Count < 5)
                {
                    return new ResultadoTendenciaValor
                    {
                        Tendencia = "insuficiente",
                        Mensagem = "Histórico insuficiente no Bridge para analisar tendência do valor."
                    };
                }

                var metade = dados.Count / 2;

                var primeiraMetade = dados
                    .Take(metade)
                    .Average(x => x.Valor);

                var segundaMetade = dados
                    .Skip(metade)
                    .Average(x => x.Valor);

                var diferenca = segundaMetade - primeiraMetade;

                var percentual = primeiraMetade != 0
                    ? Math.Abs(diferenca / primeiraMetade) * 100
                    : Math.Abs(diferenca);

                if (percentual < 10)
                {
                    return new ResultadoTendenciaValor
                    {
                        Tendencia = "estavel",
                        Mensagem = "O valor da tag permaneceu relativamente estável no histórico recente."
                    };
                }

                if (diferenca > 0)
                {
                    return new ResultadoTendenciaValor
                    {
                        Tendencia = "alta",
                        Mensagem = $"O valor da tag apresenta tendência de alta. Média inicial {primeiraMetade:F2}, média recente {segundaMetade:F2}."
                    };
                }

                return new ResultadoTendenciaValor
                {
                    Tendencia = "queda",
                    Mensagem = $"O valor da tag apresenta tendência de queda. Média inicial {primeiraMetade:F2}, média recente {segundaMetade:F2}."
                };
            });
    }
}

public class ResultadoTendenciaValor
{
    public string Tendencia { get; set; } = "";
    public string Mensagem { get; set; } = "";
}
