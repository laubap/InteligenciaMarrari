using IAApi.Benchmark.BehaviorProfiles;
using IAApi.Benchmark.Diagnostics;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Benchmark.PersistenceCounterfactual;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using Microsoft.Extensions.Configuration;

namespace IAApi.Benchmark.Cli;

public sealed record BenchmarkCommandContext(
    BaselineDiagnosticOptions BaselineDiagnosticOptions,
    RegimeShadowOptions RegimeShadowOptions,
    DualBaselineShadowOptions DualBaselineShadowOptions,
    BenchmarkBehaviorProfileOptions BehaviorProfileObservabilityOptions,
    PersistenceCounterfactualOptions PersistenceCounterfactualOptions,
    ExperimentalIncidentOptions ExperimentalIncidentOptions,
    ExperimentalAnomalyDecisionOptions ExperimentalAnomalyDecisionOptions,
    FunctionalOperationalCanaryOptions FunctionalOperationalCanaryOptions)
{
    public static BenchmarkCommandContext Create(
        string[] args,
        IConfiguration configuration,
        BenchmarkCommandSelection commandSelection)
    {
        var baselineDiagnosticOptions =
            BaselineDiagnosticOptions.Parse(args);

        var regimeShadowOptions =
            RegimeShadowOptions.Parse(args);

        var detectorOptionsForDualBaseline =
            configuration
                .GetRequiredSection("AnomalyDetection")
                .Get<AnomalyDetectionOptions>()
            ?? new AnomalyDetectionOptions();

        var dualBaselineShadowOptions =
            DualBaselineShadowOptions.Parse(
                args,
                detectorOptionsForDualBaseline);

        var behaviorProfileObservabilityOptions =
            BenchmarkBehaviorProfileOptions.Parse(args);

        var persistenceCounterfactualOptions =
            PersistenceCounterfactualOptions.Parse(args);

        var experimentalIncidentOptions =
            configuration
                .GetSection("ExperimentalIncidentLayer")
                .Get<ExperimentalIncidentOptions>()
            ?? new ExperimentalIncidentOptions();

        if (ExperimentalIncidentOptions.Parse(args).Enabled)
        {
            experimentalIncidentOptions =
                experimentalIncidentOptions with
                {
                    Enabled = true
                };
        }

        var experimentalAnomalyDecisionOptions =
            ExperimentalAnomalyDecisionOptions.Parse(args);

        var functionalOperationalCanaryOptions =
            new FunctionalOperationalCanaryOptions
            {
                Enabled =
                    commandSelection.FunctionalOperationalCanaryEnabled,

                AllowedTags =
                    commandSelection.FunctionalOperationalCanaryTags,

                AllowedFastWindows =
                    commandSelection.FunctionalOperationalCanaryWindows
            };

        return new BenchmarkCommandContext(
            baselineDiagnosticOptions,
            regimeShadowOptions,
            dualBaselineShadowOptions,
            behaviorProfileObservabilityOptions,
            persistenceCounterfactualOptions,
            experimentalIncidentOptions,
            experimentalAnomalyDecisionOptions,
            functionalOperationalCanaryOptions);
    }
}