using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class PersistenceDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public PersistenceDetector(
        IOptions<AnomalyDetectionOptions> options)
    {
        _options = options.Value;
    }

    public string Nome => "Persistência";

    public double Peso => _options.PersistenceWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        var historico = input.Historico
            .OrderBy(x => x.DataHora)
            .ToList();

        if (historico.Count < 10)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "Histórico insuficiente para avaliar persistência."
            };
        }

        var valores = historico
            .Select(x => x.Valor)
            .ToArray();

        var mediana = DetectorMath.Median(valores);
        var escala = DetectorMath.EscalaRobusta(valores);

        if (escala <= 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "Não existe dispersão suficiente para avaliar persistência."
            };
        }

        var limite =
            _options.MadThreshold;

        bool EstaFora(double valor)
        {
            var score =
                Math.Abs(valor - mediana) /
                escala;

            return score >= limite;
        }

        var pontosComAtual = historico
            .Select(x => new HistoricoDetectorPoint
            {
                DataHora = x.DataHora,
                Valor = x.Valor
            })
            .ToList();

        pontosComAtual.Add(new HistoricoDetectorPoint
        {
            DataHora = input.DataHoraAtual,
            Valor = input.ValorAtual
        });

        var consecutivos = 0;
        DateTime? inicioPersistencia = null;

        for (var i = pontosComAtual.Count - 1; i >= 0; i--)
        {
            if (!EstaFora(pontosComAtual[i].Valor))
                break;

            consecutivos++;
            inicioPersistencia = pontosComAtual[i].DataHora;
        }

        var duracaoMinutos =
            inicioPersistencia.HasValue
                ? Math.Max(
                    0,
                    (
                        input.DataHoraAtual -
                        inicioPersistencia.Value
                    ).TotalMinutes
                )
                : 0;

        var intervaloMediano =
            DetectorMath.IntervaloMedianoMinutos(
                input.Historico
            );

        var duracaoMinima = Math.Max(
            _options.PersistenceMinimumMinutes,
            intervaloMediano *
            Math.Max(
                1,
                _options.PersistenceMinimumPoints - 1
            )
        );

        var componentePontos = Math.Clamp(
            consecutivos /
            (double)_options.PersistenceMinimumPoints,
            0,
            1
        );

        var componenteDuracao =
            duracaoMinima > 0
                ? Math.Clamp(
                    duracaoMinutos / duracaoMinima,
                    0,
                    1
                )
                : 0;

        var scoreNormalizado =
            (
                componentePontos * 0.50 +
                componenteDuracao * 0.50
            ) * 100;

        var ehAnomalia =
            consecutivos >=
                _options.PersistenceMinimumPoints &&
            duracaoMinutos >= duracaoMinima;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,

            Score =
                scoreNormalizado / 20.0,

            ScoreNormalizado =
                scoreNormalizado,

            Peso = Peso,
            Detector = Nome,

            Explicacao = ehAnomalia
                ? $"O desvio permaneceu por {consecutivos} leituras e aproximadamente {duracaoMinutos:F1} minutos."
                : $"Foram identificadas {consecutivos} leituras consecutivas fora do padrão.",

            Metricas = new Dictionary<string, double>
            {
                ["pontosConsecutivos"] = consecutivos,
                ["duracaoMinutos"] = duracaoMinutos,
                ["duracaoMinimaMinutos"] = duracaoMinima
            }
        };
    }
}