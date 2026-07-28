using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class StatisticalDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public StatisticalDetector(
        IOptions<AnomalyDetectionOptions> options)
    {
        _options = options.Value;
    }

    public string Nome => "Z-Score";

    public double Peso => _options.ZScoreWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        var media = input.MediaHistorica;
        var desvio = input.DesvioPadraoHistorico;
        var valorAtual = input.ValorAtual;

        if (desvio <= 0 || !double.IsFinite(desvio))
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "Z-score não aplicável porque o desvio padrão é zero."
            };
        }

        var limiteInferior =
            media - (_options.ZScoreThreshold * desvio);

        var limiteSuperior =
            media + (_options.ZScoreThreshold * desvio);

        var zScore =
            Math.Abs(valorAtual - media) / desvio;

        var ehAnomalia =
            zScore >= _options.ZScoreThreshold;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = zScore,

            ScoreNormalizado =
                DetectorMath.NormalizarPorLimite(
                    zScore,
                    _options.ZScoreThreshold
                ),

            Peso = Peso,
            LimiteInferior = limiteInferior,
            LimiteSuperior = limiteSuperior,
            Media = media,
            DesvioPadrao = desvio,
            Detector = Nome,

            Explicacao = ehAnomalia
                ? $"O valor está a {zScore:F2} desvios da média histórica."
                : $"O Z-score atual é {zScore:F2}, dentro do limite configurado.",

            Metricas = new Dictionary<string, double>
            {
                ["zScore"] = zScore,
                ["media"] = media,
                ["desvioPadrao"] = desvio
            }
        };
    }
}