
namespace IAApi.Services.AnomalyDetection.Multivariate;
/// <summary>
/// B3.24.2 / MV08.2A
///
/// Consolida resultados produzidos pelo MultivariateDetector em um backtest
/// histórico. Este componente NÃO treina nem atualiza o modelo.
///
/// O modelo deve permanecer congelado durante todo o backtest.
/// </summary>
public sealed class MultivariateHistoricalBacktestAnalyzer
{
    private static readonly double[] DefaultThresholds =
        [50, 60, 70, 75, 80, 90, 95];

    public MultivariateHistoricalBacktestSummary Analisar(
        IEnumerable<MultivariateDetectionResult> results,
        int topEventsLimit = 20,
        IReadOnlyCollection<double>? thresholds = null,
        MultivariateGroupModel? model = null)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (topEventsLimit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(topEventsLimit),
                "O limite de eventos não pode ser negativo.");
        }

        var materialized =
            results.ToList();

        var applicable =
            materialized
                .Where(result =>
                    result.Aplicavel &&
                    double.IsFinite(
                        result.ScoreNormalizado) &&
                    double.IsFinite(
                        result.MahalanobisSquared))
                .ToList();

        var notApplicable =
            materialized
                .Where(result =>
                    !result.Aplicavel)
                .ToList();

        var anomalies =
            applicable
                .Where(result =>
                    result.EhAnomalia)
                .ToList();

        var normal =
            applicable.Count -
            anomalies.Count;

        var scoreValues =
            applicable
                .Select(result =>
                    result.ScoreNormalizado)
                .OrderBy(value => value)
                .ToArray();

        var mahalanobisValues =
            applicable
                .Select(result =>
                    result.MahalanobisSquared)
                .OrderBy(value => value)
                .ToArray();

        var effectiveThresholds =
            NormalizarThresholds(
                thresholds);

        // MV08.3C — classificação apenas diagnóstica dos eventos do backtest.
        // Sem modelo, mantém compatibilidade com os usos anteriores do analyzer.
        var evidenceClassifier =
            model is null
                ? null
                : new MultivariateEvidenceTypeClassifier();

        var thresholdSensitivity =
            effectiveThresholds
                .Select(threshold =>
                {
                    var count =
                        applicable.Count(result =>
                            result.ScoreNormalizado >=
                            threshold);

                    return new MultivariateBacktestThresholdResult
                    {
                        Threshold =
                            threshold,

                        EventCount =
                            count,

                        EventRate =
                            applicable.Count > 0
                                ? count /
                                  (double)applicable.Count
                                : 0
                    };
                })
                .ToArray();

        var topEvents =
            applicable
                .OrderByDescending(result =>
                    result.ScoreNormalizado)
                .ThenByDescending(result =>
                    result.MahalanobisSquared)
                .ThenBy(result =>
                    result.MinuteIndex)
                .Take(topEventsLimit)
                .Select(result =>
                {
                    var classification =
                        evidenceClassifier is not null &&
                        model is not null
                            ? evidenceClassifier
                                .Classificar(
                                    result,
                                    model)
                            : null;

                    return new MultivariateBacktestEvent
                    {
                        ClienteId =
                            result.ClienteId,

                        GroupName =
                            result.GroupName,

                        MinuteIndex =
                            result.MinuteIndex,

                        IsAnomaly =
                            result.EhAnomalia,

                        Score =
                            result.ScoreNormalizado,

                        Threshold =
                            result.LimiteScore,

                        MahalanobisSquared =
                            result.MahalanobisSquared,

                        Tags =
                            result.TagsUtilizadas
                                .ToArray(),

                        Contribuicoes =
                            result.Contribuicoes
                                .Select(item =>
                                    new MultivariateTagContribution
                                    {
                                        TagName =
                                            item.TagName,

                                        ValorAtual =
                                            item.ValorAtual,

                                        MediaTreinamento =
                                            item.MediaTreinamento,

                                        DesvioPadraoTreinamento =
                                            item.DesvioPadraoTreinamento,

                                        ZScoreAbsoluto =
                                            item.ZScoreAbsoluto
                                    })
                                .ToArray(),

                        EvidenceType =
                            classification?
                                .EvidenceType,

                        EvidenceReason =
                            classification?
                                .Reason,

                        DominantTag =
                            classification?
                                .DominantTag,

                        DominantSquaredZShare =
                            classification?
                                .DominantSquaredZShare,

                        MaximumAbsoluteCorrelation =
                            classification?
                                .MaximumAbsoluteCorrelation,

                        RelationshipFirstTag =
                            classification?
                                .RelationshipFirstTag,

                        RelationshipSecondTag =
                            classification?
                                .RelationshipSecondTag,

                        RelationshipCorrelation =
                            classification?
                                .RelationshipCorrelation,

                        RelationshipDirectionContradicted =
                            classification?
                                .RelationshipDirectionContradicted
                    };
                })
                .ToArray();

        var nonApplicableReasons =
            notApplicable
                .GroupBy(
                    result =>
                        string.IsNullOrWhiteSpace(
                            result.Explicacao)
                            ? "SemExplicacao"
                            : result.Explicacao.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    new MultivariateBacktestReasonCount
                    {
                        Reason =
                            group.Key,

                        Count =
                            group.Count()
                    })
                .OrderByDescending(item =>
                    item.Count)
                .ThenBy(item =>
                    item.Reason,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new MultivariateHistoricalBacktestSummary
        {
            TotalResults =
                materialized.Count,

            ApplicableResults =
                applicable.Count,

            NotApplicableResults =
                notApplicable.Count,

            AnomalyCount =
                anomalies.Count,

            NormalCount =
                normal,

            AnomalyRate =
                applicable.Count > 0
                    ? anomalies.Count /
                      (double)applicable.Count
                    : 0,

            ScoreDistribution =
                CriarDistribuicao(
                    scoreValues),

            MahalanobisSquaredDistribution =
                CriarDistribuicao(
                    mahalanobisValues),

            ThresholdSensitivity =
                thresholdSensitivity,

            TopEvents =
                topEvents,

            NonApplicableReasons =
                nonApplicableReasons,

            DistinctAnomalyMinutes =
                anomalies
                    .Select(result =>
                        result.MinuteIndex)
                    .Distinct()
                    .Count(),

            LongestConsecutiveAnomalyRunMinutes =
                CalcularMaiorSequenciaConsecutiva(
                    anomalies
                        .Select(result =>
                            result.MinuteIndex)
                        .Distinct()
                        .OrderBy(value => value))
        };
    }

    private static IReadOnlyList<double>
        NormalizarThresholds(
            IReadOnlyCollection<double>? thresholds)
    {
        var source =
            thresholds is null ||
            thresholds.Count == 0
                ? DefaultThresholds
                : thresholds;

        var normalized =
            source
                .Where(double.IsFinite)
                .Where(value =>
                    value > 0 &&
                    value < 100)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "Nenhum threshold válido foi informado.",
                nameof(thresholds));
        }

        return normalized;
    }

    private static MultivariateBacktestDistribution
        CriarDistribuicao(
            IReadOnlyList<double> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return new MultivariateBacktestDistribution
            {
                Count = 0
            };
        }

        return new MultivariateBacktestDistribution
        {
            Count =
                sortedValues.Count,

            Minimum =
                sortedValues[0],

            Mean =
                sortedValues.Average(),

            P50 =
                Percentile(
                    sortedValues,
                    0.50),

            P75 =
                Percentile(
                    sortedValues,
                    0.75),

            P90 =
                Percentile(
                    sortedValues,
                    0.90),

            P95 =
                Percentile(
                    sortedValues,
                    0.95),

            P99 =
                Percentile(
                    sortedValues,
                    0.99),

            Maximum =
                sortedValues[^1]
        };
    }

    /// <summary>
    /// Percentil com interpolação linear entre posições adjacentes.
    /// </summary>
    private static double Percentile(
        IReadOnlyList<double> sortedValues,
        double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var position =
            percentile *
            (sortedValues.Count - 1);

        var lowerIndex =
            (int)Math.Floor(position);

        var upperIndex =
            (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var fraction =
            position -
            lowerIndex;

        return
            sortedValues[lowerIndex] +
            (
                sortedValues[upperIndex] -
                sortedValues[lowerIndex]
            ) *
            fraction;
    }

    private static int
        CalcularMaiorSequenciaConsecutiva(
            IEnumerable<long> orderedMinutes)
    {
        var values =
            orderedMinutes.ToArray();

        if (values.Length == 0)
        {
            return 0;
        }

        var longest =
            1;

        var current =
            1;

        for (var index = 1;
             index < values.Length;
             index++)
        {
            if (values[index] ==
                values[index - 1] + 1)
            {
                current++;

                longest =
                    Math.Max(
                        longest,
                        current);
            }
            else
            {
                current =
                    1;
            }
        }

        return longest;
    }
}

