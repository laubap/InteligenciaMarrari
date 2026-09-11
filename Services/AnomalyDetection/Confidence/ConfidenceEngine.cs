using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Interpreta evidências padronizadas e classifica o grau de sustentação
/// de um evento.
///
/// Fluxo principal:
/// DetectorResultV2
///     -> EvidenceBuilder
///     -> EvidenceCollection
///     -> ConfidenceEngine
///     -> ConfidenceResult
///
/// O método que recebe DetectorResultV2 foi mantido para compatibilidade.
/// </summary>
public sealed class ConfidenceEngine
{
    private readonly ConfidenceEngineOptions _options;
    private readonly EvidenceBuilder _evidenceBuilder;

    public ConfidenceEngine(
        IOptions<ConfidenceEngineOptions> options,
        EvidenceBuilder evidenceBuilder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(evidenceBuilder);

        _options = options.Value;
        _options.Validate();

        _evidenceBuilder = evidenceBuilder;
    }

    /// <summary>
    /// Entrada de compatibilidade.
    ///
    /// Converte os resultados V2 para EvidenceCollection e delega
    /// toda a decisão ao método principal.
    /// </summary>
    public ConfidenceResult Evaluate(
        IEnumerable<DetectorResultV2> detectorResults)
    {
        ArgumentNullException.ThrowIfNull(detectorResults);

        var evidences =
            _evidenceBuilder.FromDetectorResults(
                detectorResults);

        return Evaluate(
            evidences);
    }

