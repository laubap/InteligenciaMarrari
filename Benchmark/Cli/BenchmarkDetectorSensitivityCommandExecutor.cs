using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkDetectorSensitivityCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (!commandSelection.ExecutarAnaliseSensibilidadeDetectores)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo de análise " +
            "de sensibilidade dos detectores.");

        var diretorioResultados =
            Path.IsPathRooted(
                benchmarkOptions.ResultDirectory)
                ? benchmarkOptions.ResultDirectory
                : Path.Combine(
                    Directory.GetCurrentDirectory(),
                    benchmarkOptions.ResultDirectory);

        using var scope =
            services.CreateScope();

        var report =
            scope.ServiceProvider
                .GetRequiredService<DetectorSensitivityReport>();

        await report.AnalyzeAsync(
            diretorioResultados);

        return true;
    }
}