namespace IAApi.Services.AnomalyDetection.Runtime.Regime;

public sealed record CandidatePoint(long Sequence, DateTimeOffset Timestamp, double Value);

public sealed record CandidateEvidenceState(
    double ActiveCenter,
    double ActiveScale,
    double Deadband,
    IReadOnlyList<CandidatePoint> Points,
    int ConsecutiveReturnPoints = 0,
    string? EpisodeId = null,
    long? BufferStartedMinuteIndex = null,
    DateTimeOffset? BufferStartedTimestamp = null,
    double? BufferInitialValue = null,
    int PointCountBeforeCurrentEpisode = 0,
    IReadOnlyList<string>? FormationEvidenceSources = null)
{
    public static CandidateEvidenceState Empty { get; } = new(0, 0, 0, []);
    public bool EpisodeStarted => EpisodeId is not null;
    public bool CandidateStarted => Points.Count > 0;
    public bool Started => CandidateStarted;
    // B3.22.5 V1b - persistent variability reference bound to ActiveRegimeId.
    // Observational only: it does not change RegimePolicy decisions.
    public string? NoiseReferenceRegimeId { get; init; }
    public double NoiseReferenceCenter { get; init; }
    public double NoiseReferenceScale { get; init; }
    public DateTime? NoiseReferenceEstablishedAt { get; init; }
    public int NoiseReferencePointCount { get; init; }
    public double NoiseReferenceDurationMinutes { get; init; }

    public CandidateEvidenceState ResetCandidate() => this with
    {
        Points = [],
        BufferStartedMinuteIndex = null,
        BufferStartedTimestamp = null,
        BufferInitialValue = null,
        PointCountBeforeCurrentEpisode = 0,
        FormationEvidenceSources = [],
        ConsecutiveReturnPoints = 0
    };
    public CandidateEvidenceState ResetEpisode() => Empty with
    {
        NoiseReferenceRegimeId = NoiseReferenceRegimeId,
        NoiseReferenceCenter = NoiseReferenceCenter,
        NoiseReferenceScale = NoiseReferenceScale,
        NoiseReferenceEstablishedAt = NoiseReferenceEstablishedAt,
        NoiseReferencePointCount = NoiseReferencePointCount,
        NoiseReferenceDurationMinutes = NoiseReferenceDurationMinutes
    };
}

public sealed record RegimeShadowTagState(
    IAApi.RegimePolicy.RegimePolicyState PolicyState,
    CandidateEvidenceState CandidateEvidence,
    string? PendingResetReason = null);
