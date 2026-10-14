using System.Globalization;
using IAApi.Services.AnomalyDetection.Runtime.Regime;

namespace IAApi.Benchmark.Cli;

public static class RegimeShadowOptionsParser
{
    public static RegimeShadowOptions Parse(string[] args)
    {
        var experimentalIncident = args.Any(x => x.Equals(
            "--experimental-incident-layer", StringComparison.OrdinalIgnoreCase));
        var incidentCounterfactual = args.Any(x => x.Equals(
            "--stable-deviation-incident-counterfactual", StringComparison.OrdinalIgnoreCase));
        var decisionCounterfactual = experimentalIncident || incidentCounterfactual || args.Any(x => x.Equals(
            "--dual-baseline-decision-counterfactual", StringComparison.OrdinalIgnoreCase));
        var enabled = decisionCounterfactual ||
            args.Any(x => x.Equals("--regime-shadow", StringComparison.OrdinalIgnoreCase));
        if (!enabled) return new();

        var tags = Read(args, "--regime-shadow-tags")?.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? (decisionCounterfactual
                ? []
                : throw new InvalidOperationException("--regime-shadow-tags Ã© obrigatÃ³rio."));

        var defaults = new RegimeShadowOptions();

        return defaults with
        {
            Enabled = true,
            ObserveAllTags = decisionCounterfactual,
            DecisionCounterfactualEnabled = decisionCounterfactual,
            IncidentCounterfactualEnabled = incidentCounterfactual,
            Tags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase),

            FormationMinimumPoints = Int(
                args,
                "--shadow-formation-points",
                defaults.FormationMinimumPoints),

            FormationMinimumMinutes = Double(
                args,
                "--shadow-formation-minutes",
                defaults.FormationMinimumMinutes),

            MinimumDisplacementDeadbands = Double(
                args,
                "--shadow-displacement-deadbands",
                defaults.MinimumDisplacementDeadbands),

            MinimumDisplacementRobustScales = Double(
                args,
                "--shadow-displacement-scales",
                defaults.MinimumDisplacementRobustScales),

            StabilityMinimumIndependentSamples = Int(
                args,
                "--shadow-stability-samples",
                defaults.StabilityMinimumIndependentSamples),

            StabilityMinimumMinutes = Double(
                args,
                "--shadow-stability-minutes",
                defaults.StabilityMinimumMinutes),

            MaximumDispersionToDisplacementRatio = Double(
                args,
                "--shadow-max-dispersion-ratio",
                defaults.MaximumDispersionToDisplacementRatio),

            MaximumResidualSlopeDriftRatio = Double(
                args,
                "--shadow-max-slope-ratio",
                defaults.MaximumResidualSlopeDriftRatio),

            ReturnMinimumPoints = Int(
                args,
                "--shadow-return-points",
                defaults.ReturnMinimumPoints),

            CandidateBufferCapacity = Int(
                args,
                "--shadow-buffer-capacity",
                defaults.CandidateBufferCapacity),

            CandidateBufferHorizonMinutes = Double(
                args,
                "--shadow-buffer-minutes",
                defaults.CandidateBufferHorizonMinutes),

            SlowDriftWindowPoints = Int(
                args,
                "--shadow-slow-drift-window-points",
                defaults.SlowDriftWindowPoints),

            SlowDriftMinimumPoints = Int(
                args,
                "--shadow-slow-drift-min-points",
                defaults.SlowDriftMinimumPoints),

            SlowDriftMinimumMinutes = Double(
                args,
                "--shadow-slow-drift-min-minutes",
                defaults.SlowDriftMinimumMinutes),

            SlowDriftMinimumDisplacementScales = Double(
                args,
                "--shadow-slow-drift-displacement-scales",
                defaults.SlowDriftMinimumDisplacementScales),

            SlowDriftMinimumTrendToNoiseRatio = Double(
                args,
                "--shadow-slow-drift-trend-noise-ratio",
                defaults.SlowDriftMinimumTrendToNoiseRatio),

            SlowDriftMinimumDirectionalConsistency = Double(
                args,
                "--shadow-slow-drift-directional-consistency",
                defaults.SlowDriftMinimumDirectionalConsistency),

            FrozenMinimumPoints = Int(
                args,
                "--shadow-frozen-min-points",
                defaults.FrozenMinimumPoints),

            FrozenMinimumMinutes = Double(
                args,
                "--shadow-frozen-min-minutes",
                defaults.FrozenMinimumMinutes),

            FrozenPriorWindowPoints = Int(
                args,
                "--shadow-frozen-prior-window-points",
                defaults.FrozenPriorWindowPoints),            FrozenMaximumPriorZeroRate = Double(
                args,
                "--shadow-frozen-max-prior-zero-rate",
                defaults.FrozenMaximumPriorZeroRate),

            IncreasingNoiseReferenceWindowPoints = Int(
                args,
                "--shadow-increasing-noise-reference-points",
                defaults.IncreasingNoiseReferenceWindowPoints),

            IncreasingNoiseRecentWindowPoints = Int(
                args,
                "--shadow-increasing-noise-recent-points",
                defaults.IncreasingNoiseRecentWindowPoints),

            IncreasingNoiseMinimumPoints = Int(
                args,
                "--shadow-increasing-noise-min-points",
                defaults.IncreasingNoiseMinimumPoints),

            IncreasingNoiseMinimumMinutes = Double(
                args,
                "--shadow-increasing-noise-min-minutes",
                defaults.IncreasingNoiseMinimumMinutes),

            IncreasingNoiseMinimumScaleRatio = Double(
                args,
                "--shadow-increasing-noise-scale-ratio",
                defaults.IncreasingNoiseMinimumScaleRatio),

            IncreasingNoiseMaximumCenterShiftScales = Double(
                args,
                "--shadow-increasing-noise-max-center-shift-scales",
                defaults.IncreasingNoiseMaximumCenterShiftScales)
        };
    }
        private static string? Read(
        IReadOnlyList<string> args,
        string name)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith(
                    name + "=",
                    StringComparison.OrdinalIgnoreCase))
            {
                return args[i][(name.Length + 1)..];
            }

            if (args[i].Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Count)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int Int(
        string[] a,
        string n,
        int d) =>
        int.TryParse(
            Read(a, n),
            out var v) &&
        v > 0
            ? v
            : d;

    private static double Double(
        string[] a,
        string n,
        double d) =>
        double.TryParse(
            Read(a, n),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var v) &&
        v > 0
            ? v
            : d;
}