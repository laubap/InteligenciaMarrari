namespace IAApi.RegimePolicy;

public sealed record ReadingObservation(
    long Sequence,
    DateTimeOffset Timestamp,
    double Value,
    double MedianCadenceMinutes,
    RegimeSignalKind SignalKind,
    bool IsFinite = true,
    bool HasCriticalGap = false)
{
    /// <summary>
    /// B3.22.4 — indica evidência de leitura congelada.
    ///
    /// É uma condição de qualidade do dado/sensor e não um voto
    /// do Ensemble Legacy.
    /// </summary>
    public bool HasFrozenSensor { get; init; }
}