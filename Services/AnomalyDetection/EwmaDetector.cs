using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class EwmaDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public EwmaDetector(
        IOptions<AnomalyDetectionOptions> options)
    {
        _options = options.Value;
    }

    public string Nome => "EWMA";

    public double Peso => _options.EwmaWeight;

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
                Explicacao = "Histórico insuficiente para calcular EWMA."
            };
        }

        var lambda = Math.Clamp(
            _options.EwmaLambda,
            0.01,
            1
        );

        var mediaAlvo = input.MediaHistorica;

        var desvio = input.DesvioPadraoHistorico > 0
            ? input.DesvioPadraoHistorico
            : DetectorMath.DesvioPadrao(valores);

        if (desvio <= 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "EWMA não aplicável porque o histórico não apresenta dispersão."
            };
        }

        var ewma = mediaAlvo;

        foreach (var valor in valores)
        {
            ewma =
                (lambda * valor) +
                ((1 - lambda) * ewma);
        }

        var ewmaAtual =
            (lambda * input.ValorAtual) +
            ((1 - lambda) * ewma);

        var desvioEwma =
            desvio *
            Math.Sqrt(lambda / (2 - lambda));

        if (desvioEwma <= 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao = "Não foi possível calcular o desvio do EWMA."
            };
        }

        var scoreEwma =
            Math.Abs(ewmaAtual - mediaAlvo) /
            desvioEwma;

        var ehAnomalia =
            scoreEwma >= _options.EwmaSigmaLimit;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = scoreEwma,

            ScoreNormalizado =
                DetectorMath.NormalizarPorLimite(
                    scoreEwma,
                    _options.EwmaSigmaLimit
                ),

            Peso = Peso,

            LimiteInferior =
                mediaAlvo -
                (_options.EwmaSigmaLimit * desvioEwma),

            LimiteSuperior =
                mediaAlvo +
                (_options.EwmaSigmaLimit * desvioEwma),

            Media = mediaAlvo,
            DesvioPadrao = desvioEwma,
            Detector = Nome,

            Explicacao = ehAnomalia
                ? $"O EWMA atingiu {ewmaAtual:F2}, indicando desvio gradual ou persistente."
                : $"O EWMA atual é {ewmaAtual:F2}, dentro dos limites esperados.",

            Metricas = new Dictionary<string, double>
            {
                ["ewmaAnterior"] = ewma,
                ["ewmaAtual"] = ewmaAtual,
                ["lambda"] = lambda,
                ["scoreEwma"] = scoreEwma
            }
        };
    }
}