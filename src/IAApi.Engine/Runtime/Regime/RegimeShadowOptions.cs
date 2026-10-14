namespace IAApi.Services.AnomalyDetection.Runtime.Regime;

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

}
    