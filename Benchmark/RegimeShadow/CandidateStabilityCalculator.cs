using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.RegimeShadow;

public sealed class CandidateStabilityCalculator
{
    public CandidateStability Calculate(CandidateEvidenceState state, double cadence, RegimeShadowOptions options)
    {
        if (state.Points.Count < 2 || cadence <= 0)
            return CandidateStability.Insufficient;
        var values = state.Points.Select(x => x.Value).ToArray();
        var median = DetectorMath.Median(values);
        var mad = DetectorMath.Mad(values, median);
        var robustScale = Math.Max(1.4826 * mad, state.Deadband);
        var duration = (state.Points[^1].Timestamp - state.Points[0].Timestamp).TotalMinutes;
        var independent = Math.Min(state.Points.Count, 1 + (int)Math.Floor(duration / cadence));
        var displacement = Math.Abs(median - state.ActiveCenter);
        var dispersionRatio = displacement > 0 ? robustScale / displacement : double.PositiveInfinity;
        var slope = Slope(state.Points);
        var slopeRatio = displacement > 0 ? Math.Abs(slope) * Math.Max(duration, cadence) / displacement : double.PositiveInfinity;
        var dispersionOk = dispersionRatio <= options.MaximumDispersionToDisplacementRatio;
        var slopeOk = slopeRatio <= options.MaximumResidualSlopeDriftRatio;
        var durationOk = duration >= options.StabilityMinimumMinutes;
        var samplesOk = independent >= options.StabilityMinimumIndependentSamples;
        var confidence = new[] {
            Ratio(options.MaximumDispersionToDisplacementRatio, dispersionRatio),
            Ratio(options.MaximumResidualSlopeDriftRatio, slopeRatio),
            Math.Clamp(duration / options.StabilityMinimumMinutes, 0, 1),
            Math.Clamp(independent / (double)options.StabilityMinimumIndependentSamples, 0, 1)
        }.Min();
        var status = dispersionOk && slopeOk && durationOk && samplesOk
            ? CandidateStabilityStatus.Stable
            : dispersionOk && slopeOk ? CandidateStabilityStatus.Stabilizing : CandidateStabilityStatus.Unstable;

        // B3.18 â€” diagnÃ³stico temporÃ¡rio de estabilidade candidata.
        // NÃ£o altera nenhum cÃ¡lculo nem decisÃ£o.
        return new(status, confidence, independent, duration, 0, dispersionOk, slopeOk, true, false,
            [$"candidateMedian={median:R}", $"candidateMad={mad:R}", $"candidateRobustScale={robustScale:R}", $"dispersionRatio={dispersionRatio:R}", $"residualSlopePerMinute={slope:R}", $"slopeDriftRatio={slopeRatio:R}"]);
    }

    private static double Ratio(double limit, double value) => !double.IsFinite(value) || value <= 0 ? (value == 0 ? 1 : 0) : Math.Clamp(limit / value, 0, 1);
    private static double Slope(IReadOnlyList<CandidatePoint> points)
    {
        var origin = points[0].Timestamp; var meanX = points.Average(p => (p.Timestamp - origin).TotalMinutes); var meanY = points.Average(p => p.Value);
        var numerator = 0d; var denominator = 0d;
        foreach (var p in points) { var x = (p.Timestamp - origin).TotalMinutes - meanX; numerator += x * (p.Value - meanY); denominator += x * x; }
        return denominator > 0 ? numerator / denominator : 0;
    }
}
