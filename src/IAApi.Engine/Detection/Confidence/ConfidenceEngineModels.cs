namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Consolidação das evidências de uma única família.
/// </summary>
public sealed class ConfidenceFamilyResult
{
    public DetectorFamily Family { get; init; }

    /// <summary>
    /// Score consolidado da família entre 0 e 1.
    /// </summary>
    public double Score { get; init; }

    public double AverageEvidence { get; init; }
    public double MaximumEvidence { get; init; }
    public double AverageConfidence { get; init; }

    public int ApplicableDetectorCount { get; init; }
    public int PositiveVoteCount { get; init; }

    public IReadOnlyList<string> Detectors { get; init; } =
        Array.Empty<string>();
}

/// <summary>
/// Resultado isolado do Confidence Engine.
/// </summary>
public sealed class ConfidenceResult
{
    public ConfidenceLevel Level { get; init; }

    /// <summary>
    /// Score final entre 0 e 1.
    /// </summary>
    public double Score { get; init; }

    public bool HasEvidence =>
        Level is
            ConfidenceLevel.Suspicious or
            ConfidenceLevel.Probable or
            ConfidenceLevel.Confirmed;

    public int ApplicableDetectorCount { get; init; }
    public int PositiveVoteCount { get; init; }
    public int PositiveFamilyCount { get; init; }

    public bool HasTemporalConfirmation { get; init; }
    public bool HasMultivariateEvidence { get; init; }

    public string Explanation { get; init; } = "";

    public IReadOnlyList<string> Reasons { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<ConfidenceFamilyResult> Families { get; init; } =
        Array.Empty<ConfidenceFamilyResult>();

    public IReadOnlyList<DetectorResultV2> ApplicableResults { get; init; } =
        Array.Empty<DetectorResultV2>();
}
