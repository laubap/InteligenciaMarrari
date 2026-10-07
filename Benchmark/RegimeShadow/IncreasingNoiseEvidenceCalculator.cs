using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime;

namespace IAApi.Benchmark.RegimeShadow;

/// <summary>
/// B3.22.5 V1b - observational evidence of sustained variability expansion.
///
/// Does NOT participate in the Legacy Ensemble score/vote.
/// Does NOT change RegimePolicy decisions in V1b.
///
/// A robust variability reference is bound to ActiveRegimeId and is
/// preserved across RestoreActiveRegime. The recent robust scale is
/// compared with that reference while requiring a stable center.
/// </summary>
public sealed class IncreasingNoiseEvidenceCalculator
{
    public IncreasingNoiseEvidenceResult Calculate(
        DetectorInput input,
        RegimeSignalKind signalKind,
        BaselineProtectionProfile protection,
        RegimePolicyState policy,
        CandidateEvidenceState prior,
        RegimeShadowOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(options);

        if (signalKind != RegimeSignalKind.Continuous)
            return Preserve(prior, "signal-not-continuous");

        if (input.QualidadeHistorico.HasCriticalGap)
            return Preserve(prior, "critical-gap");

        if (!double.IsFinite(input.ValorAtual) ||
            input.DataHoraAtual == default)
            return Preserve(prior, "invalid-current-reading");

        var minimumPoints =
            Math.Max(5, options.IncreasingNoiseMinimumPoints);

        var referenceWindowPoints =
            Math.Max(
                minimumPoints,
                options.IncreasingNoiseReferenceWindowPoints);

        var recentWindowPoints =
            Math.Max(
                minimumPoints,
                options.IncreasingNoiseRecentWindowPoints);

        var sameReference =
            string.Equals(
                prior.NoiseReferenceRegimeId,
                policy.ActiveRegimeId,
                StringComparison.Ordinal) &&
            double.IsFinite(prior.NoiseReferenceCenter) &&
            double.IsFinite(prior.NoiseReferenceScale) &&
            prior.NoiseReferenceScale > 0 &&
            prior.NoiseReferencePointCount >= minimumPoints;

        string? referenceRegimeId = prior.NoiseReferenceRegimeId;
        var referenceCenter = prior.NoiseReferenceCenter;
        var referenceScale = prior.NoiseReferenceScale;
        var referenceEstablishedAt = prior.NoiseReferenceEstablishedAt;
        var referencePointCount = prior.NoiseReferencePointCount;
        var referenceDurationMinutes = prior.NoiseReferenceDurationMinutes;
        var referenceUpdated = false;

        if (!sameReference)
        {
            var reference =
                BuildHistoricalWindow(
                    input,
                    referenceWindowPoints);

            referenceUpdated = true;

            if (reference.Count < minimumPoints)
                return Cleared(
                    "insufficient-reference-points",
                    referenceUpdated,
                    reference.Count);

            referenceDurationMinutes =
                (reference[^1].DataHora -
                 reference[0].DataHora)
                .TotalMinutes;

            if (!double.IsFinite(referenceDurationMinutes) ||
                referenceDurationMinutes <
                    options.IncreasingNoiseMinimumMinutes)
            {
                return Cleared(
                    "insufficient-reference-duration",
                    referenceUpdated,
                    reference.Count,
                    referenceDurationMinutes);
            }

            var values =
                reference
                    .Select(point => point.Valor)
                    .ToArray();

            referenceCenter =
                DetectorMath.Median(values);

            var mad =
                DetectorMath.Mad(
                    values,
                    referenceCenter);

            referenceScale =
                ResolveScale(
                    1.4826 * mad,
                    protection.RelativeScaleFloor);

            if (!double.IsFinite(referenceCenter) ||
                !double.IsFinite(referenceScale) ||
                referenceScale <= 0)
            {
                return Cleared(
                    "invalid-reference-scale",
                    referenceUpdated,
                    reference.Count,
                    referenceDurationMinutes);
            }

            referenceRegimeId =
                policy.ActiveRegimeId;

            referenceEstablishedAt =
                reference[^1].DataHora;

            referencePointCount =
                reference.Count;
        }

        var recent =
            BuildRecentWindow(
                input,
                recentWindowPoints);

        if (recent.Count < minimumPoints)
        {
            return Result(
                false,
                false,
                "insufficient-recent-points",
                referenceUpdated,
                referenceRegimeId,
                referencePointCount,
                referenceDurationMinutes,
                referenceCenter,
                referenceScale,
                referenceEstablishedAt,
                recent.Count);
        }

        var recentDurationMinutes =
            (recent[^1].DataHora -
             recent[0].DataHora)
            .TotalMinutes;

        if (!double.IsFinite(recentDurationMinutes) ||
            recentDurationMinutes <
                options.IncreasingNoiseMinimumMinutes)
        {
            return Result(
                false,
                false,
                "insufficient-recent-duration",
                referenceUpdated,
                referenceRegimeId,
                referencePointCount,
                referenceDurationMinutes,
                referenceCenter,
                referenceScale,
                referenceEstablishedAt,
                recent.Count,
                recentDurationMinutes);
        }

        var recentValues =
            recent
                .Select(point => point.Valor)
                .ToArray();

        var recentCenter =
            DetectorMath.Median(recentValues);

        var recentMad =
            DetectorMath.Mad(
                recentValues,
                recentCenter);

        var recentScale =
            ResolveScale(
                1.4826 * recentMad,
                protection.RelativeScaleFloor);

        if (!double.IsFinite(recentCenter) ||
            !double.IsFinite(recentScale) ||
            recentScale <= 0)
        {
            return Result(
                false,
                false,
                "invalid-recent-scale",
                referenceUpdated,
                referenceRegimeId,
                referencePointCount,
                referenceDurationMinutes,
                referenceCenter,
                referenceScale,
                referenceEstablishedAt,
                recent.Count,
                recentDurationMinutes);
        }

        var scaleRatio =
            recentScale /
            referenceScale;

        var centerShift =
            Math.Abs(
                recentCenter -
                referenceCenter);

        var centerShiftScales =
            centerShift /
            referenceScale;

        var minimumScaleRatio =
            Math.Max(
                1.5,
                options.IncreasingNoiseMinimumScaleRatio);

        var maximumCenterShiftScales =
            Math.Max(
                0.5,
                options.IncreasingNoiseMaximumCenterShiftScales);

        var hasEvidence =
            scaleRatio >= minimumScaleRatio &&
            centerShiftScales <= maximumCenterShiftScales;

        return Result(
            hasEvidence,
            true,
            hasEvidence
                ? "increasing-noise-evidence"
                : "increasing-noise-thresholds-not-met",
            referenceUpdated,
            referenceRegimeId,
            referencePointCount,
            referenceDurationMinutes,
            referenceCenter,
            referenceScale,
            referenceEstablishedAt,
            recent.Count,
            recentDurationMinutes,
            recentCenter,
            recentScale,
            scaleRatio,
            centerShift,
            centerShiftScales);
    }

