using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Readiness;

namespace IAApi.Benchmark.DetectorApplicabilityCounterfactual;

public sealed record DetectorApplicabilityCounterfactualRecord
{
    public int FastWindowIndex { get; init; }

    public string Tag { get; init; } = "";

    public int MinuteIndex { get; init; }

    public TagBehaviorType LegacyProfile { get; init; }

    public TagBehaviorType ProspectiveProfile { get; init; }

    public BehaviorProfileReadinessStatus Readiness { get; init; }

    public IReadOnlyList<string> LegacyApplicableDetectors { get; init; } =
        [];

    public IReadOnlyList<string> ProspectiveApplicableDetectors { get; init; } =
        [];

    public IReadOnlyList<string> AddedDetectors { get; init; } =
        [];

    public IReadOnlyList<string> RemovedDetectors { get; init; } =
        [];

    public bool? OriginalFinalAnomaly { get; set; }

    public double? OriginalFinalScore { get; set; }

    public string Reason { get; init; } = "";
}

public sealed record DetectorApplicabilityChangeDuration
{
    public IReadOnlyList<string> AddedDetectors { get; init; } =
        [];

    public IReadOnlyList<string> RemovedDetectors { get; init; } =
        [];

    public int MaximumDurationMinutes { get; init; }
}

public sealed record DetectorApplicabilityTagSummary
{
    public string Tag { get; init; } = "";

    public long EvaluatedReadings { get; init; }

    public long ReadyReadings { get; init; }

    public long DifferentApplicabilityReadings { get; init; }

    public IReadOnlyDictionary<string, long> AddedDetectorCounts { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> RemovedDetectorCounts { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyList<DetectorApplicabilityChangeDuration> MaximumChangeDurations { get; init; } =
        [];

    public IReadOnlyDictionary<string, long> DifferenceReasons { get; init; } =
        new Dictionary<string, long>();
}

public sealed record DetectorApplicabilityCounterfactualSummary
{
    public DateTime GeneratedUtc { get; init; }

    public long EvaluatedReadings { get; init; }

    public long ReadyReadings { get; init; }

    public long DifferentApplicabilityReadings { get; init; }

    public double PotentiallyAffectedPercent { get; init; }

    public string? MostFrequentlyAddedDetector { get; init; }

    public string? MostFrequentlyRemovedDetector { get; init; }

    public IReadOnlyDictionary<string, long> AddedDetectorCounts { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> RemovedDetectorCounts { get; init; } =
        new Dictionary<string, long>();

    public IReadOnlyDictionary<string, long> DifferencesByProspectiveProfile { get; init; } =
        new Dictionary<string, long>();

    public long DifferencesDuringOriginalAnomaly { get; init; }

    public IReadOnlyList<DetectorApplicabilityTagSummary> Tags { get; init; } =
        [];
}