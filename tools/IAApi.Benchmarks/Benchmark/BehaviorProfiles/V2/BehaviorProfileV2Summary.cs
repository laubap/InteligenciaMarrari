using IAApi.Benchmark.BehaviorProfiles.V2.Maturity;

using ServiceBehaviorProfileV2Result =
    IAApi.Services.AnomalyDetection.BehaviorProfiles.BehaviorProfileV2Result;

namespace IAApi.Benchmark.BehaviorProfiles.V2;

public sealed record BehaviorProfileV2Summary
{
    public string AlgorithmVersion { get; init; } =
        ServiceBehaviorProfileV2Result.AlgorithmVersion;

    public long ObservationCount { get; init; }

    public IReadOnlyDictionary<string, long> LegacyDistribution { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> V2Distribution { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, double> LegacyAverageConfidenceByClass { get; init; } =
        new Dictionary<string, double>();

    public IReadOnlyDictionary<string, double> V2AverageConfidenceByClass { get; init; } =
        new Dictionary<string, double>();

    public long LegacyClassChanges { get; init; }

    public long V2ClassChanges { get; init; }

    public double LegacyFlipRate { get; init; }

    public double V2FlipRate { get; init; }

    public long LegacyUnknownCount { get; init; }

    public long V2UnknownCount { get; init; }

    public double LegacyUnknownRatio { get; init; }

    public double V2UnknownRatio { get; init; }

    public long AgreementCount { get; init; }

    public double AgreementRatio { get; init; }

    public IReadOnlyDictionary<string, long> LegacyByV2Matrix { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> LegacyTemporalTransitions { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> V2TemporalTransitions { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> PrincipalDivergenceReasons { get; init; } =
        new Dictionary<string, long>();

    public BehaviorProfileMaturitySummary Maturity { get; init; } =
        new();
}