    private static List<HistoricoDetectorPoint>
        BuildHistoricalWindow(
            DetectorInput input,
            int windowPoints) =>
        input.Historico
            .Where(point =>
                double.IsFinite(point.Valor) &&
                point.DataHora != default)
            .TakeLast(windowPoints)
            .Select(point =>
                new HistoricoDetectorPoint
                {
                    DataHora = point.DataHora,
                    Valor = point.Valor
                })
            .ToList();

    private static List<HistoricoDetectorPoint>
        BuildRecentWindow(
            DetectorInput input,
            int windowPoints)
    {
        var result =
            input.Historico
                .Where(point =>
                    double.IsFinite(point.Valor) &&
                    point.DataHora != default)
                .TakeLast(
                    Math.Max(0, windowPoints - 1))
                .Select(point =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = point.DataHora,
                        Valor = point.Valor
                    })
                .ToList();

        if (result.Count > 0 &&
            input.DataHoraAtual <
                result[^1].DataHora)
            return [];

        if (result.Count > 0 &&
            input.DataHoraAtual ==
                result[^1].DataHora)
        {
            result[^1] =
                new HistoricoDetectorPoint
                {
                    DataHora = input.DataHoraAtual,
                    Valor = input.ValorAtual
                };
        }
        else
        {
            result.Add(
                new HistoricoDetectorPoint
                {
                    DataHora = input.DataHoraAtual,
                    Valor = input.ValorAtual
                });
        }

        return result
            .TakeLast(windowPoints)
            .ToList();
    }

    private static double ResolveScale(
        double rawScale,
        double relativeFloor)
    {
        var raw =
            double.IsFinite(rawScale) &&
            rawScale > 0
                ? rawScale
                : 0;

        var floor =
            double.IsFinite(relativeFloor) &&
            relativeFloor > 0
                ? relativeFloor
                : 1e-12;

        return Math.Max(raw, floor);
    }

    private static IncreasingNoiseEvidenceResult
        Preserve(
            CandidateEvidenceState prior,
            string reason) =>
        Result(
            false,
            false,
            reason,
            false,
            prior.NoiseReferenceRegimeId,
            prior.NoiseReferencePointCount,
            prior.NoiseReferenceDurationMinutes,
            prior.NoiseReferenceCenter,
            prior.NoiseReferenceScale,
            prior.NoiseReferenceEstablishedAt);

    private static IncreasingNoiseEvidenceResult
        Cleared(
            string reason,
            bool referenceUpdated,
            int referencePointCount = 0,
            double referenceDurationMinutes = 0) =>
        Result(
            false,
            false,
            reason,
            referenceUpdated,
            null,
            referencePointCount,
            referenceDurationMinutes,
            0,
            0,
            null);

    private static IncreasingNoiseEvidenceResult
        Result(
            bool hasEvidence,
            bool applicable,
            string reason,
            bool referenceUpdated,
            string? referenceRegimeId,
            int referencePointCount,
            double referenceDurationMinutes,
            double referenceCenter,
            double referenceScale,
            DateTime? referenceEstablishedAt,
            int recentPointCount = 0,
            double recentDurationMinutes = 0,
            double recentCenter = 0,
            double recentScale = 0,
            double scaleRatio = 0,
            double centerShift = 0,
            double centerShiftScales = 0) =>
        new(
            hasEvidence,
            applicable,
            reason,
            referenceUpdated,
            referenceRegimeId,
            referencePointCount,
            referenceDurationMinutes,
            referenceCenter,
            referenceScale,
            referenceEstablishedAt,
            recentPointCount,
            recentDurationMinutes,
            recentCenter,
            recentScale,
            scaleRatio,
            centerShift,
            centerShiftScales);
}

public sealed record IncreasingNoiseEvidenceResult(
    bool HasEvidence,
    bool Applicable,
    string Reason,
    bool ReferenceUpdated,
    string? ReferenceRegimeId,
    int ReferencePointCount,
    double ReferenceDurationMinutes,
    double ReferenceCenter,
    double ReferenceScale,
    DateTime? ReferenceEstablishedAt,
    int RecentPointCount,
    double RecentDurationMinutes,
    double RecentCenter,
    double RecentScale,
    double ScaleRatio,
    double CenterShift,
    double CenterShiftScales);