using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

/// <summary>
/// Trecho temporal contínuo do histórico de uma tag.
///
/// Um segmento representa apenas continuidade temporal.
/// Ele ainda não representa um comportamento aprendido BEH-xxx.
/// </summary>
public sealed record BehaviorHistorySegment
{
    public int Index { get; init; }

    public DateTime Inicio { get; init; }

    public DateTime Fim { get; init; }

    public double? GapAnteriorMinutos { get; init; }

    public IReadOnlyList<LeituraBruta> Historico { get; init; } =
        Array.Empty<LeituraBruta>();

    public int QuantidadeLeituras =>
        Historico.Count;

    public TimeSpan Duracao =>
        Fim >= Inicio
            ? Fim - Inicio
            : TimeSpan.Zero;
}

/// <summary>
/// Resultado da segmentação temporal do histórico comportamental.
/// </summary>
public sealed record BehaviorHistorySegmentationResult
{
    public IReadOnlyList<BehaviorHistorySegment> Segments { get; init; } =
        Array.Empty<BehaviorHistorySegment>();

    public double GapThresholdMinutes { get; init; }

    public int TotalLeituras { get; init; }

    public int QuantidadeSegmentos =>
        Segments.Count;

    public bool FoiSegmentado =>
        Segments.Count > 1;
}

/// <summary>
/// Divide o histórico longo em trechos temporalmente contínuos.
///
/// A regra de quebra reutiliza exatamente o limite calculado pelo
/// DetectionHistorySanitizer:
///
///     critical gap = mediana da cadência * 5
///
/// Nenhuma classificação comportamental acontece nesta etapa.
/// </summary>
public sealed class BehaviorHistorySegmentationService
{
    public BehaviorHistorySegmentationResult Segment(
        BehaviorHistoryCollectionResult collection)
    {
        ArgumentNullException.ThrowIfNull(
            collection);

        var history =
            collection.Historico;

        var threshold =
            collection.Qualidade
                .CriticalGapThresholdMinutes;

        if (history.Count == 0)
        {
            return new BehaviorHistorySegmentationResult
            {
                GapThresholdMinutes =
                    threshold,

                TotalLeituras =
                    0
            };
        }

        var segments =
            new List<BehaviorHistorySegment>();

        var current =
            new List<LeituraBruta>
            {
                history[0]
            };

        double? currentPreviousGap =
            null;

        for (var index = 1;
             index < history.Count;
             index++)
        {
            var previous =
                history[index - 1];

            var reading =
                history[index];

            var gapMinutes =
                (
                    reading.DataHora -
                    previous.DataHora
                ).TotalMinutes;

            var isCriticalGap =
                threshold > 0 &&
                gapMinutes > threshold;

            if (!isCriticalGap)
            {
                current.Add(
                    reading);

                continue;
            }

            segments.Add(
                BuildSegment(
                    segments.Count,
                    current,
                    currentPreviousGap));

            current =
            [
                reading
            ];

            currentPreviousGap =
                gapMinutes;
        }

        segments.Add(
            BuildSegment(
                segments.Count,
                current,
                currentPreviousGap));

        return new BehaviorHistorySegmentationResult
        {
            Segments =
                segments,

            GapThresholdMinutes =
                threshold,

            TotalLeituras =
                history.Count
        };
    }

    private static BehaviorHistorySegment BuildSegment(
        int index,
        IReadOnlyList<LeituraBruta> history,
        double? previousGapMinutes)
    {
        return new BehaviorHistorySegment
        {
            Index =
                index,

            Inicio =
                history[0].DataHora,

            Fim =
                history[^1].DataHora,

            GapAnteriorMinutos =
                previousGapMinutes,

            Historico =
                history.ToArray()
        };
    }
}