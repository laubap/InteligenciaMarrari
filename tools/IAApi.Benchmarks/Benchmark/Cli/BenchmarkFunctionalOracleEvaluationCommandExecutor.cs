using IAApi.Benchmark.ExperimentalAnomalyDecisions;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkFunctionalOracleEvaluationCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        string[] args,
        BenchmarkCommandSelection commandSelection)
    {
        if (!commandSelection.ExecutarAvaliacaoFunctionalOracle)
            return false;

        Console.WriteLine();
        Console.WriteLine(
            "API iniciada em modo de avaliação funcional do Oracle.");

        var argumentos =
            args
                .Skip(
                    commandSelection.IndiceEvaluateFunctionalOracle + 1)
                .ToArray();

        if (argumentos.Length != 5)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Uso:");

            Console.WriteLine(
                "  dotnet run -c Release -- --evaluate-functional-oracle " +
                "<oracle-expandido.json> " +
                "<legacy-detections.jsonl> " +
                "<operational-detections.jsonl> " +
                "<functional-new-evidence.jsonl> " +
                "<diretorio-saida>");

            return true;
        }

        var expandedOraclePath =
            argumentos[0];

        var legacyDetectionsPath =
            argumentos[1];

        var operationalDetectionsPath =
            argumentos[2];

        var functionalNewEvidencePath =
            argumentos[3];

        var outputDirectory =
            argumentos[4];

        var evaluator =
            new FunctionalOraclePreservationEvaluator();

        await evaluator.EvaluateAsync(
            expandedOraclePath,
            legacyDetectionsPath,
            operationalDetectionsPath,
            functionalNewEvidencePath,
            outputDirectory);

        return true;
    }
}