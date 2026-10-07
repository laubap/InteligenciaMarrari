using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkConfidenceCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (!commandSelection.ExecutarConfidenceBenchmark)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo Confidence Engine Benchmark.");

        var diretorioResultados =
            Path.IsPathRooted(
                benchmarkOptions.ResultDirectory)
                ? benchmarkOptions.ResultDirectory
                : Path.Combine(
                    Directory.GetCurrentDirectory(),
                    benchmarkOptions.ResultDirectory);

        using var scope =
            services.CreateScope();

        var runner =
            scope.ServiceProvider
                .GetRequiredService<ConfidenceEngineBenchmarkRunner>();

        await runner.RunAsync(
            diretorioResultados);

        return true;
    }
}