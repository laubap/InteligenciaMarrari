namespace IAApi.Benchmark.BehaviorProfiles.V2.Structural;

public sealed record ObservationCoverage
{
    public int SampleCount { get; init; }
    public double DurationMinutes { get; init; }
    public int SegmentCount { get; init; }
    public int SegmentsWithActivity { get; init; }
    public int SegmentsWithPositiveProgression { get; init; }
    public IReadOnlyList<int> PositiveChangesBySegment { get; init; } = [];
    public IReadOnlyList<int> ChangesBySegment { get; init; } = [];
    public IReadOnlyList<double> NetProgressionBySegment { get; init; } = [];
    public int CriticalGapCount { get; init; }
}
