namespace IAApi.Benchmark.Diagnostics;

public sealed class BaselineDiagnosticRecord
{
    public int FastWindowIndex { get; init; }
    public int MinuteIndex { get; init; }
    public DateTime Timestamp { get; init; }
    public string Tag { get; init; } = "";
    public double CurrentValue { get; init; }
    public int HistoryCount { get; init; }
    public int HistoryWindowCapacity { get; init; }
    public double HistoryWindowUtilization { get; init; }
    public DateTime? HistoryStart { get; init; }
    public DateTime? HistoryEnd { get; init; }
    public double HistoryDurationMinutes { get; init; }
    public double MedianCadenceMinutes { get; init; }
    public bool CurrentTimestampAlreadyInHistory { get; init; }
    public bool HistoryLatestBeforeCurrent { get; init; }
    public double Mean { get; init; }
    public double StdDev { get; init; }
    public double Median { get; init; }
    public double Mad { get; init; }
    public double Deadband { get; init; }
    public string DeadbandSource { get; init; } = "";
    public double DeadbandConfidence { get; init; }
    public double AutomaticDeadbandP95 { get; init; }
    public required IReadOnlyDictionary<string, DetectorDiagnostic> Detectors { get; init; }
    public bool FinalApplicable { get; init; }
    public bool FinalAnomaly { get; init; }
    public double FinalScore { get; init; }
    public double FinalNormalizedScore { get; init; }
    public int ApplicableDetectorCount { get; init; }
    public int AnomalousDetectorCount { get; init; }
}

public sealed class DetectorDiagnostic
{
    public bool Applicable { get; init; }
    public bool Vote { get; init; }
    public double Score { get; init; }
    public double NormalizedScore { get; init; }
    public IReadOnlyDictionary<string, double> Metrics { get; init; } =
        new Dictionary<string, double>();
}
