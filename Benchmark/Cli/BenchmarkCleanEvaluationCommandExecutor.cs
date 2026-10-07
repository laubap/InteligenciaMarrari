using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkCleanEvaluationCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        string[] args,
        BenchmarkCommandSelection commandSelection)
    {
        if (!commandSelection.ExecutarAvaliacaoClean)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo de avaliação do banco limpo.");

        var argumentosAvaliacaoClean =
            args
                .Skip(commandSelection.IndiceEvaluateClean + 1)
                .ToArray();

        var diretorioProjeto =
            Directory.GetCurrentDirectory();

        var diretorioResultadosPadrao =
            Path.Combine(
                diretorioProjeto,
                "Results");

        string detectionsPath;
        string errorsPath;
        string summaryPath;
        string outputDirectory;

        if (argumentosAvaliacaoClean.Length == 0)
        {
            if (!Directory.Exists(
                    diretorioResultadosPadrao))
            {
                throw new DirectoryNotFoundException(
                    $"Diretório de resultados não encontrado: " +
                    $"{diretorioResultadosPadrao}");
            }

            var arquivoDeteccoesMaisRecente =
                new DirectoryInfo(
                    diretorioResultadosPadrao)
                    .GetFiles(
                        "clean_detections_*.jsonl")
                    .OrderByDescending(
                        arquivo =>
                            arquivo.LastWriteTimeUtc)
                    .FirstOrDefault();

            if (arquivoDeteccoesMaisRecente is null)
            {
                throw new FileNotFoundException(
                    $"Nenhum arquivo clean_detections_*.jsonl " +
                    $"foi encontrado em {diretorioResultadosPadrao}.");
            }

            detectionsPath =
                arquivoDeteccoesMaisRecente.FullName;

            var sufixoExecucao =
                Path.GetFileNameWithoutExtension(
                    arquivoDeteccoesMaisRecente.Name)
                    .Replace(
                        "clean_detections_",
                        string.Empty,
                        StringComparison.OrdinalIgnoreCase);

            errorsPath =
                Path.Combine(
                    diretorioResultadosPadrao,
                    $"clean_errors_{sufixoExecucao}.jsonl");

            summaryPath =
                Path.Combine(
                    diretorioResultadosPadrao,
                    $"clean_summary_{sufixoExecucao}.json");

            outputDirectory =
                diretorioResultadosPadrao;
        }
        else if (argumentosAvaliacaoClean.Length == 4)
        {
            detectionsPath =
                argumentosAvaliacaoClean[0];

            errorsPath =
                argumentosAvaliacaoClean[1];

            summaryPath =
                argumentosAvaliacaoClean[2];

            outputDirectory =
                argumentosAvaliacaoClean[3];
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(
                "Uso automático:");

            Console.WriteLine(
                "  dotnet run -c Release --no-build -- --evaluate-clean");

            Console.WriteLine();

            Console.WriteLine(
                "Uso com caminhos informados manualmente:");

            Console.WriteLine(
                "  dotnet run -c Release --no-build -- --evaluate-clean " +
                "<clean_detections.jsonl> <clean_errors.jsonl> " +
                "<clean_summary.json> <diretório-saída>");

            return true;
        }

        if (!File.Exists(detectionsPath))
        {
            throw new FileNotFoundException(
                "Arquivo de detecções do banco limpo não encontrado.",
                detectionsPath);
        }

        if (!File.Exists(errorsPath))
        {
            throw new FileNotFoundException(
                "Arquivo de erros do banco limpo não encontrado.",
                errorsPath);
        }

        if (!File.Exists(summaryPath))
        {
            throw new FileNotFoundException(
                "Arquivo de resumo do banco limpo não encontrado.",
                summaryPath);
        }

        Directory.CreateDirectory(
            outputDirectory);

        Console.WriteLine();
        Console.WriteLine(
            $"Detecções: {detectionsPath}");

        Console.WriteLine(
            $"Erros:     {errorsPath}");

        Console.WriteLine(
            $"Resumo:    {summaryPath}");

        Console.WriteLine(
            $"Saída:     {outputDirectory}");

        Console.WriteLine();

        using var scope =
            services.CreateScope();

        var evaluator =
            scope.ServiceProvider
                .GetRequiredService<CleanBenchmarkEvaluator>();

        await evaluator.EvaluateAsync(
            detectionsPath,
            errorsPath,
            summaryPath,
            outputDirectory);

        Console.WriteLine();

        Console.WriteLine(
            "Avaliação do banco limpo concluída. " +
            "Confira os relatórios gerados na pasta de saída.");

        return true;
    }
}