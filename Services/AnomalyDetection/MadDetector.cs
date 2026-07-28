using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class MadDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public MadDetector(
        IOptions<AnomalyDetectionOptions> options)
    {
        _options = options.Value;
    }

    public string Nome => "Mediana e MAD";

    public double Peso => _options.MadWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        var valores = input.Historico
            .Select(x => x.Valor)
            .Where(double.IsFinite)
            .ToArray();

        if (valores.Length < 10)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao = "Histórico insuficiente para calcular MAD."
            };
        }

        var mediana = DetectorMath.Median(valores);
        var mad = DetectorMath.Mad(valores, mediana);

        if (mad <= 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "MAD não aplicável porque o histórico não apresenta dispersão."
            };
        }

        var zModificado =
            0.6745 *
            Math.Abs(input.ValorAtual - mediana) /
            mad;

        var ehAnomalia =
            zModificado >= _options.MadThreshold;

        var escalaRobusta = mad * 1.4826;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = zModificado,

            ScoreNormalizado =
                DetectorMath.NormalizarPorLimite(
                    zModificado,
                    _options.MadThreshold
                ),

            Peso = Peso,

            LimiteInferior =
                mediana -
                (_options.MadThreshold * escalaRobusta),

            LimiteSuperior =
                mediana +
                (_options.MadThreshold * escalaRobusta),

            Media = mediana,
            DesvioPadrao = escalaRobusta,
            Detector = Nome,

            Explicacao = ehAnomalia
                ? $"O valor possui Z-score modificado de {zModificado:F2}, indicando desvio robusto em relação à mediana."
                : $"O Z-score modificado é {zModificado:F2}, dentro do comportamento robusto esperado.",

            Metricas = new Dictionary<string, double>
            {
                ["mediana"] = mediana,
                ["mad"] = mad,
                ["zModificado"] = zModificado
            }
        };
    }
}