using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class EnsembleDetectionService
{
    private readonly List<IAnomalyDetector> _detectores;
    private readonly AnomalyDetectionOptions _options;

    public EnsembleDetectionService(
        IEnumerable<IAnomalyDetector> detectores,
        IOptions<AnomalyDetectionOptions> options)
    {
        _detectores = detectores.ToList();
        _options = options.Value;
    }

    public DetectorResult Detectar(DetectorInput input)
    {
        if (_detectores.Count == 0)
        {
            throw new InvalidOperationException(
                "Nenhum detector de anomalias foi registrado."
            );
        }

        /*
         * Mantemos também os não aplicáveis no debug,
         * mas somente os aplicáveis participam do score final.
         */
        var todosResultados = _detectores
            .Select(detector =>
            {
                try
                {
                    return detector.Detectar(input);
                }
                catch (Exception ex)
                {
                    return new DetectorResult
                    {
                        Aplicavel = false,
                        EhAnomalia = false,
                        Score = 0,
                        ScoreNormalizado = 0,
                        Peso = detector.Peso,
                        Detector = detector.Nome,
                        Explicacao =
                            $"Erro ao executar o detector: {ex.Message}"
                    };
                }
            })
            .ToList();

        var resultadosAplicaveis = todosResultados
            .Where(resultado => resultado.Aplicavel)
            .ToList();

        if (resultadosAplicaveis.Count == 0)
        {
            throw new InvalidOperationException(
                "Nenhum detector pôde analisar os dados informados."
            );
        }

        var somaPesos = resultadosAplicaveis
            .Sum(resultado => resultado.Peso);

        if (somaPesos <= 0)
        {
            throw new InvalidOperationException(
                "A soma dos pesos dos detectores aplicáveis deve ser maior que zero."
            );
        }

        var scoreFinalNormalizado = resultadosAplicaveis
            .Sum(resultado =>
                resultado.ScoreNormalizado *
                resultado.Peso
            ) / somaPesos;

        var ehAnomaliaFinal =
            scoreFinalNormalizado >=
            _options.EnsembleThreshold;

        var resultadoZScore = resultadosAplicaveis
            .FirstOrDefault(resultado =>
                resultado.Detector == "Z-Score"
            );

        var resultadoReferencia =
            resultadoZScore ??
            resultadosAplicaveis.First();

        var detalhes = todosResultados
            .Select(resultado => new DetectorDebugItem
            {
                Nome = resultado.Detector,
                Aplicavel = resultado.Aplicavel,
                VotouAnomalia =
                    resultado.Aplicavel &&
                    resultado.EhAnomalia,

                ScoreNormalizado =
                    resultado.ScoreNormalizado,

                ScoreOriginal =
                    resultado.Score,

                Peso =
                    resultado.Peso,

                Explicacao =
                    resultado.Explicacao,

                Metricas =
                    resultado.Metricas
            })
            .ToList();

        ExibirDebugNoConsole(
            input,
            detalhes,
            scoreFinalNormalizado,
            ehAnomaliaFinal
        );

        return new DetectorResult
        {
            Aplicavel = true,

            EhAnomalia =
                ehAnomaliaFinal,

            /*
             * Converte o score 0–100 para aproximadamente 0–5,
             * preservando compatibilidade com o RiskScoreService.
             */
            Score =
                scoreFinalNormalizado / 20.0,

            ScoreNormalizado =
                scoreFinalNormalizado,

            Peso = 1,

            LimiteInferior =
                resultadoReferencia.LimiteInferior,

            LimiteSuperior =
                resultadoReferencia.LimiteSuperior,

            Media =
                input.MediaHistorica,

            DesvioPadrao =
                input.DesvioPadraoHistorico,

            Detector =
                "Ensemble ponderado",

            Explicacao = string.Join(
                Environment.NewLine,
                todosResultados.Select(resultado =>
                    $"{resultado.Detector}: " +
                    $"{resultado.Explicacao}"
                )
            ),

            Metricas = new Dictionary<string, double>
            {
                ["scoreFinalNormalizado"] =
                    scoreFinalNormalizado,

                ["limiteEnsemble"] =
                    _options.EnsembleThreshold,

                ["quantidadeDetectoresRegistrados"] =
                    todosResultados.Count,

                ["quantidadeDetectoresAplicaveis"] =
                    resultadosAplicaveis.Count,

                ["quantidadeAlertas"] =
                    resultadosAplicaveis.Count(resultado =>
                        resultado.EhAnomalia
                    ),

                ["somaPesosAplicaveis"] =
                    somaPesos
            },

            DetalhesDetectores =
                detalhes
        };
    }

    private static void ExibirDebugNoConsole(
        DetectorInput input,
        IReadOnlyCollection<DetectorDebugItem> detalhes,
        double scoreFinal,
        bool ehAnomalia)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("           DEBUG DOS DETECTORES");
        Console.WriteLine("========================================");
        Console.WriteLine($"Cliente: {input.ClienteId}");
        Console.WriteLine($"Tag: {input.TagName}");
        Console.WriteLine($"Valor atual: {input.ValorAtual:F4}");
        Console.WriteLine($"Pontos históricos: {input.Historico.Count}");
        Console.WriteLine("----------------------------------------");

        foreach (var detector in detalhes)
        {
            Console.WriteLine(detector.Nome);

            Console.WriteLine(
                $"Aplicável..........{FormatarSimNao(detector.Aplicavel)}"
            );

            Console.WriteLine(
                $"Score..............{detector.ScoreNormalizado:F2}/100"
            );

            Console.WriteLine(
                $"Score original.....{detector.ScoreOriginal:F4}"
            );

            Console.WriteLine(
                $"Peso...............{detector.Peso:P0}"
            );

            Console.WriteLine(
                $"Votou anomalia.....{FormatarSimNao(detector.VotouAnomalia)}"
            );

            Console.WriteLine(
                $"Explicação.........{detector.Explicacao}"
            );

            if (detector.Metricas.Count > 0)
            {
                Console.WriteLine("Métricas:");

                foreach (var metrica in detector.Metricas)
                {
                    Console.WriteLine(
                        $"  - {metrica.Key}: {metrica.Value:F4}"
                    );
                }
            }

            Console.WriteLine("----------------------------------------");
        }

        Console.WriteLine($"SCORE FINAL.........{scoreFinal:F2}/100");

        Console.WriteLine(
            $"ANOMALIA............{FormatarSimNao(ehAnomalia)}"
        );

        Console.WriteLine("========================================");
        Console.WriteLine();
    }

    private static string FormatarSimNao(bool valor)
    {
        return valor ? "SIM" : "NÃO";
    }
}