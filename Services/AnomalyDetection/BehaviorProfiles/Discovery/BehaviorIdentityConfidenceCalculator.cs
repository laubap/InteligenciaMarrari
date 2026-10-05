namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Confiança de pertencimento de uma ocorrência ao grupo comportamental.
/// </summary>
public sealed record BehaviorOccurrenceIdentityConfidence
{
    public int SourceSegmentIndex { get; init; }

    public int EpisodeIndex { get; init; }

    public double Confidence { get; init; }
}

/// <summary>
/// Resultado explicável da confiança de identidade de um grupo.
///
/// A confiança mede quanta folga estatística ainda existe até os limites
/// usados pelo comparador. Ela não representa normalidade nem severidade.
/// </summary>
public sealed record BehaviorIdentityConfidenceResult
{
    public bool CanEvaluate { get; init; }

    public double Confidence { get; init; }

    public IReadOnlyList<BehaviorOccurrenceIdentityConfidence> Occurrences { get; init; } =
        Array.Empty<BehaviorOccurrenceIdentityConfidence>();

    public int PairCount { get; init; }

    public double WeakestPairConfidence { get; init; }

    public string Reason { get; init; } = "";
}

/// <summary>
/// Calcula confiança de identidade sem criar um score arbitrário desconectado
/// das regras de similaridade.
///
/// Para cada par de episódios:
/// - 100 significa descritores coincidentes nas dimensões bloqueantes;
/// - 0 significa que o par está exatamente no limite aceito;
/// - fora do limite torna o grupo não avaliável como uma única identidade.
///
/// O score do grupo é a confiança do elo mais fraco.
/// Isso combina com o agrupamento atual, que exige compatibilidade com
/// todos os membros e evita encadeamento transitivo.
/// </summary>
public sealed class BehaviorIdentityConfidenceCalculator
{
    private const double Epsilon = 1e-12;

    private readonly BehaviorCandidateEpisodeSimilarityOptions _options;

    private readonly BehaviorCandidateEpisodeSimilarityComparer _comparer;

    public BehaviorIdentityConfidenceCalculator(
        BehaviorCandidateEpisodeSimilarityOptions? options = null)
    {
        _options =
            options ??
            new BehaviorCandidateEpisodeSimilarityOptions();

        _comparer =
            new BehaviorCandidateEpisodeSimilarityComparer(
                _options);
    }

