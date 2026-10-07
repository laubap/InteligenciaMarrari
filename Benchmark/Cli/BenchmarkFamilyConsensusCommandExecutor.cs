using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkFamilyConsensusCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (!commandSelection.ExecutarFamilyConsensusReport)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo Family Consensus Report.");

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
                .GetRequiredService<FamilyConsensusReport>();

        await report.RunAsync(
            diretorioResultados);

        return true;
    }
}