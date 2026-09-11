namespace IAApi.Benchmark.PersistenceCounterfactual;

public enum PersistenceUnderlyingEvidenceKind
{
    NotApplicable,
    Mad,
    Deadband,
    StandardDeviationFallback,
    Unknown
}

public sealed record PersistenceCounterfactualRecord
{
    public int FastWindowIndex { get; init; }
    public int MinuteIndex { get; init; }
    public string Tag { get; init; } = "";
    public double LegacyScore { get; init; }
    public bool LegacyAnomaly { get; init; }
    public double CounterfactualScore { get; init; }
    public bool CounterfactualAnomaly { get; init; }
    public bool PersistenceContributionRemoved { get; init; }
    public PersistenceUnderlyingEvidenceKind UnderlyingEvidenceKind { get; init; }
    public bool MadAnomalous { get; init; }
    public bool PersistenceAnomalous { get; init; }
    public double PersistenceScore { get; init; }
    public int PersistenceConsecutivePoints { get; init; }
    public double PersistenceDurationMinutes { get; init; }
    public IReadOnlyList<string> AnomalousDetectors { get; init; } = [];
}

public sealed record PersistenceCounterfactualSummary
{
    public DateTime GeneratedUtc { get; init; }
    public long EvaluatedReadings { get; init; }
    public long LegacyAnomalyRecords { get; init; }
    public long CounterfactualAnomalyRecords { get; init; }
    public long RemovedAnomalyRecords { get; init; }
    public long AddedAnomalyRecords { get; init; }
    public IReadOnlyDictionary<string, long> EvidenceReadings { get; init; } =
        new Dictionary<string, long>();
    public IReadOnlyDictionary<string, long> EvidenceSequences { get; init; } =
        new Dictionary<string, long>();
}
