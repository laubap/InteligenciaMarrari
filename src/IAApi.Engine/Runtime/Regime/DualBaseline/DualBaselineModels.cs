using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

public enum DualBaselineReadiness { NotReady, Ready }
public enum DualStatisticalState { NotReady, Normal, Abnormal }
public enum CandidateCoverageStatus { NotStarted, Collecting, Covered, Validated }

public sealed record DualBaselinePoint(long MinuteIndex, DateTime Timestamp, double Value);

public sealed record DualBaselineReference(
    string Id,
    int Version,
    DateTime StartTimestamp,
    DateTime EndTimestamp,
    long? StartMinuteIndex,
    long? EndMinuteIndex,
    int SampleCount,
    double Mean,
    double StdDev,
    double Median,
    double Mad,
    double RobustScale,
    double Deadband,
    string DeadbandSource,
    double CadenceMinutes,
    double MadEffectiveScale,
    string MadEffectiveScaleOrigin,
    double ZEffectiveScale,
    string ZEffectiveScaleOrigin,
    IReadOnlyList<HistoricoDetectorPoint> Points);

public sealed record DualDetectorEvaluation(
    DualBaselineReadiness Readiness,
    double? Score,
    bool? Vote,
    double? Center,
    double? RawScale,
    double? EffectiveScale,
    string? EffectiveScaleOrigin);

public sealed record DualEwmaEvaluation(
    DualBaselineReadiness Readiness,
    double? Target,
    double? PreviousState,
    double? CurrentState,
    double? EffectiveAlpha,
    double? Score,
    bool? Vote);

public sealed record CandidateReadinessAudit(
    DualBaselineReadiness Status,
    IReadOnlyList<string> Reasons,
    int SampleCount,
    int IndependentSampleCount,
    double ObservedDurationMinutes,
    double CadenceMinutes,
    double LargestGapMinutes,
    bool HasCriticalGap,
    bool CenterIsStable,
    bool ScaleIsStable,
    bool ResidualSlopeIsStable,
    double FormationMedian,
    double ValidationMedian,
    double FormationRobustScale,
    double ValidationRobustScale,
    double ResidualSlopePerMinute,
    CandidateCoverageStatus FormationCoverage,
    CandidateCoverageStatus ValidationCoverage,
    int InferredCoverageSamples,
    double FormationDurationMinutes,
    double ValidationDurationMinutes,
    double FormationMean,
    double ValidationMean,
    double FormationStdDev,
    double ValidationStdDev,
    double FormationRange,
    double ValidationRange,
    double MaximumQuantileDelta,
    double AlignedMeanAbsoluteDifference,
    bool StructuralRepetitionObserved);

public sealed class DualEwmaState
{
    public required string BaselineId { get; init; }
    public required double Target { get; init; }
    public required double EffectiveBaseScale { get; init; }
    public required double CadenceMinutes { get; init; }
    public required double Value { get; set; }
    public required double VarianceFactor { get; set; }
    public DateTime? PreviousTimestamp { get; set; }
}

public sealed class DualBaselineTagState
{
    public DualBaselineReference? Active { get; set; }
    public DualBaselineReference? Candidate { get; set; }
    public List<DualBaselinePoint> TransitionPoints { get; } = [];
    public List<DualBaselinePoint> CandidateCollectionPoints { get; } = [];
    public List<DualBaselinePoint> FormationPoints { get; } = [];
    public List<DualBaselinePoint> ValidationPoints { get; } = [];
    public List<DualBaselinePoint> PostReadyObservationPoints { get; } = [];
    public DualEwmaState? ActiveEwma { get; set; }
    public DualEwmaState? CandidateEwma { get; set; }
    public string? EpisodeId { get; set; }
    public string? CandidateId { get; set; }
    public bool CandidateOpened { get; set; }
    public bool CandidateReady { get; set; }
    public int ActiveBaselineVersion { get; set; }
    public int CandidateBaselineVersion { get; set; }
    public int ConsecutiveCandidateFitAnomalies { get; set; }
    public string? LastResetReason { get; set; }
}
