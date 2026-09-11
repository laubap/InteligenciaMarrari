namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.4C
///
/// Constrói evidência legível/auditável para revisão industrial humana
/// a partir dos eventos anômalos já produzidos pelo backtest.
///
/// IMPORTANTE:
/// - não altera detecção;
/// - não altera threshold;
/// - não altera readiness;
/// - não altera ApprovedGroups;
/// - não habilita promoção;
/// - não afirma que um evento é falha real ou falso-positivo.
///
/// A participação por tag é calculada por z² / soma(z²).
/// Ela representa participação no DESVIO INDIVIDUAL padronizado e não
/// uma decomposição exata da distância de Mahalanobis quando existe
/// covariância relevante entre as variáveis.
/// </summary>
public sealed class MultivariateIndustrialValidationEvidenceBuilder
{
    public MultivariateIndustrialValidationEvidence Build(
        MultivariateGroupModel model,
        MultivariateHistoricalBacktestSummary referenceSummary,
        MultivariateHistoricalBacktestSummary holdoutSummary,
        double threshold)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(referenceSummary);
        ArgumentNullException.ThrowIfNull(holdoutSummary);

        if (!double.IsFinite(threshold) ||
            threshold <= 0 ||
            threshold >= 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                "O threshold deve estar no intervalo (0, 100).");
        }

        var referenceEvents =
            CriarEventos(
                model,
                referenceSummary.TopEvents,
                MultivariateIndustrialEvidenceWindows.Reference,
                threshold);

        var holdoutEvents =
            CriarEventos(
                model,
                holdoutSummary.TopEvents,
                MultivariateIndustrialEvidenceWindows.Holdout,
                threshold);

        var events =
            referenceEvents
                .Concat(holdoutEvents)
                .OrderBy(item =>
                    item.EventDateTime ??
                    DateTime.MaxValue)
                .ThenBy(item =>
                    item.MinuteIndex)
                .ToArray();

        var totalObservedAnomalies =
            Math.Max(
                0,
                referenceSummary.AnomalyCount) +
            Math.Max(
                0,
                holdoutSummary.AnomalyCount);

        return new MultivariateIndustrialValidationEvidence
        {
            ClienteId =
                model.ClienteId?.Trim() ??
                "",

            GroupName =
                model.GroupName?.Trim() ??
                "",

            ModelVersion =
                model.Version,

            ModelAlgorithm =
                model.Algorithm ??
                "",

            ModelTrainedAtUtc =
                model.TrainedAtUtc,

            Threshold =
                threshold,

            ReferenceAnomalyCount =
                Math.Max(
                    0,
                    referenceSummary.AnomalyCount),

            HoldoutAnomalyCount =
                Math.Max(
                    0,
                    holdoutSummary.AnomalyCount),

            EvidenceEventCount =
                events.Length,

            EvidenceIsCompleteForObservedAnomalies =
                events.Length ==
                totalObservedAnomalies,

            EventTimeBasis =
                "MinuteIndex reconstruído a partir de DateTime.Ticks; fuso/Kind não é inferido.",

            ContributionBasis =
                "z² / soma(z²): participação no desvio individual padronizado; não é decomposição exata de Mahalanobis.",

            Events =
                events
        };
    }

    private static IReadOnlyList<
        MultivariateIndustrialEvidenceEvent>
        CriarEventos(
            MultivariateGroupModel model,
            IReadOnlyList<MultivariateBacktestEvent> topEvents,
            string sourceWindow,
            double threshold)
    {
        if (topEvents is null ||
            topEvents.Count == 0)
        {
            return
                Array.Empty<
                    MultivariateIndustrialEvidenceEvent>();
        }

        return topEvents
            .Where(item =>
                item is not null &&
                item.IsAnomaly)
            .Select(item =>
                CriarEvento(
                    model,
                    item,
                    sourceWindow,
                    threshold))
            .ToArray();
    }

    private static MultivariateIndustrialEvidenceEvent
        CriarEvento(
            MultivariateGroupModel model,
            MultivariateBacktestEvent source,
            string sourceWindow,
            double threshold)
    {
        var contributions =
            CriarContribuicoes(
                source.Contribuicoes);

        return new MultivariateIndustrialEvidenceEvent
        {
            SourceWindow =
                sourceWindow,

            ClienteId =
                source.ClienteId,

            GroupName =
                source.GroupName,

            MinuteIndex =
                source.MinuteIndex,

            EventDateTime =
                TryReconstructMinuteDateTime(
                    source.MinuteIndex),

            IsAnomaly =
                source.IsAnomaly,

            Score =
                source.Score,

            Threshold =
                threshold,

            MahalanobisSquared =
                source.MahalanobisSquared,

            EvidenceType =
                source.EvidenceType,

            EvidenceReason =
                source.EvidenceReason,

            DominantTag =
                source.DominantTag,

            DominantSquaredZShare =
                source.DominantSquaredZShare,

            MaximumAbsoluteCorrelation =
                source.MaximumAbsoluteCorrelation,

            RelationshipFirstTag =
                source.RelationshipFirstTag,

            RelationshipSecondTag =
                source.RelationshipSecondTag,

            RelationshipCorrelation =
                source.RelationshipCorrelation,

            RelationshipDirectionContradicted =
                source.RelationshipDirectionContradicted,

            ModelVersion =
                model.Version,

            ModelAlgorithm =
                model.Algorithm ??
                "",

            ModelTrainedAtUtc =
                model.TrainedAtUtc,

            Tags =
                source.Tags
                    .ToArray(),

            Contributions =
                contributions
        };
    }

    private static IReadOnlyList<
        MultivariateIndustrialEvidenceTag>
        CriarContribuicoes(
            IReadOnlyList<MultivariateTagContribution> source)
    {
        if (source is null ||
            source.Count == 0)
        {
            return
                Array.Empty<
                    MultivariateIndustrialEvidenceTag>();
        }

        var usable =
            source
                .Where(item =>
                    item is not null &&
                    !string.IsNullOrWhiteSpace(
                        item.TagName) &&
                    double.IsFinite(
                        item.ZScoreAbsoluto))
                .ToArray();

        var totalSquaredZ =
            usable
                .Sum(item =>
                    item.ZScoreAbsoluto *
                    item.ZScoreAbsoluto);

        var hasValidEnergy =
            double.IsFinite(
                totalSquaredZ) &&
            totalSquaredZ > 0;

        return usable
            .Select(item =>
            {
                var squaredZ =
                    item.ZScoreAbsoluto *
                    item.ZScoreAbsoluto;

                return new MultivariateIndustrialEvidenceTag
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
                        item.ZScoreAbsoluto,

                    IndividualDeviationShare =
                        hasValidEnergy
                            ? squaredZ /
                              totalSquaredZ
                            : null
                };
            })
            .OrderByDescending(item =>
                item.IndividualDeviationShare ??
                0)
            .ThenBy(item =>
                item.TagName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DateTime?
        TryReconstructMinuteDateTime(
            long minuteIndex)
    {
        if (minuteIndex < 0)
        {
            return null;
        }

        try
        {
            var ticks =
                checked(
                    minuteIndex *
                    TimeSpan.TicksPerMinute);

            if (ticks < DateTime.MinValue.Ticks ||
                ticks > DateTime.MaxValue.Ticks)
            {
                return null;
            }

            /*
             * O MinuteIndex original foi criado usando DataHora.Ticks.
             * Ticks não preserva informação de fuso/Kind.
             * Por isso NÃO rotulamos esse horário como UTC.
             */
            return new DateTime(
                ticks,
                DateTimeKind.Unspecified);
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}

public static class MultivariateIndustrialEvidenceWindows
{
    public const string Reference =
        "Reference";

    public const string Holdout =
        "Holdout";
}

public sealed class MultivariateIndustrialValidationEvidence
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public int ModelVersion { get; init; }

    public string ModelAlgorithm { get; init; } =
        "";

    public DateTime ModelTrainedAtUtc { get; init; }

    public double Threshold { get; init; }

    public int ReferenceAnomalyCount { get; init; }

    public int HoldoutAnomalyCount { get; init; }

    public int EvidenceEventCount { get; init; }

    public bool EvidenceIsCompleteForObservedAnomalies { get; init; }

    public string EventTimeBasis { get; init; } =
        "";

    public string ContributionBasis { get; init; } =
        "";

    public IReadOnlyList<
        MultivariateIndustrialEvidenceEvent>
        Events { get; init; } =
            Array.Empty<
                MultivariateIndustrialEvidenceEvent>();
}

public sealed class MultivariateIndustrialEvidenceEvent
{
    public string SourceWindow { get; init; } =
        "";

    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public long MinuteIndex { get; init; }

    public DateTime? EventDateTime { get; init; }

    public bool IsAnomaly { get; init; }

    public double Score { get; init; }

    public double Threshold { get; init; }

    public double MahalanobisSquared { get; init; }

    public string? EvidenceType { get; init; }

    public string? EvidenceReason { get; init; }

    public string? DominantTag { get; init; }

    public double? DominantSquaredZShare { get; init; }

    public double? MaximumAbsoluteCorrelation { get; init; }

    public string? RelationshipFirstTag { get; init; }

    public string? RelationshipSecondTag { get; init; }

    public double? RelationshipCorrelation { get; init; }

    public bool? RelationshipDirectionContradicted { get; init; }

    public int ModelVersion { get; init; }

    public string ModelAlgorithm { get; init; } =
        "";

    public DateTime ModelTrainedAtUtc { get; init; }

    public IReadOnlyList<string> Tags { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<
        MultivariateIndustrialEvidenceTag>
        Contributions { get; init; } =
            Array.Empty<
                MultivariateIndustrialEvidenceTag>();
}

public sealed class MultivariateIndustrialEvidenceTag
{
    public string TagName { get; init; } =
        "";

    public double ValorAtual { get; init; }

    public double MediaTreinamento { get; init; }

    public double DesvioPadraoTreinamento { get; init; }

    public double ZScoreAbsoluto { get; init; }

    public double? IndividualDeviationShare { get; init; }
}