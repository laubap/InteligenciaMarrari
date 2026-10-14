using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

namespace IAApi.Benchmark.Cli;

public static class DualBaselineShadowOptionsParser
{
    public static DualBaselineShadowOptions Parse(
        string[] args,
        AnomalyDetectionOptions detectorOptions)
    {
        ArgumentNullException.ThrowIfNull(detectorOptions);

        var experimentalIncident =
            args.Any(x => x.Equals(
                "--experimental-incident-layer",
                StringComparison.OrdinalIgnoreCase));

        var incidentCounterfactual =
            args.Any(x => x.Equals(
                "--stable-deviation-incident-counterfactual",
                StringComparison.OrdinalIgnoreCase));

        var decisionCounterfactual =
            experimentalIncident ||
            incidentCounterfactual ||
            args.Any(x => x.Equals(
                "--dual-baseline-decision-counterfactual",
                StringComparison.OrdinalIgnoreCase));

        var enabled =
            decisionCounterfactual ||
            args.Any(x => x.Equals(
                "--dual-baseline-shadow",
                StringComparison.OrdinalIgnoreCase));

        return new DualBaselineShadowOptions
        {
            Enabled = enabled,
            DecisionCounterfactualEnabled = decisionCounterfactual,
            ObserveAllTagsAndWindows = decisionCounterfactual,
            IncidentCounterfactualEnabled = incidentCounterfactual,
            MadThreshold = detectorOptions.MadThreshold,
            ZScoreThreshold = detectorOptions.ZScoreThreshold,
            EwmaLambda = detectorOptions.EwmaLambda,
            EwmaSigmaLimit = detectorOptions.EwmaSigmaLimit
        };
    }
}