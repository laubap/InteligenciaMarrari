using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class RateOfChangeDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public RateOfChangeDetector(
        IOptions<AnomalyDetectionOptions> options)
    {
        _options = options.Value;
    }

    public string Nome => "Taxa de variação";

    public double Peso => _options.RateOfChangeWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        var historico = input.Historico
            .OrderBy(x => x.DataHora)
            .ToArray();

        if (historico.Length < 3)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao = "Histórico insuficiente para calcular variação."
            };
        }

        var taxasHistoricas = new List<double>();

        for (var i = 1; i < historico.Length; i++)
        {
            var minutos = (
                historico[i].DataHora -
                historico[i - 1].DataHora
            ).TotalMinutes;

            if (minutos <= 0)
                continue;

            var taxa =
                (historico[i].Valor -
                 historico[i - 1].Valor) /
                minutos;

            if (double.IsFinite(taxa))
                taxasHistoricas.Add(taxa);
        }

        if (taxasHistoricas.Count < 2)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "Não existem intervalos temporais válidos suficientes."
            };
        }

        var ultimo = historico.Last();

        var intervaloAtual = (
            input.DataHoraAtual -
            ultimo.DataHora
        ).TotalMinutes;

        if (intervaloAtual <= 0)
        {
            intervaloAtual =
                DetectorMath.IntervaloMedianoMinutos(
                    input.Historico
                );
        }

        if (intervaloAtual <= 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "Não foi possível determinar o intervalo da leitura atual."
            };
        }

        var taxaAtual =
            (input.ValorAtual - ultimo.Valor) /
            intervaloAtual;

        var medianaTaxas =
            DetectorMath.Median(taxasHistoricas);

        var madTaxas =
            DetectorMath.Mad(
                taxasHistoricas,
                medianaTaxas
            );

        var escala = madTaxas > 0
            ? madTaxas
            : DetectorMath.DesvioPadrao(taxasHistoricas);

        if (escala <= 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "As taxas históricas não apresentam variação suficiente."
            };
        }

        var scoreTaxa =
            0.6745 *
            Math.Abs(taxaAtual - medianaTaxas) /
            escala;

        var ehAnomalia =
            scoreTaxa >=
            _options.RateOfChangeThreshold;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = scoreTaxa,

            ScoreNormalizado =
                DetectorMath.NormalizarPorLimite(
                    scoreTaxa,
                    _options.RateOfChangeThreshold
                ),

            Peso = Peso,
            Detector = Nome,

            Explicacao = ehAnomalia
                ? $"A taxa atual de {taxaAtual:F4} unidades/minuto difere significativamente da taxa histórica."
                : $"A taxa atual de {taxaAtual:F4} unidades/minuto está dentro do padrão histórico.",

            Metricas = new Dictionary<string, double>
            {
                ["taxaAtualPorMinuto"] = taxaAtual,
                ["medianaTaxas"] = medianaTaxas,
                ["madTaxas"] = madTaxas,
                ["scoreTaxa"] = scoreTaxa
            }
        };
    }
}