    /// <summary>
    /// Entrada principal do Confidence Engine.
    ///
    /// O motor não conhece fórmulas específicas de detector.
    /// Ele interpreta somente evidências padronizadas.
    /// </summary>
    public ConfidenceResult Evaluate(
        EvidenceCollection evidences)
    {
        ArgumentNullException.ThrowIfNull(evidences);

        var validationErrors =
            evidences.Validate();

        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "A coleção de evidências é inválida: " +
                string.Join(
                    " | ",
                    validationErrors));
        }

        var applicable =
            evidences.Applicable;

        if (applicable.Count == 0)
        {
            return new ConfidenceResult
            {
                Level =
                    ConfidenceLevel.NotApplicable,

                Score =
                    0,

                ApplicableDetectorCount =
                    0,

                PositiveVoteCount =
                    0,

                PositiveFamilyCount =
                    0,

                Explanation =
                    "Nenhuma evidência aplicável foi produzida."
            };
        }

        var positive =
            evidences.Positive;

        if (positive.Count == 0)
        {
            return new ConfidenceResult
            {
                Level =
                    ConfidenceLevel.Normal,

                Score =
                    0,

                ApplicableDetectorCount =
                    applicable.Count,

                PositiveVoteCount =
                    0,

                PositiveFamilyCount =
                    0,

                Explanation =
                    "Nenhuma evidência aplicável apresentou voto positivo.",

                ApplicableResults =
                    Array.Empty<DetectorResultV2>()
            };
        }

        var familyResults =
            positive
                .GroupBy(item => item.Family)
                .Where(
                    group =>
                        group.Key !=
                        DetectorFamily.Unknown)
                .Select(
                    group =>
                        BuildFamilyResult(
                            group.Key,
                            group.ToList()))
                .OrderByDescending(
                    item => item.Score)
                .ToList();

        var positiveFamilyCount =
            familyResults.Count;

        var familyDiversityScore =
            CalculateFamilyDiversityScore(
                positiveFamilyCount);

        var evidenceScore =
            familyResults.Count > 0
                ? familyResults.Average(
                    item => item.Score)
                : 0;

        var detectorConfidenceScore =
            positive.Count > 0
                ? positive.Average(
                    item => item.Confidence)
                : 0;

        var hasTemporalConfirmation =
            evidences.HasTemporalConfirmation;

        var hasMultivariateEvidence =
            evidences.HasMultivariateEvidence;

        var temporalScore =
            hasTemporalConfirmation
                ? 1.0
                : 0.0;

        var rawScore =
            (familyDiversityScore *
             _options.FamilyDiversityWeight) +
            (evidenceScore *
             _options.EvidenceWeight) +
            (detectorConfidenceScore *
             _options.DetectorConfidenceWeight) +
            (temporalScore *
             _options.TemporalConfirmationWeight);

        var cappedScore =
            ApplyAuthorityCaps(
                rawScore,
                positiveFamilyCount);

        var level =
            ResolveLevel(
                cappedScore,
                positiveFamilyCount,
                hasTemporalConfirmation);

        var reasons =
            BuildReasons(
                level,
                positive,
                familyResults,
                cappedScore,
                hasTemporalConfirmation,
                hasMultivariateEvidence);

        return new ConfidenceResult
        {
            Level =
                level,

            Score =
                cappedScore,

            ApplicableDetectorCount =
                applicable.Count,

            PositiveVoteCount =
                positive.Count,

            PositiveFamilyCount =
                positiveFamilyCount,

            HasTemporalConfirmation =
                hasTemporalConfirmation,

            HasMultivariateEvidence =
                hasMultivariateEvidence,

            Explanation =
                BuildExplanation(
                    level,
                    cappedScore,
                    positiveFamilyCount),

            Reasons =
                reasons,

            Families =
                familyResults,

            /*
             * O contrato ConfidenceResult ainda mantém esta propriedade
             * por compatibilidade. Nesta entrada baseada em Evidence,
             * ela não é preenchida.
             */
            ApplicableResults =
                Array.Empty<DetectorResultV2>()
        };
    }

    private static ConfidenceFamilyResult BuildFamilyResult(
        DetectorFamily family,
        IReadOnlyCollection<Evidence> evidences)
    {
        var effectiveStrengthValues =
            evidences
                .Select(
                    item =>
                        Clamp01(
                            item.EffectiveStrength))
                .ToList();

        var averageEvidence =
            evidences.Average(
                item => item.Strength);

        var maximumEvidence =
            evidences.Max(
                item => item.Strength);

        var averageConfidence =
            evidences.Average(
                item => item.Confidence);

        /*
         * O maior resultado domina, mas a média interna oferece reforço
         * limitado. Detectores correlacionados da mesma família não são
         * tratados como evidências totalmente independentes.
         */
        var familyScore =
            Clamp01(
                (effectiveStrengthValues.Max() * 0.70) +
                (effectiveStrengthValues.Average() * 0.30));

        return new ConfidenceFamilyResult
        {
            Family =
                family,

            Score =
                familyScore,

            AverageEvidence =
                averageEvidence,

            MaximumEvidence =
                maximumEvidence,

            AverageConfidence =
                averageConfidence,

            ApplicableDetectorCount =
                evidences.Count,

            PositiveVoteCount =
                evidences.Count(
                    item => item.PositiveVote),

            Detectors =
                evidences
                    .Select(item => item.Detector)
                    .OrderBy(
                        item => item,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList()
        };
    }

    private static double CalculateFamilyDiversityScore(
        int positiveFamilyCount)
    {
        return positiveFamilyCount switch
        {
            <= 0 => 0,
            1 => 0.22,
            2 => 0.56,
            3 => 0.82,
            4 => 0.95,
            _ => 1.00
        };
    }

    private double ApplyAuthorityCaps(
        double rawScore,
        int positiveFamilyCount)
    {
        if (positiveFamilyCount <= 1)
        {
            return Math.Min(
                Clamp01(rawScore),
                _options.SingleFamilyScoreCap);
        }

        if (positiveFamilyCount == 2)
        {
            return Math.Min(
                Clamp01(rawScore),
                _options.TwoFamilyScoreCap);
        }

        return Clamp01(rawScore);
    }

    private ConfidenceLevel ResolveLevel(
        double score,
        int positiveFamilyCount,
        bool hasTemporalConfirmation)
    {
        if (positiveFamilyCount <= 0)
        {
            return ConfidenceLevel.Normal;
        }

        if (positiveFamilyCount <
            _options.MinimumFamiliesForProbable)
        {
            return ConfidenceLevel.Suspicious;
        }

        var canConfirmByFamilyCount =
            positiveFamilyCount >=
            _options.MinimumFamiliesForConfirmed;

        var canConfirmWithTwoFamilies =
            positiveFamilyCount == 2 &&
            (!_options.RequireTemporalFamilyForTwoFamilyConfirmation ||
             hasTemporalConfirmation);

        if (score >=
                _options.ConfirmedThreshold &&
            (canConfirmByFamilyCount ||
             canConfirmWithTwoFamilies))
        {
            return ConfidenceLevel.Confirmed;
        }

        if (score >=
            _options.ProbableThreshold)
        {
            return ConfidenceLevel.Probable;
        }

        return ConfidenceLevel.Suspicious;
    }

    private static IReadOnlyList<string> BuildReasons(
        ConfidenceLevel level,
        IReadOnlyCollection<Evidence> positiveEvidences,
        IReadOnlyCollection<ConfidenceFamilyResult> families,
        double score,
        bool hasTemporalConfirmation,
        bool hasMultivariateEvidence)
    {
        var reasons =
            new List<string>
            {
                $"{positiveEvidences.Count} evidência(s) positiva(s).",
                $"{families.Count} família(s) independente(s) participaram.",
                $"Score consolidado: {score:F3}."
            };

        if (families.Count == 1)
        {
            reasons.Add(
                "A evidência está concentrada em uma única família.");
        }

        if (hasTemporalConfirmation)
        {
            reasons.Add(
                "Existe confirmação temporal.");
        }

        if (hasMultivariateEvidence)
        {
            reasons.Add(
                "Existe evidência multivariada.");
        }

        if (level == ConfidenceLevel.Suspicious)
        {
            reasons.Add(
                "O evento deve permanecer como suspeita até receber " +
                "confirmação adicional.");
        }

        return reasons;
    }

    private static string BuildExplanation(
        ConfidenceLevel level,
        double score,
        int positiveFamilyCount)
    {
        return level switch
        {
            ConfidenceLevel.NotApplicable =>
                "Não foi possível avaliar a confiança.",

            ConfidenceLevel.Normal =>
                "O conjunto de evidências não apresentou comportamento anômalo.",

            ConfidenceLevel.Suspicious =>
                $"O evento possui evidência limitada ({score:F3}) e " +
                $"{positiveFamilyCount} família(s) positiva(s).",

            ConfidenceLevel.Probable =>
                $"O evento possui evidências independentes suficientes " +
                $"para ser considerado provável ({score:F3}).",

            ConfidenceLevel.Confirmed =>
                $"O evento foi sustentado por múltiplas famílias " +
                $"independentes ({score:F3}).",

            _ =>
                "Nível de confiança desconhecido."
        };
    }

    private static double Clamp01(
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
