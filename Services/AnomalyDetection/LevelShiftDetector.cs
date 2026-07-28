using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class LevelShiftDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public LevelShiftDetector(
        IOptions<AnomalyDetectionOptions> options)
    {
        _options = options.Value;
    }

    public string Nome => "Mudança de patamar";

    public double Peso => _options.LevelShiftWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        var serie = input.Historico
            .OrderBy(x => x.DataHora)
            .Select(x => x.Valor)
            .Where(double.IsFinite)
            .ToList();

        serie.Add(input.ValorAtual);

        var totalNecessario =
            _options.LevelShiftPreviousWindow +
            _options.LevelShiftRecentWindow;

        if (serie.Count < totalNecessario)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    $"São necessários pelo menos {totalNecessario} pontos."
            };
        }

        var recentes = serie
            .TakeLast(
                _options.LevelShiftRecentWindow
            )
            .ToArray();

        var anteriores = serie
            .Skip(
                serie.Count -
                totalNecessario
            )
            .Take(
                _options.LevelShiftPreviousWindow
            )
            .ToArray();

        var medianaAnterior =
            DetectorMath.Median(anteriores);

        var medianaRecente =
            DetectorMath.Median(recentes);

        var escalaAnterior =
            DetectorMath.EscalaRobusta(anteriores);

        if (escalaAnterior <= 0)
        {
            return new DetectorResult
            {
                Aplicavel = false,
                Detector = Nome,
                Peso = Peso,
                Explicacao =
                    "A janela anterior não apresenta dispersão suficiente."
            };
        }

        var diferenca =
            medianaRecente - medianaAnterior;

        var scoreMudanca =
            Math.Abs(diferenca) /
            escalaAnterior;

        var direcao = Math.Sign(diferenca);

        var consistentes = recentes.Count(valor =>
            direcao > 0
                ? valor > medianaAnterior
                : valor < medianaAnterior
        );

        var consistencia =
            consistentes /
            (double)recentes.Length;

        var componenteDeslocamento =
            Math.Clamp(
                scoreMudanca /
                _options.LevelShiftThreshold,
                0,
                1
            );

        var scoreNormalizado =
            (
                componenteDeslocamento * 0.70 +
                consistencia * 0.30
            ) * 100;

        var ehAnomalia =
            scoreMudanca >=
                _options.LevelShiftThreshold &&
            consistencia >=
                _options.LevelShiftMinimumConsistency;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,

            Score =
                scoreMudanca,

            ScoreNormalizado =
                scoreNormalizado,

            Peso = Peso,
            Detector = Nome,

            Explicacao = ehAnomalia
                ? $"Foi identificada mudança persistente de patamar: mediana anterior {medianaAnterior:F2}, recente {medianaRecente:F2}."
                : $"Não foi identificada mudança consistente de patamar.",

            Metricas = new Dictionary<string, double>
            {
                ["medianaAnterior"] = medianaAnterior,
                ["medianaRecente"] = medianaRecente,
                ["diferencaPatamar"] = diferenca,
                ["scoreMudanca"] = scoreMudanca,
                ["consistencia"] = consistencia
            }
        };
    }
}