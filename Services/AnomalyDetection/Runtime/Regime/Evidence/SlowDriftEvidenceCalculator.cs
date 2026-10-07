using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime;

namespace IAApi.Services.AnomalyDetection.Runtime.Regime.Evidence;

/// <summary>
/// Produz evidência experimental de deriva lenta para a camada de regime.
///
/// Esta classe NÃO participa do score/voto do Ensemble Legacy.
/// O objetivo é reconhecer uma tendência gradual, sustentada e material
/// em sinal contínuo mesmo quando não existe um edge abrupto de ROC.
///
/// A evidência combina:
/// - quantidade e duração mínimas;
/// - regressão linear no trecho recente;
/// - deslocamento acumulado da tendência;
/// - deslocamento robusto entre início e fim da janela;
/// - ruído residual após remover a tendência;
/// - consistência direcional.
/// </summary>
public sealed class SlowDriftEvidenceCalculator
{
    public SlowDriftEvidenceResult Calculate(
        DetectorInput input,
        RegimeSignalKind signalKind,
        BaselineProtectionProfile protection,
        RegimeShadowOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(options);

        if (signalKind != RegimeSignalKind.Continuous)
        {
            return SlowDriftEvidenceResult.NotApplicable(
                "signal-not-continuous");
        }

        if (input.QualidadeHistorico.HasCriticalGap)
        {
            return SlowDriftEvidenceResult.NotApplicable(
                "critical-gap");
        }

        if (!double.IsFinite(input.ValorAtual) ||
            input.DataHoraAtual == default)
        {
            return SlowDriftEvidenceResult.NotApplicable(
                "invalid-current-reading");
        }

        var minimumPoints =
            Math.Max(
                3,
                options.SlowDriftMinimumPoints);

        var windowPoints =
            Math.Max(
                minimumPoints,
                options.SlowDriftWindowPoints);

        var points =
            BuildRecentWindow(
                input,
                windowPoints);

        if (points.Count < minimumPoints)
        {
            return SlowDriftEvidenceResult.NotApplicable(
                "insufficient-points",
                pointCount: points.Count);
        }

        var durationMinutes =
            (points[^1].DataHora -
             points[0].DataHora)
            .TotalMinutes;

        if (!double.IsFinite(durationMinutes) ||
            durationMinutes <
            options.SlowDriftMinimumMinutes)
        {
            return SlowDriftEvidenceResult.NotApplicable(
                "insufficient-duration",
                pointCount: points.Count,
                durationMinutes: durationMinutes);
        }

        if (!TryLinearRegression(
                points,
                out var slopePerMinute,
                out var intercept,
                out var origin))
        {
            return SlowDriftEvidenceResult.NotApplicable(
                "regression-unavailable",
                pointCount: points.Count,
                durationMinutes: durationMinutes);
        }

        var residuals =
            new double[points.Count];

        for (var index = 0;
             index < points.Count;
             index++)
        {
            var x =
                (points[index].DataHora -
                 origin)
                .TotalMinutes;

            var expected =
                intercept +
                slopePerMinute * x;

            residuals[index] =
                points[index].Valor -
                expected;
        }

        var residualMedian =
            DetectorMath.Median(
                residuals);

        var residualMad =
            DetectorMath.Mad(
                residuals,
                residualMedian);

        var residualScale =
            Math.Max(
                1.4826 * residualMad,
                Math.Max(
                    protection.Deadband,
                    protection.RelativeScaleFloor));

        if (!double.IsFinite(residualScale) ||
            residualScale <= 0)
        {
            return SlowDriftEvidenceResult.NotApplicable(
                "invalid-residual-scale",
                pointCount: points.Count,
                durationMinutes: durationMinutes,
                slopePerMinute: slopePerMinute);
        }

        var trendDisplacement =
            Math.Abs(
                slopePerMinute) *
            durationMinutes;

        var blockSize =
            Math.Max(
                3,
                points.Count / 3);

        var firstValues =
            points
                .Take(blockSize)
                .Select(point => point.Valor)
                .ToArray();

        var lastValues =
            points
                .TakeLast(blockSize)
                .Select(point => point.Valor)
                .ToArray();

        var firstMedian =
            DetectorMath.Median(
                firstValues);

        var lastMedian =
            DetectorMath.Median(
                lastValues);

        var structuralDelta =
            lastMedian -
            firstMedian;

        var structuralDisplacement =
            Math.Abs(
                structuralDelta);

        var slopeDirection =
            Math.Sign(
                slopePerMinute);

        var structuralDirection =
            Math.Sign(
                structuralDelta);

        var directionalConsistency =
            CalculateDirectionalConsistency(
                points,
                slopeDirection,
                protection,
                residualScale);

        var trendToNoiseRatio =
            trendDisplacement /
            residualScale;

        var structuralToNoiseRatio =
            structuralDisplacement /
            residualScale;

        var minimumScaleRatio =
            Math.Max(
                1.0,
                options.SlowDriftMinimumDisplacementScales);

        var minimumTrendToNoise =
            Math.Max(
                1.0,
                options.SlowDriftMinimumTrendToNoiseRatio);

        var minimumDirectionalConsistency =
            Math.Clamp(
                options.SlowDriftMinimumDirectionalConsistency,
                0.5,
                1.0);

        var directionMatches =
            slopeDirection != 0 &&
            slopeDirection ==
            structuralDirection;

        var hasEvidence =
            directionMatches &&
            trendDisplacement >=
                minimumScaleRatio * residualScale &&
            structuralDisplacement >=
                minimumScaleRatio * residualScale &&
            trendToNoiseRatio >=
                minimumTrendToNoise &&
            structuralToNoiseRatio >=
                minimumScaleRatio &&
            directionalConsistency >=
                minimumDirectionalConsistency;

        return new SlowDriftEvidenceResult(
            HasEvidence: hasEvidence,
            Applicable: true,
            Reason: hasEvidence
                ? "slow-drift-evidence"
                : "slow-drift-thresholds-not-met",
            PointCount: points.Count,
            DurationMinutes: durationMinutes,
            SlopePerMinute: slopePerMinute,
            ResidualScale: residualScale,
            TrendDisplacement: trendDisplacement,
            StructuralDisplacement: structuralDisplacement,
            TrendToNoiseRatio: trendToNoiseRatio,
            StructuralToNoiseRatio: structuralToNoiseRatio,
            DirectionalConsistency: directionalConsistency,
            FirstMedian: firstMedian,
            LastMedian: lastMedian);
    }

