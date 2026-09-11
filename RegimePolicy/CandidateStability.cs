namespace IAApi.RegimePolicy;

public enum CandidateStabilityStatus
{
    InsufficientData = 0,
    Unstable = 1,
    Stabilizing = 2,
    Stable = 3
}

public sealed record CandidateStability
{
    public CandidateStability(
        CandidateStabilityStatus status,
        double confidence = 0,
        int independentSampleCount = 0,
        double observedDurationMinutes = 0,
        int completedCycles = 0,
        bool dispersionIsStable = false,
        bool residualSlopeIsStable = false,
        bool cadenceIsReliable = false,
        bool hasCriticalGap = false,
        IEnumerable<string>? reasons = null)
    {
        Status = status;
        Confidence = Clamp01(confidence);
        IndependentSampleCount = Math.Max(0, independentSampleCount);
        ObservedDurationMinutes = Math.Max(0, observedDurationMinutes);
        CompletedCycles = Math.Max(0, completedCycles);
        DispersionIsStable = dispersionIsStable;
        ResidualSlopeIsStable = residualSlopeIsStable;
        CadenceIsReliable = cadenceIsReliable;
        HasCriticalGap = hasCriticalGap;
        Reasons = Array.AsReadOnly((reasons ?? []).ToArray());
    }

    public CandidateStabilityStatus Status { get; }
    public double Confidence { get; }
    public int IndependentSampleCount { get; }
    public double ObservedDurationMinutes { get; }
    public int CompletedCycles { get; }
    public bool DispersionIsStable { get; }
    public bool ResidualSlopeIsStable { get; }
    public bool CadenceIsReliable { get; }
    public bool HasCriticalGap { get; }
    public IReadOnlyList<string> Reasons { get; }

    public static CandidateStability Insufficient { get; } =
        new(CandidateStabilityStatus.InsufficientData);

    private static double Clamp01(double value) =>
        double.IsFinite(value)
            ? Math.Clamp(value, 0, 1)
            : 0;
}
