using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

public sealed class BehaviorProfileFeatureExtractor
{
    private const double IntegerTolerance = 1e-9;

    public BehaviorProfileFeatures Extract(
        IReadOnlyList<HistoricoDetectorPoint> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var points = history
            .Where(point => double.IsFinite(point.Valor))
            .OrderBy(point => point.DataHora)
            .ToArray();

        if (points.Length == 0)
            return new BehaviorProfileFeatures();

        var values = points
            .Select(point => point.Valor)
            .ToArray();

        var sorted = values
            .OrderBy(value => value)
            .ToArray();

        var distinct = values
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        var median = DetectorMath.Median(sorted);

        var deviations = values
            .Select(value => Math.Abs(value - median))
            .OrderBy(value => value)
            .ToArray();

        var mad = DetectorMath.Median(deviations);
        var robustScale = mad * 1.4826;
        var range = sorted[^1] - sorted[0];

        var tolerance =
            Math.Max(
                IntegerTolerance,
                Math.Abs(range) * 1e-12);

        var levelGaps = distinct
            .Zip(
                distinct.Skip(1),
                (a, b) => b - a)
            .Where(delta => delta > tolerance)
            .OrderBy(delta => delta)
            .ToArray();

        var resolution = DetectorMath.Median(levelGaps);

        var deltas =
            new List<double>(
                Math.Max(
                    0,
                    values.Length - 1));

        var cadences =
            new List<double>(
                Math.Max(
                    0,
                    values.Length - 1));

        var dwellLengths =
            new List<int>();

        var currentDwell = 1;

        for (var index = 1;
             index < values.Length;
             index++)
        {
            var delta =
                values[index] -
                values[index - 1];

            deltas.Add(delta);

            cadences.Add(
                (points[index].DataHora -
                 points[index - 1].DataHora)
                .TotalMinutes);

            if (Math.Abs(delta) <= tolerance)
            {
                currentDwell++;
            }
            else
            {
                dwellLengths.Add(currentDwell);
                currentDwell = 1;
            }
        }

        dwellLengths.Add(currentDwell);

        var positiveCadences = cadences
            .Where(value => value > 0)
            .OrderBy(value => value)
            .ToArray();

        var medianCadence =
            DetectorMath.Median(positiveCadences);

        var criticalGapThreshold =
            medianCadence > 0
                ? medianCadence * 3
                : double.PositiveInfinity;

        var absNonZero = deltas
            .Select(Math.Abs)
            .Where(value => value > tolerance)
            .OrderBy(value => value)
            .ToArray();

        var typicalStep =
            DetectorMath.Median(absNonZero);

        var p90Step =
            Percentile(
                absNonZero,
                0.90);

        var transitionCount =
            deltas.Count;

        var nonZeroCount =
            absNonZero.Length;

        var positiveCount =
            deltas.Count(
                delta =>
                    delta > tolerance);

        var negativeCount =
            deltas.Count(
                delta =>
                    delta < -tolerance);

        var zeroCount =
            transitionCount -
            positiveCount -
            negativeCount;

        var downwardIndices = deltas
            .Select(
                (delta, index) =>
                    (delta, index))
            .Where(
                item =>
                    item.delta <
                    -tolerance)
            .Select(
                item =>
                    item.index)
            .ToArray();

        var downwardIntervals = downwardIndices
            .Zip(
                downwardIndices.Skip(1),
                (a, b) =>
                    (double)(b - a))
            .OrderBy(value => value)
            .ToArray();

        var medianDownwardInterval =
            DetectorMath.Median(
                downwardIntervals);

        var downwardIntervalCv =
            CoefficientVariation(
                downwardIntervals);

        var downwardRegularityTolerance =
            Math.Max(
                1,
                medianDownwardInterval *
                0.10);

        var downwardRegularityRatio =
            downwardIntervals.Length == 0
                ? 0
                : downwardIntervals.Count(
                    value =>
                        Math.Abs(
                            value -
                            medianDownwardInterval) <=
                        downwardRegularityTolerance) /
                  (double)downwardIntervals.Length;

        var signs = deltas
            .Where(
                delta =>
                    Math.Abs(delta) >
                    tolerance)
            .Select(
                delta =>
                    Math.Sign(delta))
            .ToArray();

        var reversals = signs
            .Zip(
                signs.Skip(1),
                (a, b) =>
                    a != b)
            .Count(value => value);

        var levelIndex = distinct
            .Select(
                (value, index) =>
                    (value, index))
            .ToDictionary(
                item => item.value,
                item => item.index);

        var adjacent = 0;

        foreach (var pair in
                 values.Zip(
                     values.Skip(1)))
        {
            if (Math.Abs(
                    pair.Second -
                    pair.First) <=
                tolerance)
            {
                continue;
            }

            if (Math.Abs(
                    levelIndex[pair.Second] -
                    levelIndex[pair.First]) <=
                1)
            {
                adjacent++;
            }
        }

        var smoothLimit =
            Math.Max(
                typicalStep * 3,
                range * 0.10);

        var abruptLimit =
            Math.Max(
                typicalStep * 5,
                range * 0.20);

        var smooth =
            absNonZero.Count(
                delta =>
                    delta <=
                    smoothLimit +
                    tolerance);

        var abrupt =
            absNonZero.Count(
                delta =>
                    delta >
                    abruptLimit +
                    tolerance);

        var resetLimit =
            Math.Max(
                typicalStep * 5,
                range * 0.20);

        var resets =
            deltas.Count(
                delta =>
                    delta <
                    -resetLimit);

        return new BehaviorProfileFeatures
        {
            SampleCount =
                values.Length,

            ObservedDurationMinutes =
                points.Length > 1
                    ? (points[^1].DataHora -
                       points[0].DataHora)
                      .TotalMinutes
                    : 0,

            MedianCadenceMinutes =
                medianCadence,

            MaximumGapMinutes =
                positiveCadences.Length == 0
                    ? 0
                    : positiveCadences[^1],

            CriticalGapCount =
                positiveCadences.Count(
                    value =>
                        value >
                        criticalGapThreshold),

            DistinctValueCount =
                distinct.Length,

            DistinctValueRatio =
                distinct.Length /
                (double)values.Length,

            IntegerLikeRatio =
                values.Count(
                    value =>
                        Math.Abs(
                            value -
                            Math.Round(value)) <=
                        IntegerTolerance) /
                (double)values.Length,

            ValueRange =
                range,

            Median =
                median,

            Mad =
                mad,

            RobustScale =
                robustScale,

            EstimatedResolution =
                resolution,

            OrderedLevelCount =
                distinct.Length,

            TypicalAbsoluteStep =
                typicalStep,

            P90AbsoluteStep =
                p90Step,

            StepToRangeRatio =
                range > tolerance
                    ? typicalStep / range
                    : 0,

            StepToRobustScaleRatio =
                robustScale > tolerance
                    ? typicalStep /
                      robustScale
                    : 0,

            ChangeRatio =
                Ratio(
                    nonZeroCount,
                    transitionCount),

            ZeroDeltaRatio =
                Ratio(
                    zeroCount,
                    transitionCount),

            PositiveDeltaRatio =
                Ratio(
                    positiveCount,
                    transitionCount),

            NegativeDeltaRatio =
                Ratio(
                    negativeCount,
                    transitionCount),

            DownwardStepCount =
                negativeCount,

            MedianDownwardStepIntervalSamples =
                medianDownwardInterval,

            DownwardStepIntervalCoefficientVariation =
                downwardIntervalCv,

            DownwardStepIntervalRegularityRatio =
                downwardRegularityRatio,

            NonDecreasingRatio =
                Ratio(
                    transitionCount -
                    negativeCount,
                    transitionCount),

            ReversalRatio =
                Ratio(
                    reversals,
                    Math.Max(
                        0,
                        signs.Length - 1)),

            AdjacentLevelTransitionRatio =
                Ratio(
                    adjacent,
                    nonZeroCount),

            SmoothTransitionRatio =
                Ratio(
                    smooth,
                    nonZeroCount),

            AbruptJumpRatio =
                Ratio(
                    abrupt,
                    nonZeroCount),

            MedianDwellSamples =
                DetectorMath.Median(
                    dwellLengths
                        .Select(
                            value =>
                                (double)value)
                        .OrderBy(
                            value =>
                                value)
                        .ToArray()),

            MaximumDwellSamples =
                dwellLengths.Count == 0
                    ? 0
                    : dwellLengths.Max(),

            ResetCount =
                resets,

            ResetRatio =
                Ratio(
                    resets,
                    transitionCount)
        };
    }

    private static double Ratio(
        int numerator,
        int denominator)
    {
        return denominator > 0
            ? numerator /
              (double)denominator
            : 0;
    }

    private static double Percentile(
        IReadOnlyList<double> sorted,
        double percentile)
    {
        if (sorted.Count == 0)
            return 0;

        var index =
            (int)Math.Ceiling(
                percentile *
                sorted.Count) - 1;

        return sorted[
            Math.Clamp(
                index,
                0,
                sorted.Count - 1)];
    }

    private static double CoefficientVariation(
        IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return 1;

        var mean =
            values.Average();

        if (mean <= 0)
            return 1;

        var variance =
            values
                .Select(
                    value =>
                        Math.Pow(
                            value - mean,
                            2))
                .Average();

        return Math.Sqrt(variance) /
               mean;
    }
}