namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Constrói evidências válidas e realiza a conversão entre
/// DetectorResultV2 e Evidence.
///
/// Esta classe centraliza normalização e invariantes para evitar
/// implementações diferentes em cada detector.
/// </summary>
public sealed class EvidenceBuilder
{
    public Evidence FromDetectorResult(
        DetectorResultV2 result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Aplicavel)
        {
            return NotApplicable(
                detector:
                    result.Detector,

                family:
                    result.Family,

                baseWeight:
                    result.PesoBase,

                reason:
                    result.MotivoNaoAplicavel ??
                    result.Explicacao);
        }

        return Applicable(
            detector:
                result.Detector,

            family:
                result.Family,

            positiveVote:
                result.VotouAnomalia,

            strength:
                result.Evidencia,

            confidence:
                result.Confianca,

            severity:
                result.Severidade,

            baseWeight:
                result.PesoBase,

            explanation:
                result.Explicacao,

            limits:
                result.Limites,

            metrics:
                result.Metricas);
    }

    public EvidenceCollection FromDetectorResults(
        IEnumerable<DetectorResultV2> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return new EvidenceCollection(
            results.Select(
                FromDetectorResult));
    }

    public Evidence Applicable(
        string detector,
        DetectorFamily family,
        bool positiveVote,
        double strength,
        double confidence,
        double severity,
        double baseWeight,
        string explanation,
        LimitesDetector? limits = null,
        IReadOnlyDictionary<string, double>? metrics = null)
    {
        if (string.IsNullOrWhiteSpace(detector))
        {
            throw new ArgumentException(
                "O nome do detector é obrigatório.",
                nameof(detector));
        }

        if (family == DetectorFamily.Unknown)
        {
            throw new ArgumentException(
                "A família da evidência é obrigatória.",
                nameof(family));
        }

        return new Evidence
        {
            Detector =
                detector.Trim(),

            Family =
                family,

            Applicable =
                true,

            PositiveVote =
                positiveVote,

            Strength =
                Normalize01(strength),

            Confidence =
                Normalize01(confidence),

            Severity =
                Normalize01(severity),

            BaseWeight =
                Normalize01(baseWeight),

            Explanation =
                explanation ?? "",

            NotApplicableReason =
                null,

            Limits =
                limits,

            Metrics =
                metrics ??
                new Dictionary<string, double>()
        };
    }

    public Evidence NotApplicable(
        string detector,
        DetectorFamily family,
        double baseWeight,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(detector))
        {
            throw new ArgumentException(
                "O nome do detector é obrigatório.",
                nameof(detector));
        }

        if (family == DetectorFamily.Unknown)
        {
            throw new ArgumentException(
                "A família da evidência é obrigatória.",
                nameof(family));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "O motivo de não aplicabilidade é obrigatório.",
                nameof(reason));
        }

        return new Evidence
        {
            Detector =
                detector.Trim(),

            Family =
                family,

            Applicable =
                false,

            PositiveVote =
                false,

            Strength =
                0,

            Confidence =
                0,

            Severity =
                0,

            BaseWeight =
                Normalize01(baseWeight),

            Explanation =
                reason,

            NotApplicableReason =
                reason
        };
    }

    private static double Normalize01(
        double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Clamp(
            value,
            0,
            1);
    }
}
