using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkEvaluationCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        string[] args,
        BenchmarkCommandSelection commandSelection)
    {
        if (!commandSelection.ExecutarAvaliacao)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo de avaliação do benchmark.");

        var argumentosAvaliacao =
            args
                .Skip(commandSelection.IndiceEvaluate + 1)
                .ToArray();

        var diretorioProjeto =
            Directory.GetCurrentDirectory();

        var diretorioResultadosPadrao =
            Path.Combine(
                diretorioProjeto,
                "Results");

        var oraclePadrao =
            Path.Combine(
                AppContext.BaseDirectory,
                "Benchmark",
                "oracle.json");

        string detectionsPath;
        string errorsPath;
        string oraclePath;
        string outputDirectory;

        if (argumentosAvaliacao.Length == 0)
        {
            if (!Directory.Exists(diretorioResultadosPadrao))
            {
                throw new DirectoryNotFoundException(
                    $"Diretório de resultados não encontrado: " +
                    $"{diretorioResultadosPadrao}");
            }

            var arquivoDeteccoesMaisRecente =
                new DirectoryInfo(
                    diretorioResultadosPadrao)
                    .GetFiles("detections_*.jsonl")
                    .OrderByDescending(
                        arquivo => arquivo.LastWriteTimeUtc)
                    .FirstOrDefault();

            if (arquivoDeteccoesMaisRecente is null)
            {
                throw new FileNotFoundException(
                    $"Nenhum arquivo detections_*.jsonl foi encontrado em " +
                    $"{diretorioResultadosPadrao}.");
            }

            detectionsPath =
                arquivoDeteccoesMaisRecente.FullName;

            var sufixoExecucao =
                Path.GetFileNameWithoutExtension(
                    arquivoDeteccoesMaisRecente.Name)
                    .Replace(
                        "detections_",
                        string.Empty,
                        StringComparison.OrdinalIgnoreCase);

            errorsPath =
                Path.Combine(
                    diretorioResultadosPadrao,
                    $"errors_{sufixoExecucao}.jsonl");

            oraclePath =
                oraclePadrao;

            outputDirectory =
                diretorioResultadosPadrao;
        }
        else if (argumentosAvaliacao.Length == 4)
        {
            detectionsPath =
                argumentosAvaliacao[0];

            errorsPath =
                argumentosAvaliacao[1];

            oraclePath =
                argumentosAvaliacao[2];

            outputDirectory =
                argumentosAvaliacao[3];
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(
                "Uso automático:");

            Console.WriteLine(
                "  dotnet run -c Release -- --evaluate");

            Console.WriteLine();

            Console.WriteLine(
                "Uso com caminhos informados manualmente:");

            Console.WriteLine(
                "  dotnet run -c Release -- --evaluate " +
                "<detections.jsonl> <errors.jsonl> " +
                "<oracle.json> <diretório-saída>");

            return true;
        }

        if (!File.Exists(detectionsPath))
        {
            throw new FileNotFoundException(
                "Arquivo de detecções não encontrado.",
                detectionsPath);
        }

        if (!File.Exists(errorsPath))
        {
            throw new FileNotFoundException(
                "Arquivo de erros não encontrado.",
                errorsPath);
        }

        if (!File.Exists(oraclePath))
        {
            throw new FileNotFoundException(
                "Arquivo oracle.json não encontrado.",
                oraclePath);
        }

        Directory.CreateDirectory(
            outputDirectory);

        Console.WriteLine();
        Console.WriteLine(
            $"Detecções: {detectionsPath}");

        Console.WriteLine(
            $"Erros:     {errorsPath}");

        Console.WriteLine(
            $"Oráculo:   {oraclePath}");

        Console.WriteLine(
            $"Saída:     {outputDirectory}");

        Console.WriteLine();

        using var scope =
            services.CreateScope();

        var evaluator =
            scope.ServiceProvider
                .GetRequiredService<BenchmarkEvaluator>();

        await evaluator.EvaluateAsync(
            detectionsPath,
            errorsPath,
            oraclePath,
            outputDirectory);

        Console.WriteLine();
        Console.WriteLine(
            "Avaliação concluída. " +
            "Confira os relatórios gerados na pasta de saída.");

        return true;
    }
}