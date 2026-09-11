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
        ArgumentNullException.ThrowIfNull(input);

        var contexto = input.Contexto;
        var historico = contexto.Historico;

        if (historico.Count < 3)
            return NaoAplicavel(
                "Histórico insuficiente para calcular variação.");

        var taxasHistoricas = contexto.TaxasHistoricas;

        if (taxasHistoricas.Length < 2)
            return NaoAplicavel(
                "Não existem intervalos temporais válidos suficientes.");

        var ultimo = contexto.UltimoPonto;

        if (ultimo is null)
            return NaoAplicavel(
                "O histórico não possui um último ponto válido.");

        var intervaloAtual =
            (input.DataHoraAtual - ultimo.DataHora).TotalMinutes;

        if (intervaloAtual <= 0)
            intervaloAtual = contexto.IntervaloMedianoMinutos;

        if (intervaloAtual <= 0 ||
            !double.IsFinite(intervaloAtual))
        {
            return NaoAplicavel(
                "Não foi possível determinar o intervalo da leitura atual.");
        }

        var taxaAtual =
            (input.ValorAtual - ultimo.Valor) / intervaloAtual;

        if (!double.IsFinite(taxaAtual))
            return NaoAplicavel(
                "A taxa atual calculada não é válida.");

        var medianaTaxas = contexto.MedianaTaxas;
        var madTaxas = contexto.MadTaxas;
        var desvioPadraoTaxas = contexto.DesvioPadraoTaxas;

        double scoreTaxa;
        string metodoScore;
        double escalaUtilizada;

        if (madTaxas > 0 && double.IsFinite(madTaxas))
        {
            // Modified Z-score robusto: o fator 0.6745 pertence
            // exclusivamente à normalização por MAD.
            escalaUtilizada = madTaxas;
            scoreTaxa =
                0.6745 *
                Math.Abs(taxaAtual - medianaTaxas) /
                madTaxas;

            metodoScore = "MAD";
        }
        else if (desvioPadraoTaxas > 0 &&
                 double.IsFinite(desvioPadraoTaxas))
        {
            // Fallback clássico: ao usar desvio padrão não se aplica
            // o fator 0.6745 do modified Z-score.
            escalaUtilizada = desvioPadraoTaxas;
            scoreTaxa =
                Math.Abs(taxaAtual - medianaTaxas) /
                desvioPadraoTaxas;

            metodoScore = "DesvioPadrao";
        }
        else
        {
            return NaoAplicavel(
                "As taxas históricas não apresentam variação suficiente.");
        }

        if (!double.IsFinite(scoreTaxa))
            return NaoAplicavel(
                "O score da taxa de variação não é numericamente válido.");

        var ehAnomalia =
            scoreTaxa >= _options.RateOfChangeThreshold;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = scoreTaxa,
            ScoreNormalizado =
                DetectorMath.NormalizarPorLimite(
                    scoreTaxa,
                    _options.RateOfChangeThreshold),
            Peso = Peso,
            Detector = Nome,
            Explicacao = ehAnomalia
                ? $"A taxa atual de {taxaAtual:F4} unidades/minuto difere significativamente da taxa histórica ({metodoScore})."
                : $"A taxa atual de {taxaAtual:F4} unidades/minuto está dentro do padrão histórico ({metodoScore}).",
            Metricas = new Dictionary<string, double>
            {
                ["taxaAtualPorMinuto"] = taxaAtual,
                ["medianaTaxas"] = medianaTaxas,
                ["madTaxas"] = madTaxas,
                ["desvioPadraoTaxas"] = desvioPadraoTaxas,
                ["escalaUtilizada"] = escalaUtilizada,
                ["fallbackDesvioPadrao"] =
                    metodoScore == "DesvioPadrao" ? 1 : 0,
                ["scoreTaxa"] = scoreTaxa
            }
        };
    }

    private DetectorResult NaoAplicavel(string explicacao) => new()
    {
        Aplicavel = false,
        EhAnomalia = false,
        Score = 0,
        ScoreNormalizado = 0,
        Detector = Nome,
        Peso = Peso,
        Explicacao = explicacao
    };
}