public sealed class MultivariateHistoricalBacktestSummary
{
    public int TotalResults { get; init; }

    public int ApplicableResults { get; init; }

    public int NotApplicableResults { get; init; }

    public int AnomalyCount { get; init; }

    public int NormalCount { get; init; }

    public double AnomalyRate { get; init; }

    public int DistinctAnomalyMinutes { get; init; }

    public int LongestConsecutiveAnomalyRunMinutes { get; init; }

    public MultivariateBacktestDistribution ScoreDistribution { get; init; } =
        new();

    public MultivariateBacktestDistribution
        MahalanobisSquaredDistribution { get; init; } =
            new();

    public IReadOnlyList<MultivariateBacktestThresholdResult>
        ThresholdSensitivity { get; init; } =
            Array.Empty<MultivariateBacktestThresholdResult>();

    public IReadOnlyList<MultivariateBacktestEvent> TopEvents { get; init; } =
        Array.Empty<MultivariateBacktestEvent>();

    public IReadOnlyList<MultivariateBacktestReasonCount>
        NonApplicableReasons { get; init; } =
            Array.Empty<MultivariateBacktestReasonCount>();
}

public sealed class MultivariateBacktestDistribution
{
    public int Count { get; init; }

    public double Minimum { get; init; }

    public double Mean { get; init; }

