namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Metadados produzidos durante a sanitização central do histórico.
///
/// Nenhum ponto é descartado silenciosamente: a quantidade e o motivo
/// das remoções ficam disponíveis para os detectores, benchmarks e logs.
/// </summary>
public sealed class DetectionInputQuality
{
    public int OriginalPoints { get; init; }

    public int ValidPoints { get; init; }

    public int InvalidValues { get; init; }

    public int InvalidTimestamps { get; init; }

    public int DuplicateTimestamps { get; init; }

    public int OutOfOrderTransitions { get; init; }

    public double MedianIntervalMinutes { get; init; }

    public double LargestGapMinutes { get; init; }

    public double CriticalGapThresholdMinutes { get; init; }

    public bool HasCriticalGap { get; init; }

    public int RemovedPoints =>
        Math.Max(
            0,
            OriginalPoints - ValidPoints);

    public bool WasSanitized =>
        RemovedPoints > 0 ||
        OutOfOrderTransitions > 0;

    public bool HasQualityIssues =>
        InvalidValues > 0 ||
        InvalidTimestamps > 0 ||
        DuplicateTimestamps > 0 ||
        OutOfOrderTransitions > 0 ||
        HasCriticalGap;

    public static DetectionInputQuality Empty { get; } =
        new();
}
