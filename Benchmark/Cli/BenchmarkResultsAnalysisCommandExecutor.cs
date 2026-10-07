using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkResultsAnalysisCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (!commandSelection.ExecutarAnaliseResultados)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo de análise da pasta Results.");

        var diretorioResultados =
            Path.IsPathRooted(
                benchmarkOptions.ResultDirectory)
                ? benchmarkOptions.ResultDirectory
                : Path.Combine(
                    Directory.GetCurrentDirectory(),
                    benchmarkOptions.ResultDirectory);

        using var scope =
            services.CreateScope();

        var analyzer =
            scope.ServiceProvider
                .GetRequiredService<ResultsAnalyzer>();

        await analyzer.AnalyzeAsync(
            diretorioResultados);

        return true;
    }
}