    public double P50 { get; init; }

    public double P75 { get; init; }

    public double P90 { get; init; }

    public double P95 { get; init; }

    public double P99 { get; init; }

    public double Maximum { get; init; }
}

public sealed class MultivariateBacktestThresholdResult
{
    public double Threshold { get; init; }

    public int EventCount { get; init; }

    public double EventRate { get; init; }
}

public sealed class MultivariateBacktestEvent
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public long MinuteIndex { get; init; }

    public bool IsAnomaly { get; init; }

    public double Score { get; init; }

    public double Threshold { get; init; }

    public double MahalanobisSquared { get; init; }

    public IReadOnlyList<string> Tags { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<MultivariateTagContribution>
        Contribuicoes { get; init; } =
            Array.Empty<MultivariateTagContribution>();

    public string? EvidenceType { get; init; }

    public string? EvidenceReason { get; init; }

    public string? DominantTag { get; init; }

    public double? DominantSquaredZShare { get; init; }

    public double? MaximumAbsoluteCorrelation { get; init; }

    public string? RelationshipFirstTag { get; init; }

    public string? RelationshipSecondTag { get; init; }

    public double? RelationshipCorrelation { get; init; }

    public bool? RelationshipDirectionContradicted { get; init; }
}

public sealed class MultivariateBacktestReasonCount
{
    public string Reason { get; init; } =
        "";

    public int Count { get; init; }
}