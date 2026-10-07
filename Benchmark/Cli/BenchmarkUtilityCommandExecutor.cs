using IAApi.Benchmark.Multivariate;
using IAApi.Benchmark.Tests;
using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkUtilityCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection)
    {
        if (commandSelection.ExecutarTesteGrupos)
        {
            using var scope = services.CreateScope();

            var tester =
                scope.ServiceProvider
                    .GetRequiredService<GrupoAnaliseTester>();

            await tester.ExecutarAsync();

            return true;
        }

        if (commandSelection.ExecutarTesteBufferMultivariado)
        {
            using var scope = services.CreateScope();

            var tester =
                scope.ServiceProvider
                    .GetRequiredService<MultivariateBufferTester>();

            await tester.ExecutarAsync();

            return true;
        }

        if (commandSelection.ExecutarTesteDatasetMultivariado)
        {
            using var scope = services.CreateScope();

            var tester =
                scope.ServiceProvider
                    .GetRequiredService<MultivariateDatasetTester>();

            await tester.ExecutarAsync();

            return true;
        }

        if (commandSelection.ExecutarTesteDetectorMultivariado)
        {
            using var scope = services.CreateScope();

            var tester =
                scope.ServiceProvider
                    .GetRequiredService<MultivariateDetectorTester>();

            await tester.ExecutarAsync();

            return true;
        }

        if (commandSelection.ExecutarTreinamentoMultivariado)
        {
            Console.WriteLine();
            Console.WriteLine(
                "API iniciada em modo de treinamento multivariado.");

            using var scope = services.CreateScope();

            var trainingService =
                scope.ServiceProvider
                    .GetRequiredService<MultivariateTrainingService>();

            await trainingService.TreinarAsync();

            Console.WriteLine();
            Console.WriteLine(
                "Treinamento multivariado concluído.");

            return true;
        }

        return false;
    }
}