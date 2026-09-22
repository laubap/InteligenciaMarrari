namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity;

/// <summary>
/// Parametros experimentais exclusivos da maturidade de perfil. Nao sao
/// compartilhados com Regime Shadow nem com detectores.
/// </summary>
public sealed record BehaviorProfileMaturityOptions
{
    public int MinimumHistorySamples { get; init; } = 60;

    public double MinimumObservedDurationMinutes { get; init; } = 59;

    public int MinimumCandidateSamples { get; init; } = 60;

    public double MinimumCandidateDurationMinutes { get; init; } = 59;

    public double MinimumWinnerMargin { get; init; } = 0.08;

    public double StrongRunnerUpScore { get; init; } = 0.70;

    public double CounterContinuousConflictMargin { get; init; } = 0.30;

    public int ExtendedConflictSamples { get; init; } = 360;

    public double ExtendedConflictDurationMinutes { get; init; } = 359;

    public int RecentWinnerWindowSize { get; init; } = 120;

    public int MaximumRecentFlipsForPromotion { get; init; } = 1;

    public int MaximumCriticalGapsForPromotion { get; init; } = 0;
}