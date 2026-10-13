using Microsoft.Extensions.Configuration;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkCommandOrchestrator
{
    public static async Task<bool> TryExecuteBeforePipelineAsync(
        IServiceProvider services,
        string[] args,
        IConfiguration configuration,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        var benchmarkCommandContext =
            BenchmarkCommandContext.Create(
                args,
                configuration,
                commandSelection);

        BenchmarkCommandValidator.Validate(
            commandSelection,
            benchmarkCommandContext);

        var functionalValidationMessages =
            BenchmarkCommandValidator.ValidateFunctional(
                commandSelection,
                benchmarkCommandContext);

        if (functionalValidationMessages.Length > 0)
        {
            foreach (var message in functionalValidationMessages)
            {
                Console.WriteLine(message);
            }

            return true;
        }

        var modeSelectionValidationMessage =
            BenchmarkCommandValidator.ValidateModeSelection(
                commandSelection);

        if (modeSelectionValidationMessage is not null)
        {
            Console.WriteLine();
            Console.WriteLine(
                modeSelectionValidationMessage);

            return true;
        }

        if (await BenchmarkUtilityCommandExecutor.TryExecuteAsync(
                services,
                commandSelection))
        {
            return true;
        }

        if (await BenchmarkRunCommandExecutor.TryExecuteAsync(
                services,
                commandSelection))
        {
            return true;
        }

        if (await BenchmarkFastCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkCommandContext))
        {
            return true;
        }

        if (await BenchmarkDeadbandImpactCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkOptions))
        {
            return true;
        }

        if (await BenchmarkCleanInvestigationCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkOptions))
        {
            return true;
        }

        if (await BenchmarkCleanEvaluationCommandExecutor.TryExecuteAsync(
                services,
                args,
                commandSelection))
        {
            return true;
        }

        if (await BenchmarkFunctionalOracleEvaluationCommandExecutor.TryExecuteAsync(
                args,
                commandSelection))
        {
            return true;
        }

        if (await BenchmarkEvaluationCommandExecutor.TryExecuteAsync(
                services,
                args,
                commandSelection))
        {
            return true;
        }

        if (await BenchmarkDetectorSensitivityCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkOptions))
        {
            return true;
        }

        return false;
    }

    public static async Task<bool> TryExecuteAfterPipelineAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BenchmarkOptions benchmarkOptions)
    {
        if (await BenchmarkResultsAnalysisCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkOptions))
        {
            return true;
        }

        if (await BenchmarkAdaptiveEnsembleCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkOptions))
        {
            return true;
        }

        if (await BenchmarkFamilyConsensusCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkOptions))
        {
            return true;
        }

        if (await BenchmarkConfidenceCommandExecutor.TryExecuteAsync(
                services,
                commandSelection,
                benchmarkOptions))
        {
            return true;
        }

        return false;
    }
}