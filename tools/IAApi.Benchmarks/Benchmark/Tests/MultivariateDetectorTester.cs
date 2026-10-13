using IAApi.Services.AnomalyDetection.Multivariate;

namespace IAApi.Benchmark.Tests;

public sealed class MultivariateDetectorTester
{
    private readonly MultivariateDetector _detector;
    private readonly MultivariateModelPersistenceService _persistence;

    public MultivariateDetectorTester(
        MultivariateDetector detector,
        MultivariateModelPersistenceService persistence)
    {
        _detector = detector;
        _persistence = persistence;
    }

    public async Task ExecutarAsync()
    {
        const string clienteId = "BENCHMARK_STRESS_V1";
        const string groupName = "BOILER";

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("TESTE DO DETECTOR MULTIVARIADO");
        Console.WriteLine("========================================");

        var model = await _persistence.CarregarAsync(
            clienteId,
            groupName);

        if (model is null)
        {
            Console.WriteLine(
                $"Modelo não encontrado para o grupo {groupName}.");

            return;
        }

        Console.WriteLine($"Cliente: {clienteId}");
        Console.WriteLine($"Grupo: {groupName}");
        Console.WriteLine($"Amostras do modelo: {model.SampleCount:N0}");
        Console.WriteLine($"Tags do modelo: {model.Tags.Count}");

        var normalReading = CriarLeituraPelaMedia(
            clienteId,
            groupName,
            100,
            model);

        var normalResult = await _detector.DetectarAsync(
            normalReading);

        ExibirResultado(
            "CASO NORMAL",
            normalResult);

        var anomalyReading = CriarLeituraAnomala(
            clienteId,
            groupName,
            101,
            model);

        var anomalyResult = await _detector.DetectarAsync(
            anomalyReading);

        ExibirResultado(
            "CASO ANÔMALO",
            anomalyResult);

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("TESTE DO DETECTOR CONCLUÍDO");
        Console.WriteLine("========================================");
    }

    private static BufferedGroupReading CriarLeituraPelaMedia(
        string clienteId,
        string groupName,
        long minuteIndex,
        MultivariateGroupModel model)
    {
        var reading = new BufferedGroupReading
        {
            ClienteId = clienteId,
            GroupName = groupName,
            MinuteIndex = minuteIndex,
            MinimumAvailableTags =
                model.MinimumAvailableTags,
            ExpectedTags =
                model.Tags.ToList()
        };

        for (var index = 0;
             index < model.Tags.Count;
             index++)
        {
            reading.AdicionarValor(
                model.Tags[index],
                model.Means[index]);
        }

        return reading;
    }

    private static BufferedGroupReading CriarLeituraAnomala(
        string clienteId,
        string groupName,
        long minuteIndex,
        MultivariateGroupModel model)
    {
        var reading = new BufferedGroupReading
        {
            ClienteId = clienteId,
            GroupName = groupName,
            MinuteIndex = minuteIndex,
            MinimumAvailableTags =
                model.MinimumAvailableTags,
            ExpectedTags =
                model.Tags.ToList()
        };

        for (var index = 0;
             index < model.Tags.Count;
             index++)
        {
            var standardDeviation =
                model.StandardDeviations[index];

            var offset =
                standardDeviation > 1e-9
                    ? standardDeviation * 8
                    : Math.Max(
                        Math.Abs(model.Means[index]) * 0.5,
                        10);

            reading.AdicionarValor(
                model.Tags[index],
                model.Means[index] + offset);
        }

        return reading;
    }

    private static void ExibirResultado(
        string titulo,
        MultivariateDetectionResult result)
    {
        Console.WriteLine();
        Console.WriteLine("----------------------------------------");
        Console.WriteLine(titulo);
        Console.WriteLine("----------------------------------------");

        Console.WriteLine($"Aplicável: {result.Aplicavel}");
        Console.WriteLine($"Anomalia: {result.EhAnomalia}");
        Console.WriteLine(
            $"Score: {result.ScoreNormalizado:F2}/100");
        Console.WriteLine(
            $"Limite: {result.LimiteScore:F2}");
        Console.WriteLine(
            $"Mahalanobis²: {result.MahalanobisSquared:F2}");
        Console.WriteLine(
            $"Tags utilizadas: {result.TagsUtilizadas.Count}");
        Console.WriteLine($"Explicação: {result.Explicacao}");

        if (result.Contribuicoes.Count == 0)
        {
            return;
        }

        Console.WriteLine("Maiores contribuições:");

        foreach (var contribution in
                 result.Contribuicoes.Take(5))
        {
            Console.WriteLine(
                $"  - {contribution.TagName}: " +
                $"valor={contribution.ValorAtual:F4}, " +
                $"média={contribution.MediaTreinamento:F4}, " +
                $"|z|={contribution.ZScoreAbsoluto:F2}");
        }
    }
}