    private static List<HistoricoDetectorPoint>
        BuildRecentWindow(
            DetectorInput input,
            int windowPoints)
    {
        var result =
            new List<HistoricoDetectorPoint>(
                windowPoints);

        var history =
            input.Historico;

        var historicalSlots =
            Math.Max(
                0,
                windowPoints - 1);

        var start =
            Math.Max(
                0,
                history.Count -
                historicalSlots);

        for (var index = start;
             index < history.Count;
             index++)
        {
            var point =
                history[index];

            if (!double.IsFinite(point.Valor) ||
                point.DataHora == default)
            {
                continue;
            }

            result.Add(
                new HistoricoDetectorPoint
                {
                    DataHora =
                        point.DataHora,

                    Valor =
                        point.Valor
                });
        }

        if (result.Count > 0)
        {
            var last =
                result[^1];

            if (input.DataHoraAtual <
                last.DataHora)
            {
                return new List<HistoricoDetectorPoint>();
            }

            if (input.DataHoraAtual ==
                last.DataHora)
            {
                result[^1] =
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            input.DataHoraAtual,

                        Valor =
                            input.ValorAtual
                    };

                return result;
            }
        }

        result.Add(
            new HistoricoDetectorPoint
            {
                DataHora =
                    input.DataHoraAtual,

                Valor =
                    input.ValorAtual
            });

