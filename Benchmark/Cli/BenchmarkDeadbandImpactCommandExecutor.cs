using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkDeadbandImpactCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (!commandSelection.ExecutarAvaliacaoImpactoDeadband)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo de simulação do impacto do deadband.");

        var diretorioResultados =
            Path.IsPathRooted(
                benchmarkOptions.ResultDirectory)
                ? benchmarkOptions.ResultDirectory
                : Path.Combine(
                    Directory.GetCurrentDirectory(),
                    benchmarkOptions.ResultDirectory);

        using var scope =
            services.CreateScope();

        var evaluator =
            scope.ServiceProvider
                .GetRequiredService<DeadbandImpactEvaluator>();

        await evaluator.EvaluateAsync(
            benchmarkOptions,
            diretorioResultados);

        return true;
    }
}