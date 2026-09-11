namespace IAApi.Benchmark.BehaviorProfiles.V2;

public sealed record BehaviorProfileFeatures
{
    public int SampleCount { get; init; }
    public double ObservedDurationMinutes { get; init; }
    public double MedianCadenceMinutes { get; init; }
    public double MaximumGapMinutes { get; init; }
    public int CriticalGapCount { get; init; }
    public int DistinctValueCount { get; init; }
    public double DistinctValueRatio { get; init; }
    public double IntegerLikeRatio { get; init; }
    public double ValueRange { get; init; }
    public double Median { get; init; }
    public double Mad { get; init; }
    public double RobustScale { get; init; }
    public double EstimatedResolution { get; init; }
    public int OrderedLevelCount { get; init; }
    public double TypicalAbsoluteStep { get; init; }
    public double P90AbsoluteStep { get; init; }
    public double StepToRangeRatio { get; init; }
    public double StepToRobustScaleRatio { get; init; }
    public double ChangeRatio { get; init; }
    public double ZeroDeltaRatio { get; init; }
    public double PositiveDeltaRatio { get; init; }
    public double NegativeDeltaRatio { get; init; }
    public int DownwardStepCount { get; init; }
    public double MedianDownwardStepIntervalSamples { get; init; }
    public double DownwardStepIntervalCoefficientVariation { get; init; }
    public double DownwardStepIntervalRegularityRatio { get; init; }
    public double NonDecreasingRatio { get; init; }
    public double ReversalRatio { get; init; }
    public double AdjacentLevelTransitionRatio { get; init; }
    public double SmoothTransitionRatio { get; init; }
    public double AbruptJumpRatio { get; init; }
    public double MedianDwellSamples { get; init; }
    public int MaximumDwellSamples { get; init; }
    public int ResetCount { get; init; }
    public double ResetRatio { get; init; }
}