        if (result.Count >
            windowPoints)
        {
            result =
                result
                    .TakeLast(
                        windowPoints)
                    .ToList();
        }

        return result;
    }

    private static bool TryLinearRegression(
        IReadOnlyList<HistoricoDetectorPoint> points,
        out double slopePerMinute,
        out double intercept,
        out DateTime origin)
    {
        slopePerMinute = 0;
        intercept = 0;
        origin =
            points.Count > 0
                ? points[0].DataHora
                : default;

        if (points.Count < 2 ||
            origin == default)
        {
            return false;
        }

        var meanX = 0.0;
        var meanY = 0.0;

        for (var index = 0;
             index < points.Count;
             index++)
        {
            var x =
                (points[index].DataHora -
                 origin)
                .TotalMinutes;

            if (!double.IsFinite(x) ||
                !double.IsFinite(points[index].Valor))
            {
                return false;
            }

            meanX += x;
            meanY += points[index].Valor;
        }

        meanX /= points.Count;
        meanY /= points.Count;

        var numerator = 0.0;
        var denominator = 0.0;

        for (var index = 0;
             index < points.Count;
             index++)
        {
            var x =
                (points[index].DataHora -
                 origin)
                .TotalMinutes;

            var centeredX =
                x - meanX;

            numerator +=
                centeredX *
                (points[index].Valor -
                 meanY);

            denominator +=
                centeredX *
                centeredX;
        }

        if (!double.IsFinite(denominator) ||
            denominator <= 0)
        {
            return false;
        }

        slopePerMinute =
            numerator /
            denominator;

        intercept =
            meanY -
            slopePerMinute *
            meanX;

        return
            double.IsFinite(slopePerMinute) &&
            double.IsFinite(intercept);
    }

    private static double
        CalculateDirectionalConsistency(
            IReadOnlyList<HistoricoDetectorPoint> points,
            int expectedDirection,
            BaselineProtectionProfile protection,
            double residualScale)
    {
        if (points.Count < 2 ||
            expectedDirection == 0)
        {
            return 0;
        }

        var meaningfulFloor =
            Math.Max(
                protection.Deadband * 0.10,
                Math.Max(
                    residualScale * 0.05,
                    1e-12));

        var meaningfulChanges = 0;
        var matchingChanges = 0;

        for (var index = 1;
             index < points.Count;
             index++)
        {
            var delta =
                points[index].Valor -
                points[index - 1].Valor;

            if (!double.IsFinite(delta) ||
                Math.Abs(delta) <=
                meaningfulFloor)
            {
                continue;
            }

            meaningfulChanges++;

            if (Math.Sign(delta) ==
                expectedDirection)
            {
                matchingChanges++;
            }
        }

        if (meaningfulChanges == 0)
        {
            return 0;
        }

        return matchingChanges /
               (double)meaningfulChanges;
    }
}

public sealed record SlowDriftEvidenceResult(
    bool HasEvidence,
    bool Applicable,
    string Reason,
    int PointCount,
    double DurationMinutes,
    double SlopePerMinute,
    double ResidualScale,
    double TrendDisplacement,
    double StructuralDisplacement,
    double TrendToNoiseRatio,
    double StructuralToNoiseRatio,
    double DirectionalConsistency,
    double FirstMedian,
    double LastMedian)
{
    public static SlowDriftEvidenceResult NotApplicable(
        string reason,
        int pointCount = 0,
        double durationMinutes = 0,
        double slopePerMinute = 0) =>
        new(
            HasEvidence: false,
            Applicable: false,
            Reason: reason,
            PointCount: pointCount,
            DurationMinutes: durationMinutes,
            SlopePerMinute: slopePerMinute,
            ResidualScale: 0,
            TrendDisplacement: 0,
            StructuralDisplacement: 0,
            TrendToNoiseRatio: 0,
            StructuralToNoiseRatio: 0,
            DirectionalConsistency: 0,
            FirstMedian: 0,
            LastMedian: 0);
}
