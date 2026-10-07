using IAApi.Benchmark.BehaviorProfiles;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Benchmark.PersistenceCounterfactual;
using IAApi.Benchmark.Diagnostics;
using IAApi.Services.AnomalyDetection.Runtime.Regime;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

namespace IAApi.Benchmark.Cli;

public static class BenchmarkCommandValidator
{
    public static void Validate(
        BenchmarkCommandSelection commandSelection,
        BaselineDiagnosticOptions baselineDiagnosticOptions,
        RegimeShadowOptions regimeShadowOptions,
        DualBaselineShadowOptions dualBaselineShadowOptions,
        BenchmarkBehaviorProfileOptions behaviorProfileObservabilityOptions,
        PersistenceCounterfactualOptions persistenceCounterfactualOptions,
        ExperimentalIncidentOptions experimentalIncidentOptions,
        ExperimentalAnomalyDecisionOptions experimentalAnomalyDecisionOptions)
    {
        if (baselineDiagnosticOptions.Enabled &&
            !commandSelection.ExecutarBenchmarkCleanFast)
        {
            throw new InvalidOperationException(
                "--b2-baseline-diagnostics só pode ser usado com " +
                "--benchmark-clean-fast.");
        }

        if (regimeShadowOptions.Enabled &&
            !commandSelection.ExecutarBenchmarkCleanFast &&
            !(regimeShadowOptions.DecisionCounterfactualEnabled &&
              commandSelection.ExecutarBenchmarkFast))
        {
            throw new InvalidOperationException(
                "--regime-shadow só pode ser usado com --benchmark-clean-fast.");
        }

        if (dualBaselineShadowOptions.Enabled &&
            (!regimeShadowOptions.Enabled ||
             (!dualBaselineShadowOptions.DecisionCounterfactualEnabled &&
              (!commandSelection.ExecutarBenchmarkCleanFast ||
               !regimeShadowOptions.Tags.Contains(
                   DualBaselineShadowOptions.ReferenceTag))) ||
             (dualBaselineShadowOptions.DecisionCounterfactualEnabled &&
              !commandSelection.ExecutarBenchmarkCleanFast &&
              !commandSelection.ExecutarBenchmarkFast)))
        {
            throw new InvalidOperationException(
                "--dual-baseline-shadow exige --benchmark-clean-fast --regime-shadow " +
                "--regime-shadow-tags=DRYCHAMBER04.MATERIAL_TEMPERATURE.");
        }

        if (experimentalAnomalyDecisionOptions.Enabled &&
            !commandSelection.ExecutarBenchmarkCleanFast &&
            !commandSelection.ExecutarBenchmarkFast)
        {
            throw new InvalidOperationException(
                "--experimental-anomaly-decision só pode ser usado com " +
                "--benchmark-clean-fast ou --benchmark-fast.");
        }

        if (experimentalAnomalyDecisionOptions.Enabled &&
            !dualBaselineShadowOptions.DecisionCounterfactualEnabled)
        {
            throw new InvalidOperationException(
                "--experimental-anomaly-decision exige o " +
                "--dual-baseline-decision-counterfactual.");
        }

        if (experimentalAnomalyDecisionOptions.Enabled &&
            !experimentalIncidentOptions.Enabled)
        {
            throw new InvalidOperationException(
                "--experimental-anomaly-decision exige " +
                "--experimental-incident-layer.");
        }

        if (behaviorProfileObservabilityOptions.Enabled &&
            !commandSelection.ExecutarBenchmarkCleanFast)
        {
            throw new InvalidOperationException(
                "--tag-profile-observability so pode ser usado com " +
                "--benchmark-clean-fast.");
        }

        if (persistenceCounterfactualOptions.Enabled &&
            !commandSelection.ExecutarBenchmarkFast)
        {
            throw new InvalidOperationException(
                "--persistence-counterfactual so pode ser usado com --benchmark-fast.");
        }

        if (commandSelection.PersistenceTemporalExperiment &&
            !commandSelection.ExecutarBenchmarkFast &&
            !commandSelection.ExecutarBenchmarkCleanFast)
        {
            throw new InvalidOperationException(
                "--experimental-persistence-temporal-confirmation so pode ser usado " +
                "com --benchmark-fast ou --benchmark-clean-fast.");
        }
    }

    public static string[] ValidateFunctional(
    BenchmarkCommandSelection commandSelection,
    ExperimentalAnomalyDecisionOptions experimentalAnomalyDecisionOptions)
{
    if (commandSelection.FunctionalExperimentalAnomalyDecisionEnabled &&
        !experimentalAnomalyDecisionOptions.Enabled)
    {
        return
        [
            "ERRO: --functional-experimental-anomaly-decision requer " +
            "--experimental-anomaly-decision."
        ];
    }

    if (commandSelection.FunctionalOperationalCanaryEnabled &&
        !commandSelection.FunctionalExperimentalAnomalyDecisionEnabled)
    {
        return
        [
            "ERRO: --functional-operational-canary requer " +
            "--functional-experimental-anomaly-decision."
        ];
    }

    if (commandSelection.FunctionalOperationalCanaryEnabled &&
        (commandSelection.FunctionalOperationalCanaryTags.Count == 0 ||
         commandSelection.FunctionalOperationalCanaryWindows.Count == 0))
    {
        return
        [
            "ERRO: o canário operacional exige pelo menos uma tag e " +
            "uma janela FAST explícitas.",

            "Exemplo: --functional-operational-canary " +
            "--functional-operational-canary-window=1 " +
            "--functional-operational-canary-tag=WEATHER.COOLING_DEGREE_INDEX"
        ];
    }

    return [];
}

public static string? ValidateModeSelection(
    BenchmarkCommandSelection commandSelection)
{
    if (commandSelection.QuantidadeModosSelecionados <= 1)
        return null;

    return
        "Use apenas um modo por execução: " +
        "--benchmark, --benchmark-clean, --benchmark-fast, " +
        "--benchmark-clean-fast, --evaluate, " +
        "--evaluate-functional-oracle, --evaluate-clean, " +
        "--investigate-clean-all, --test-groups, " +
        "--test-multivariate-buffer, --test-multivariate-dataset, " +
        "--evaluate-deadband-impact, --test-multivariate-detector, " +
        "--analyze-results, --analyze-detector-sensitivity, " +
        "--adaptive-ensemble-lab, --family-consensus-report, " +
        "--confidence-benchmark ou --train-multivariate.";
}
}