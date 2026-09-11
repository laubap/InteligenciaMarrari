using System.Globalization;

namespace IAApi.Benchmark.RegimeShadow;

/// <summary>ParÃ¢metros exclusivamente experimentais do Shadow Mode.</summary>
public sealed record RegimeShadowOptions
{
    public bool Enabled { get; init; }
    public bool ObserveAllTags { get; init; }
    public bool DecisionCounterfactualEnabled { get; init; }
    public bool IncidentCounterfactualEnabled { get; init; }
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public int FormationMinimumPoints { get; init; } = 5;
    public double FormationMinimumMinutes { get; init; } = 4;
    public double MinimumDisplacementDeadbands { get; init; } = 3;
    public double MinimumDisplacementRobustScales { get; init; } = 3;
    public int StabilityMinimumIndependentSamples { get; init; } = 20;
    public double StabilityMinimumMinutes { get; init; } = 30;
    public double MaximumDispersionToDisplacementRatio { get; init; } = 0.10;
    public double MaximumResidualSlopeDriftRatio { get; init; } = 0.05;
    public int ReturnMinimumPoints { get; init; } = 3;
    public int CandidateBufferCapacity { get; init; } = 480;
    public double CandidateBufferHorizonMinutes { get; init; } = 480;

    // B3.22.2 — evidência experimental de slow drift.
    // Estes parâmetros não alteram o Ensemble Legacy.
    public int SlowDriftWindowPoints { get; init; } = 30;
    public int SlowDriftMinimumPoints { get; init; } = 20;
    public double SlowDriftMinimumMinutes { get; init; } = 20;
    public double SlowDriftMinimumDisplacementScales { get; init; } = 4;
    public double SlowDriftMinimumTrendToNoiseRatio { get; init; } = 4;
    public double SlowDriftMinimumDirectionalConsistency { get; init; } = 0.60;

    // B3.22.4 — evidência experimental de sensor congelado.
    // Estes parâmetros não alteram o Ensemble Legacy.
    public int FrozenMinimumPoints { get; init; } = 10;
    public double FrozenMinimumMinutes { get; init; } = 10;
    public int FrozenPriorWindowPoints { get; init; } = 30;
    public double FrozenMaximumPriorZeroRate { get; init; } = 0.60;

    // B3.22.5 V1b - observational increasing-noise evidence.
    // These parameters do not alter the Ensemble Legacy.
    public int IncreasingNoiseReferenceWindowPoints { get; init; } = 30;
    public int IncreasingNoiseRecentWindowPoints { get; init; } = 30;
    public int IncreasingNoiseMinimumPoints { get; init; } = 20;
    public double IncreasingNoiseMinimumMinutes { get; init; } = 20;
    public double IncreasingNoiseMinimumScaleRatio { get; init; } = 3.0;
    public double IncreasingNoiseMaximumCenterShiftScales { get; init; } = 2.0;
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
