using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkAdaptiveEnsembleCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (!commandSelection.ExecutarAdaptiveEnsembleLab)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo Adaptive Ensemble Laboratory.");

        var diretorioResultados =
            Path.IsPathRooted(
                benchmarkOptions.ResultDirectory)
                ? benchmarkOptions.ResultDirectory
                : Path.Combine(
                    Directory.GetCurrentDirectory(),
                    benchmarkOptions.ResultDirectory);

        using var scope =
            services.CreateScope();

        var laboratory =
            scope.ServiceProvider
                .GetRequiredService<AdaptiveEnsembleLaboratory>();

        await laboratory.RunAsync(
            diretorioResultados);

        return true;
    }
}