namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Adaptador temporário para converter resultados antigos em resultados V2.
///
/// O adaptador existe somente durante a migração.
/// Ele não substitui a implementação V2 nativa de cada detector.
/// </summary>
public sealed class LegacyDetectorResultV2Adapter
{
    public DetectorResultV2 Convert(
        DetectorDebugItem legacyResult,
        int historyPointCount,
        int recommendedHistoryPointCount = 30)
    {
        ArgumentNullException.ThrowIfNull(legacyResult);

        var family =
            ResolveFamily(
                legacyResult.Nome);

        if (!legacyResult.Aplicavel)
        {
            return DetectorResultV2.NaoAplicavel(
                legacyResult.Nome,
                family,
                NormalizeWeight(
                    legacyResult.Peso),
                string.IsNullOrWhiteSpace(
                    legacyResult.Explicacao)
                    ? "Detector antigo não aplicável."
                    : legacyResult.Explicacao);
        }

        var evidence =
            ConvertNormalizedScoreToEvidence(
                legacyResult.ScoreNormalizado,
                legacyResult.VotouAnomalia);

        var confidence =
            CalculateHistoryConfidence(
                historyPointCount,
                recommendedHistoryPointCount);

        var metrics =
            legacyResult.Metricas ??
            new Dictionary<string, double>();

        return DetectorResultV2.AplicavelComResultado(
            detector:
                legacyResult.Nome,

            family:
                family,

            votouAnomalia:
                legacyResult.VotouAnomalia,

            evidencia:
                evidence,

            severidade:
                evidence,

            confianca:
                confidence,

            pesoBase:
                NormalizeWeight(
                    legacyResult.Peso),

            explicacao:
                legacyResult.Explicacao,

            metricas:
                metrics);
    }

    private static double ConvertNormalizedScoreToEvidence(
        double normalizedScore,
        bool votedAnomaly)
    {
        if (!double.IsFinite(normalizedScore) ||
            normalizedScore <= 0)
        {
            return 0;
        }

        /*
         * O score antigo satura em 100. Esta conversão é apenas um proxy
         * temporário para benchmark e comparação.
         */
        var ratio =
            Math.Clamp(
                normalizedScore / 100.0,
                0,
                1);

        if (votedAnomaly)
        {
            return Math.Clamp(
                0.55 + (ratio * 0.40),
                0,
                0.95);
        }

        return Math.Clamp(
            ratio * 0.49,
            0,
            0.49);
    }

    private static double CalculateHistoryConfidence(
        int historyPointCount,
        int recommendedHistoryPointCount)
    {
        var denominator =
            Math.Max(
                recommendedHistoryPointCount * 4,
                1);

        return Math.Clamp(
            historyPointCount /
            (double)denominator,
            0.25,
            1.0);
    }

    private static double NormalizeWeight(
        double weight)
    {
        if (!double.IsFinite(weight))
        {
            return 0;
        }

        return Math.Clamp(
            weight,
            0,
            1);
    }

    private static DetectorFamily ResolveFamily(
        string detectorName)
    {
        if (EqualsName(detectorName, "Z-Score") ||
            EqualsName(detectorName, "Mediana e MAD"))
        {
            return DetectorFamily.StatisticalDeviation;
        }

        if (EqualsName(detectorName, "EWMA"))
        {
            return DetectorFamily.TrendAndSmoothing;
        }

        if (EqualsName(detectorName, "Taxa de variação") ||
            EqualsName(detectorName, "Mudança de patamar"))
        {
            return DetectorFamily.Transition;
        }

        if (EqualsName(detectorName, "Persistência"))
        {
            return DetectorFamily.TemporalConfirmation;
        }

        if (detectorName.StartsWith(
                "Multivariado/",
                StringComparison.OrdinalIgnoreCase))
        {
            return DetectorFamily.Multivariate;
        }

        if (detectorName.Contains(
                "Isolation",
                StringComparison.OrdinalIgnoreCase))
        {
            return DetectorFamily.MachineLearning;
        }

        return DetectorFamily.Unknown;
    }

    private static bool EqualsName(
        string left,
        string right)
    {
        return string.Equals(
            left,
            right,
            StringComparison.OrdinalIgnoreCase);
    }
}
