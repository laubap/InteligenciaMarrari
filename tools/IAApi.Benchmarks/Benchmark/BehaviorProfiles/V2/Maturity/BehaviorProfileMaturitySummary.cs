using ServiceBehaviorProfileMaturityOptions =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity.BehaviorProfileMaturityOptions;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Maturity;

public sealed record BehaviorProfileMaturitySummary
{
    public ServiceBehaviorProfileMaturityOptions Parameters { get; init; } =
        new();

    public long ObservationCount { get; init; }

    public IReadOnlyDictionary<string, long> StableDistribution { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> MaturityStateDistribution { get; init; } =
        new Dictionary<string, long>();

    public int MaturedWindowCount { get; init; }

    public double AverageMinutesToMaturity { get; init; }

    public long PrematureClassificationsAvoided { get; init; }

    public long RawProfileChanges { get; init; }

    public long StableProfileChanges { get; init; }

    public long FalseRawFlipsAvoided { get; init; }

    public long UnknownFormationObservations { get; init; }

    public double UnknownFormationRatio { get; init; }

    public double StabilityAfterMaturityRatio { get; init; }
}