    public BehaviorIdentityConfidenceResult Calculate(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> occurrences)
    {
        ArgumentNullException.ThrowIfNull(
            occurrences);

        var eligible =
            occurrences
                .Where(occurrence =>
                    occurrence is not null &&
                    occurrence.IsCharacterizable &&
                    occurrence.ProfileFeatures is not null)
                .OrderBy(occurrence =>
                    occurrence.Inicio)
                .ThenBy(occurrence =>
                    occurrence.SourceSegmentIndex)
                .ThenBy(occurrence =>
                    occurrence.EpisodeIndex)
                .ToArray();

        if (eligible.Length < 2)
        {
            return new BehaviorIdentityConfidenceResult
            {
                CanEvaluate =
                    false,

                Confidence =
                    0,

                Reason =
                    "São necessárias ao menos duas ocorrências caracterizáveis para calcular confiança de identidade."
            };
        }

        var occurrenceScores =
            eligible.ToDictionary(
                occurrence =>
                    OccurrenceKey(
                        occurrence),
                _ =>
                    new List<double>());

        var pairScores =
            new List<double>();

        for (var firstIndex = 0;
             firstIndex < eligible.Length - 1;
             firstIndex++)
        {
            for (var secondIndex = firstIndex + 1;
                 secondIndex < eligible.Length;
                 secondIndex++)
            {
                var first =
                    eligible[firstIndex];

                var second =
                    eligible[secondIndex];

                var comparison =
                    _comparer.Compare(
                        first,
                        second);

                if (!comparison.CanCompare ||
                    !comparison.IsSimilar)
                {
                    return new BehaviorIdentityConfidenceResult
                    {
                        CanEvaluate =
                            false,

                        Confidence =
                            0,

                        PairCount =
                            pairScores.Count,

                        Reason =
                            "O conjunto contém ao menos um par que não satisfaz a regra de similaridade."
                    };
                }

                var pairConfidence =
                    CalculatePairConfidence(
                        comparison);

                pairScores.Add(
                    pairConfidence);

                occurrenceScores[
                    OccurrenceKey(first)]
                    .Add(
                        pairConfidence);

                occurrenceScores[
                    OccurrenceKey(second)]
                    .Add(
                        pairConfidence);
            }
        }

        var occurrenceResults =
            eligible
                .Select(occurrence =>
                {
                    var scores =
                        occurrenceScores[
                            OccurrenceKey(
                                occurrence)];

                    return new BehaviorOccurrenceIdentityConfidence
                    {
                        SourceSegmentIndex =
                            occurrence.SourceSegmentIndex,

                        EpisodeIndex =
                            occurrence.EpisodeIndex,

                        Confidence =
                            scores.Count > 0
                                ? scores.Min()
                                : 0
                    };
                })
                .ToArray();

        var weakest =
            pairScores.Min();

        return new BehaviorIdentityConfidenceResult
        {
            CanEvaluate =
                true,

            Confidence =
                weakest,

            Occurrences =
                occurrenceResults,

            PairCount =
                pairScores.Count,

            WeakestPairConfidence =
                weakest,

            Reason =
                "Confiança calculada pela folga do par mais fraco em relação aos thresholds de similaridade."
        };
    }

    private double CalculatePairConfidence(
        BehaviorCandidateEpisodeSimilarityResult comparison)
    {
        var floor =
            Math.Max(
                comparison.ComparisonFloor,
                Epsilon);

        var centerDistance =
            Clamp01(
                Math.Max(
                    comparison.MeanDelta,
                    comparison.MedianDelta) /
                floor);

        var distributionDistance =
            Clamp01(
                Math.Max(
                    comparison.FirstQuartileDelta,
                    Math.Max(
                        comparison.ThirdQuartileDelta,
                        comparison.InterquartileRangeDelta)) /
                floor);

        var scaleDistance =
            Math.Max(
                NormalizeRatioDistance(
                    comparison.StandardDeviationRatio,
                    _options.MaximumScaleRatio),
                NormalizeRatioDistance(
                    comparison.RobustScaleRatio,
                    _options.MaximumScaleRatio));

        var dynamicsDistance =
            NormalizeRatioDistance(
                comparison.TypicalAbsoluteStepRatio,
                _options.MaximumTypicalStepRatio);

        var worstDistance =
            Math.Max(
                centerDistance,
                Math.Max(
                    distributionDistance,
                    Math.Max(
                        scaleDistance,
                        dynamicsDistance)));

        return
            Math.Clamp(
                (1.0 - worstDistance) * 100.0,
                0,
                100);
    }

    private static double NormalizeRatioDistance(
        double ratio,
        double acceptedMaximum)
    {
        if (!double.IsFinite(ratio))
        {
            return 1;
        }

        ratio =
            Math.Max(
                ratio,
                1.0);

        if (acceptedMaximum <= 1.0 + Epsilon)
        {
            return
                ratio <= acceptedMaximum + Epsilon
                    ? 0
                    : 1;
        }

        return Clamp01(
            (ratio - 1.0) /
            (acceptedMaximum - 1.0));
    }

    private static double Clamp01(
        double value)
    {
        if (!double.IsFinite(value))
        {
            return 1;
        }

        return Math.Clamp(
            value,
            0,
            1);
    }

    private static (int Segment, int Episode) OccurrenceKey(
        BehaviorCandidateEpisodeCharacteristics occurrence)
    {
        return (
            occurrence.SourceSegmentIndex,
            occurrence.EpisodeIndex);
    }
}
