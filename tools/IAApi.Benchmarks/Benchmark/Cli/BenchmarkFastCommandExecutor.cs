using IAApi.Benchmark.BehaviorProfiles;
using IAApi.Benchmark.Diagnostics;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Benchmark.PersistenceCounterfactual;
using IAApi.Services.AnomalyDetection.Runtime.Regime;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkFastCommandExecutor
{
    public static async Task<bool> TryExecuteAsync(
        IServiceProvider services,
        BenchmarkCommandSelection commandSelection,
        BaselineDiagnosticOptions baselineDiagnosticOptions,
        RegimeShadowOptions regimeShadowOptions,
        DualBaselineShadowOptions dualBaselineShadowOptions,
        BenchmarkBehaviorProfileOptions behaviorProfileObservabilityOptions,
        PersistenceCounterfactualOptions persistenceCounterfactualOptions,
        ExperimentalIncidentOptions experimentalIncidentOptions,
        ExperimentalAnomalyDecisionOptions experimentalAnomalyDecisionOptions,
        FunctionalOperationalCanaryOptions functionalOperationalCanaryOptions)
    {
        if (commandSelection.ExecutarBenchmarkCleanFast)
        {
            Console.WriteLine();
            Console.WriteLine(
                "API iniciada em modo BENCHMARK FAST do banco limpo.");

            WriteFunctionalStatus(commandSelection);

            using var scope = services.CreateScope();

            var runner =
                scope.ServiceProvider
                    .GetRequiredService<BenchmarkRunner>();

            await runner.ExecutarAsync(
                bancoLimpo: true,
                modoRapido: true,
                baselineDiagnostics:
                    baselineDiagnosticOptions,
                regimeShadow:
                    regimeShadowOptions,
                dualBaselineShadow:
                    dualBaselineShadowOptions,
                behaviorProfileObservability:
                    behaviorProfileObservabilityOptions,
                persistenceTemporalExperiment:
                    commandSelection.PersistenceTemporalExperiment,
                experimentalIncident:
                    experimentalIncidentOptions,
                experimentalAnomalyDecision:
                    experimentalAnomalyDecisionOptions,
                functionalExperimentalAnomalyDecisionEnabled:
                    commandSelection.FunctionalExperimentalAnomalyDecisionEnabled,
                functionalOperationalCanary:
                    functionalOperationalCanaryOptions);

            Console.WriteLine();
            Console.WriteLine(
                "Execução do benchmark FAST limpo concluída.");

            return true;
        }

        if (commandSelection.ExecutarBenchmarkFast)
        {
            Console.WriteLine();
            Console.WriteLine(
                "API iniciada em modo BENCHMARK FAST do banco com anomalias.");

            WriteFunctionalStatus(commandSelection);

            using var scope = services.CreateScope();

            var runner =
                scope.ServiceProvider
                    .GetRequiredService<BenchmarkRunner>();

            await runner.ExecutarAsync(
                bancoLimpo: false,
                modoRapido: true,
                regimeShadow:
                    regimeShadowOptions,
                dualBaselineShadow:
                    dualBaselineShadowOptions,
                persistenceCounterfactual:
                    persistenceCounterfactualOptions,
                persistenceTemporalExperiment:
                    commandSelection.PersistenceTemporalExperiment,
                experimentalIncident:
                    experimentalIncidentOptions,
                experimentalAnomalyDecision:
                    experimentalAnomalyDecisionOptions,
                functionalExperimentalAnomalyDecisionEnabled:
                    commandSelection.FunctionalExperimentalAnomalyDecisionEnabled,
                functionalOperationalCanary:
                    functionalOperationalCanaryOptions);

            Console.WriteLine();
            Console.WriteLine(
                "Execução do benchmark FAST com anomalias concluída.");

            return true;
        }

        return false;
    }

    private static void WriteFunctionalStatus(
        BenchmarkCommandSelection commandSelection)
    {
        if (!commandSelection.FunctionalExperimentalAnomalyDecisionEnabled)
            return;

        Console.WriteLine(
            "Functional Experimental Anomaly Decision: ENABLED");

        if (!commandSelection.FunctionalOperationalCanaryEnabled)
            return;

        Console.WriteLine(
            "Functional Operational Canary: ENABLED");

        Console.WriteLine(
            $"Canary FAST windows: " +
            $"{string.Join(", ", commandSelection.FunctionalOperationalCanaryWindows.OrderBy(x => x))}");

        Console.WriteLine(
            $"Canary tags: " +
            $"{string.Join(", ", commandSelection.FunctionalOperationalCanaryTags.OrderBy(x => x))}");
    }

    public static Task<bool> TryExecuteAsync(
    IServiceProvider services,
    BenchmarkCommandSelection commandSelection,
    BenchmarkCommandContext context)
{
    return TryExecuteAsync(
        services,
        commandSelection,
        context.BaselineDiagnosticOptions,
        context.RegimeShadowOptions,
        context.DualBaselineShadowOptions,
        context.BehaviorProfileObservabilityOptions,
        context.PersistenceCounterfactualOptions,
        context.ExperimentalIncidentOptions,
        context.ExperimentalAnomalyDecisionOptions,
        context.FunctionalOperationalCanaryOptions);
}
}