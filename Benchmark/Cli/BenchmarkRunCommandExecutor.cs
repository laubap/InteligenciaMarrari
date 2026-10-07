using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkRunCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection)
    {
        if (commandSelection.ExecutarBenchmark)
        {
            Console.WriteLine();
            Console.WriteLine(
                "API iniciada em modo benchmark.");

            using var scope = services.CreateScope();

            var runner =
                scope.ServiceProvider
                    .GetRequiredService<BenchmarkRunner>();

            await runner.ExecutarAsync();

            Console.WriteLine();
            Console.WriteLine(
                "Execução do benchmark concluída.");

            return true;
        }

        if (commandSelection.ExecutarBenchmarkClean)
        {
            Console.WriteLine();
            Console.WriteLine(
                "API iniciada em modo benchmark do banco limpo.");

            using var scope = services.CreateScope();

            var runner =
                scope.ServiceProvider
                    .GetRequiredService<BenchmarkRunner>();

            await runner.ExecutarAsync(
                bancoLimpo: true);

            Console.WriteLine();
            Console.WriteLine(
                "Execução do benchmark limpo concluída.");

            return true;
        }

        return false;
    }
}