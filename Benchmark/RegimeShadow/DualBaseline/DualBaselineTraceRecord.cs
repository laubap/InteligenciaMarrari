namespace IAApi.Benchmark.RegimeShadow.DualBaseline;

public sealed record DualBaselineTraceRecord
{
    public required int FastWindowIndex { get; init; }
    public required long MinuteIndex { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Tag { get; init; }
    public required double CurrentValue { get; init; }
    public required string RegimeState { get; init; }
    public string? EpisodeId { get; init; }
    public string? CandidateId { get; init; }
    public string? CandidateBaselineId { get; init; }
    public string? PolicyAction { get; init; }
    public bool FrozenSensorDataQuality { get; init; }

    public bool IncreasingNoiseEvidence { get; init; }
    public string? ActiveBaselineId { get; init; }
    public int? ActiveBaselineVersion { get; init; }
    public bool ActiveBaselineFrozen { get; init; }
    public DateTime? ActiveStartTimestamp { get; init; }
    public DateTime? ActiveEndTimestamp { get; init; }
    public int? ActiveSampleCount { get; init; }
    public double? ActiveMean { get; init; }
    public double? ActiveStd { get; init; }
    public double? ActiveMedian { get; init; }
    public double? ActiveMad { get; init; }
    public double? ActiveRobustScale { get; init; }
    public double? ActiveDeadband { get; init; }
    public string? ActiveDeadbandSource { get; init; }
    public double? ActiveCadenceMinutes { get; init; }
    public string CandidateReadiness { get; init; } = "NotReady";
    public bool CandidateFormationStarted { get; init; }
    public string CandidateFormationCoverage { get; init; } = "NotStarted";
    public string CandidateValidationCoverage { get; init; } = "NotStarted";
    public bool CandidateBaselineReady { get; init; }
    public int CandidateBaselineVersion { get; init; }
    public IReadOnlyList<string> CandidateReadinessReasons { get; init; } = [];
    public int TransitionPointCount { get; init; }
    public int CandidateFormationPointCount { get; init; }
    public int CandidateValidationPointCount { get; init; }
    public int CandidateCollectionPointCount { get; init; }
    public int CandidatePostReadyObservationCount { get; init; }
    public int CandidateInferredCoverageSamples { get; init; }
    public int CandidateSampleCount { get; init; }
    public int CandidateIndependentSamples { get; init; }
    public double CandidateDurationMinutes { get; init; }
    public double CandidateCadenceMinutes { get; init; }
    public double CandidateLargestGapMinutes { get; init; }
    public bool CandidateCriticalGap { get; init; }
    public bool CandidateCenterStable { get; init; }
    public bool CandidateScaleStable { get; init; }
    public bool CandidateResidualSlopeStable { get; init; }
    public double CandidateFormationDurationMinutes { get; init; }
    public double CandidateValidationDurationMinutes { get; init; }
    public double CandidateFormationMean { get; init; }
    public double CandidateValidationMean { get; init; }
    public double CandidateFormationStdDev { get; init; }
    public double CandidateValidationStdDev { get; init; }
    public double CandidateFormationRange { get; init; }
    public double CandidateValidationRange { get; init; }
    public double CandidateMaximumQuantileDelta { get; init; }
    public double CandidateAlignedMeanAbsoluteDifference { get; init; }
    public bool CandidateStructuralRepetitionObserved { get; init; }
    public double CandidateFormationMedian { get; init; }
    public double CandidateValidationMedian { get; init; }
    public double CandidateFormationRobustScale { get; init; }
    public double CandidateValidationRobustScale { get; init; }
    public double? CandidateMean { get; init; }
    public double? CandidateStd { get; init; }
    public double? CandidateMedian { get; init; }
    public double? CandidateMad { get; init; }
    public string? CandidateEffectiveScaleOrigin { get; init; }
    public DualDetectorEvaluation? MadActive { get; init; }
    public DualDetectorEvaluation? MadCandidate { get; init; }
    public DualDetectorEvaluation? ZActive { get; init; }
    public DualDetectorEvaluation? ZCandidate { get; init; }
    public DualEwmaEvaluation? EwmaActive { get; init; }
    public DualEwmaEvaluation? EwmaCandidate { get; init; }
    public string AgainstActive { get; init; } = "NotReady";
    public string AgainstCandidate { get; init; } = "NotReady";
    public string CandidateFit { get; init; } = "NotReady";
    public bool CandidateDegradationObserved { get; init; }
    public int ConsecutiveCandidateFitAnomalies { get; init; }
    public bool CurrentReadingInActiveReference { get; init; }
    public bool CurrentReadingInCandidateReference { get; init; }
    public bool OperationalBaselineChanged { get; init; }
    public bool Promotion { get; init; }
    public string Legitimacy { get; init; } = "None";
    public bool OriginalFinalAnomaly { get; init; }
    public double OriginalFinalScore { get; init; }
    public string? CandidateResetReason { get; init; }
}
