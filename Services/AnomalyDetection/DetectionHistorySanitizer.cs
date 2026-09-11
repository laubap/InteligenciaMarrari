namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Sanitiza o histórico uma única vez, antes da execução dos detectores.
///
/// Regras:
/// - remove valores NaN/Infinity;
/// - remove timestamps inválidos;
/// - elimina timestamps duplicados mantendo a última ocorrência;
/// - ordena somente quando necessário;
/// - mede cadência e gaps;
/// - registra tudo em DetectionInputQuality.
///
/// O objetivo é impedir que cada detector implemente uma versão diferente
/// da limpeza do mesmo histórico.
/// </summary>
public static class DetectionHistorySanitizer
{
    private const double CriticalGapMultiplier = 5.0;

    public static DetectionHistorySanitizationResult Sanitize(
        IReadOnlyList<HistoricoDetectorPoint>? history)
    {
        history ??=
            Array.Empty<HistoricoDetectorPoint>();

        if (history.Count == 0)
        {
            return new DetectionHistorySanitizationResult(
                Array.Empty<HistoricoDetectorPoint>(),
                DetectionInputQuality.Empty);
        }

        var valid =
            new List<IndexedHistoryPoint>(
                history.Count);

        var invalidValues = 0;
        var invalidTimestamps = 0;
        var outOfOrderTransitions = 0;

        DateTime? previousValidTimestamp = null;

        for (var index = 0;
             index < history.Count;
             index++)
        {
            var point =
                history[index];

            if (point is null)
            {
                invalidValues++;
                continue;
            }

            if (!double.IsFinite(point.Valor))
            {
                invalidValues++;
                continue;
            }

            if (!IsValidTimestamp(point.DataHora))
            {
                invalidTimestamps++;
                continue;
            }

            if (previousValidTimestamp.HasValue &&
                point.DataHora <
                previousValidTimestamp.Value)
            {
                outOfOrderTransitions++;
            }

            previousValidTimestamp =
                point.DataHora;

            valid.Add(
                new IndexedHistoryPoint(
                    index,
                    point));
        }

        if (valid.Count == 0)
        {
            var emptyQuality =
                new DetectionInputQuality
                {
                    OriginalPoints =
                        history.Count,

                    ValidPoints =
                        0,

                    InvalidValues =
                        invalidValues,

                    InvalidTimestamps =
                        invalidTimestamps,

                    OutOfOrderTransitions =
                        outOfOrderTransitions
                };

            return new DetectionHistorySanitizationResult(
                Array.Empty<HistoricoDetectorPoint>(),
                emptyQuality);
        }

        /*
         * Só ordenamos quando encontramos uma transição fora de ordem.
         * Isso preserva o caminho rápido do benchmark, cujo histórico
         * normalmente já chega cronológico.
         */
        if (outOfOrderTransitions > 0)
        {
            valid.Sort(
                static (left, right) =>
                {
                    var timestampComparison =
                        left.Point.DataHora.CompareTo(
                            right.Point.DataHora);

                    return timestampComparison != 0
                        ? timestampComparison
                        : left.OriginalIndex.CompareTo(
                            right.OriginalIndex);
                });
        }

        var sanitized =
            new List<HistoricoDetectorPoint>(
                valid.Count);

        var duplicateTimestamps = 0;

        for (var index = 0;
             index < valid.Count;
             index++)
        {
            var current =
                valid[index];

            if (sanitized.Count > 0 &&
                sanitized[^1].DataHora ==
                current.Point.DataHora)
            {
                /*
                 * Mantém a última ocorrência do timestamp, reproduzindo
                 * a regra anteriormente usada pelo PredictionService.
                 */
                sanitized[^1] =
                    Clone(current.Point);

                duplicateTimestamps++;

                continue;
            }

            sanitized.Add(
                Clone(current.Point));
        }

        var intervalMetrics =
            CalculateIntervalMetrics(
                sanitized);

        var criticalGapThreshold =
            intervalMetrics.MedianIntervalMinutes > 0
                ? intervalMetrics.MedianIntervalMinutes *
                  CriticalGapMultiplier
                : 0;

        var hasCriticalGap =
            criticalGapThreshold > 0 &&
            intervalMetrics.LargestGapMinutes >
            criticalGapThreshold;

        var quality =
            new DetectionInputQuality
            {
                OriginalPoints =
                    history.Count,

                ValidPoints =
                    sanitized.Count,

                InvalidValues =
                    invalidValues,

                InvalidTimestamps =
                    invalidTimestamps,

                DuplicateTimestamps =
                    duplicateTimestamps,

                OutOfOrderTransitions =
                    outOfOrderTransitions,

                MedianIntervalMinutes =
                    intervalMetrics.MedianIntervalMinutes,

                LargestGapMinutes =
                    intervalMetrics.LargestGapMinutes,

                CriticalGapThresholdMinutes =
                    criticalGapThreshold,

                HasCriticalGap =
                    hasCriticalGap
            };

        /*
         * Evita alocação extra no caminho comum quando nada precisou ser
         * removido, deduplicado ou reordenado.
         */
        if (!quality.WasSanitized &&
            sanitized.Count == history.Count)
        {
            return new DetectionHistorySanitizationResult(
                history,
                quality);
        }

        return new DetectionHistorySanitizationResult(
            sanitized.AsReadOnly(),
            quality);
    }

    private static bool IsValidTimestamp(
        DateTime timestamp)
    {
        return timestamp != default &&
               timestamp > DateTime.MinValue &&
               timestamp < DateTime.MaxValue;
    }

    private static HistoricoDetectorPoint Clone(
        HistoricoDetectorPoint point)
    {
        return new HistoricoDetectorPoint
        {
            DataHora =
                point.DataHora,

            Valor =
                point.Valor
        };
    }

    private static IntervalMetrics CalculateIntervalMetrics(
        IReadOnlyList<HistoricoDetectorPoint> history)
    {
        if (history.Count < 2)
        {
            return new IntervalMetrics(
                0,
                0);
        }

        var intervals =
            new double[
                history.Count - 1];

        var count = 0;
        var largestGap = 0.0;

        for (var index = 1;
             index < history.Count;
             index++)
        {
            var minutes =
                (history[index].DataHora -
                 history[index - 1].DataHora)
                .TotalMinutes;

            if (minutes <= 0 ||
                !double.IsFinite(minutes))
            {
                continue;
            }

            intervals[count++] =
                minutes;

            if (minutes > largestGap)
            {
                largestGap =
                    minutes;
            }
        }

        if (count == 0)
        {
            return new IntervalMetrics(
                0,
                0);
        }

        if (count != intervals.Length)
        {
            Array.Resize(
                ref intervals,
                count);
        }

        var median =
            DetectorMath.Median(
                intervals);

        return new IntervalMetrics(
            median,
            largestGap);
    }

    private readonly record struct IndexedHistoryPoint(
        int OriginalIndex,
        HistoricoDetectorPoint Point);

    private readonly record struct IntervalMetrics(
        double MedianIntervalMinutes,
        double LargestGapMinutes);
}

public sealed record DetectionHistorySanitizationResult(
    IReadOnlyList<HistoricoDetectorPoint> History,
    DetectionInputQuality Quality);
