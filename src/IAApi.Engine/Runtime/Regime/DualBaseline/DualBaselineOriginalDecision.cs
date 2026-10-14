namespace IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

public sealed record DualBaselineOriginalDecision(
    bool IsAnomaly,
    double Score);