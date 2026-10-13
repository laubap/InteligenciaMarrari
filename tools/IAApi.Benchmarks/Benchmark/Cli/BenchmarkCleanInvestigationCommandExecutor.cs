using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkCleanInvestigationCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (!commandSelection.ExecutarInvestigacaoCleanAll)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo de investigação completa do banco limpo.");

        var diretorioProjeto =
            Directory.GetCurrentDirectory();

        var diretorioResultados =
            Path.Combine(
                diretorioProjeto,
                "Results");

        if (!Directory.Exists(diretorioResultados))
        {
            throw new DirectoryNotFoundException(
                $"Diretório de resultados não encontrado: " +
                $"{diretorioResultados}");
        }

        var arquivoDeteccoes =
            new DirectoryInfo(diretorioResultados)
                .GetFiles("clean_detections_*.jsonl")
                .OrderByDescending(
                    arquivo => arquivo.LastWriteTimeUtc)
                .FirstOrDefault();

        if (arquivoDeteccoes is null)
        {
            throw new FileNotFoundException(
                "Nenhum arquivo clean_detections_*.jsonl foi encontrado.");
        }

        using var scope =
            services.CreateScope();

        var investigator =
            scope.ServiceProvider
                .GetRequiredService<CalibrationAllInvestigator>();

        await investigator.InvestigateAllAsync(
            benchmarkOptions.CleanDatabasePath,
            arquivoDeteccoes.FullName,
            diretorioResultados);

        return true;
    }
}