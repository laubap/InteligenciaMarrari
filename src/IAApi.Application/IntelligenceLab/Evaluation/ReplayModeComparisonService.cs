using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// M4.1C: comparacao offline de duas execucoes ja realizadas. O gabarito
/// continua independente das decisoes, e o avaliador 3B.2 e reutilizado.
/// A primeira versao atende a cenarios de uma tag e um evento rotulado.
/// </summary>
public static class ReplayModeComparisonService
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static ReplayModeComparisonResult Compare(
        string exportSixJson, string labelsSixJson,
        string exportSevenJson, string labelsSevenJson)
    {
        // Sempre reavaliar com o contrato e as regras oficiais. Nao confiar em
        // numeros precomputados ou em um JSON de relatorio fornecido por fora.
        var sixReport = ReplayLabeledEvaluationRunner.Evaluate(exportSixJson, labelsSixJson);
        var sevenReport = ReplayLabeledEvaluationRunner.Evaluate(exportSevenJson, labelsSevenJson);

        var six = Read<UnlabeledReplayReadingsExport>(exportSixJson);
        var seven = Read<UnlabeledReplayReadingsExport>(exportSevenJson);
        var sixLabels = Read<ReplayIndependentLabels>(labelsSixJson);
        var sevenLabels = Read<ReplayIndependentLabels>(labelsSevenJson);

        if (six.ScenarioId != seven.ScenarioId || six.TestClientId != seven.TestClientId ||
            six.Readings.Count != seven.Readings.Count ||
            sixLabels.AlertEpisodeCadenceSeconds != sevenLabels.AlertEpisodeCadenceSeconds ||
            sixLabels.MinimumEventCoverageReadings != sevenLabels.MinimumEventCoverageReadings ||
            JsonSerializer.Serialize(sixLabels.Events, Options) != JsonSerializer.Serialize(sevenLabels.Events, Options) ||
            JsonSerializer.Serialize(sixLabels.NormalIntervals, Options) != JsonSerializer.Serialize(sevenLabels.NormalIntervals, Options))
        {
            throw new ArgumentException("Os modos devem usar mesmo cenario, cliente, leituras, eventos e normalidade explicita.");
        }

        var labeledEvent = sixLabels.Events.Count == 1 ? sixLabels.Events[0]
            : throw new ArgumentException("A comparacao M4.1C suporta exatamente um evento rotulado.");
        if (labeledEvent.Tags.Count != 1)
            throw new ArgumentException("A comparacao M4.1C requer evento de uma unica tag.");
        var tag = labeledEvent.Tags[0];
        var rows = new List<ReplayComparisonReading>(six.Readings.Count);
        for (var i = 0; i < six.Readings.Count; i++)
        {
            var a = six.Readings[i];
            var b = seven.Readings[i];
            if (a.TimestampUtc != b.TimestampUtc || a.TagName != b.TagName ||
                a.TagName != tag)
                throw new ArgumentException($"Leituras dos dois modos nao estao alinhadas no indice {i}.");
            if (a.EnsembleNormalizedScore is not double scoreA ||
                b.EnsembleNormalizedScore is not double scoreB)
                throw new ArgumentException($"Score do Ensemble ausente no indice {i}.");

            var during = a.TimestampUtc >= labeledEvent.StartedAtUtc &&
                         a.TimestampUtc < labeledEvent.EndedAtUtc;
            var explicitlyNormal = sixLabels.NormalIntervals.Any(n =>
                n.Tags.Contains(tag, StringComparer.Ordinal) &&
                a.TimestampUtc >= n.StartsAtUtc && a.TimestampUtc < n.EndsAtUtc);
            var phase = during ? "During" : explicitlyNormal
                ? a.TimestampUtc < labeledEvent.StartedAtUtc ? "Before" : "After"
                : "Unlabeled";
            rows.Add(new ReplayComparisonReading(i, a.TimestampUtc, tag, phase,
                scoreA, scoreB, scoreB - scoreA, a.FinalAlert, b.FinalAlert));
        }

        var phases = new[] { "Before", "During", "After", "Unlabeled" }
            .Select(phase =>
            {
                var subset = rows.Where(x => x.Phase == phase).ToArray();
                return new ReplayComparisonPhase(
                    phase, subset.Length,
                    subset.Length == 0 ? null : subset.Average(x => x.ScoreSix),
                    subset.Length == 0 ? null : subset.Average(x => x.ScoreSeven),
                    subset.Length == 0 ? null : subset.Average(x => x.Difference),
                    subset.Length == 0 ? null : subset.Max(x => x.ScoreSix),
                    subset.Length == 0 ? null : subset.Max(x => x.ScoreSeven),
                    subset.Count(x => x.AlertSix != x.AlertSeven));
            }).ToArray();

        return new ReplayModeComparisonResult(
            "intelligence-lab-replay-mode-comparison/v1",
            six.ScenarioId, six.TestClientId, tag,
            six.RunId, seven.RunId,
            "UNVERIFIED", // Nao fingir que o avaliador 3B.2 verificou codigo/modelos.
            rows.Count, rows.Count(x => x.AlertSix != x.AlertSeven),
            Classify(sixReport, rows, modeSix: true),
            Classify(sevenReport, rows, modeSix: false),
            phases, rows);
    }

    private static ReplayClassificationSummary Classify(
        IndustrialEvaluationReport report, IReadOnlyList<ReplayComparisonReading> rows, bool modeSix)
    {
        bool Alert(ReplayComparisonReading row) => modeSix ? row.AlertSix : row.AlertSeven;
        var tp = rows.Count(x => x.Phase == "During" && Alert(x));
        var fn = rows.Count(x => x.Phase == "During" && !Alert(x));
        var fp = rows.Count(x => (x.Phase == "Before" || x.Phase == "After") && Alert(x));
        var tn = rows.Count(x => (x.Phase == "Before" || x.Phase == "After") && !Alert(x));
        if (fp != report.ReadingMetrics.FinalAlertsInExplicitNormalIntervals ||
            tp + fp + rows.Count(x => x.Phase == "Unlabeled" && Alert(x)) !=
                report.ReadingMetrics.FinalAlertReadings)
            throw new InvalidOperationException("Contagens por leitura divergiram do avaliador oficial.");
        var eventResult = report.Events.Single();
        var hasExplicitNegativeReadings = fp + tn > 0;
        double? scorableReadingPrecision = hasExplicitNegativeReadings && tp + fp > 0
            ? tp / (double)(tp + fp)
            : null;
        double? readingRecall = tp + fn > 0 ? tp / (double)(tp + fn) : null;
        return new ReplayClassificationSummary(tp, fn, fp, tn,
            rows.Count(x => x.Phase == "Unlabeled"),
            report.ReadingMetrics.FinalAlertReadings,
            eventResult.Classification.ToString(), eventResult.DetectionDelay?.TotalMinutes,
            report.EventMetrics.StrictRecall,
            report.EpisodeMetrics.FalsePositiveEpisodes)
        {
            ScorableReadingPrecision = scorableReadingPrecision,
            ReadingRecall = readingRecall,
            OnTimeDetectedEvents = report.EventMetrics.DetectedOnTime,
            LateDetectedEvents = report.EventMetrics.DetectedLate,
            MissedEvents = report.EventMetrics.Missed,
            OnTimeEpisodePrecision = report.EpisodeMetrics.OnTimeEpisodePrecision
        };
    }

    private static T Read<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, Options) ??
        throw new ArgumentException("JSON da comparacao vazio ou invalido.");
}

public sealed record ReplayComparisonReading(
    int Index, DateTimeOffset TimestampUtc, string TagName, string Phase,
    double ScoreSix, double ScoreSeven, double Difference, bool AlertSix, bool AlertSeven);

public sealed record ReplayComparisonPhase(
    string Phase, int Readings, double? MeanScoreSix, double? MeanScoreSeven,
    double? MeanDifference, double? MaxScoreSix, double? MaxScoreSeven,
    int AlertDisagreements);

public sealed record ReplayClassificationSummary(
    int TruePositives, int FalseNegatives, int FalsePositives, int TrueNegatives,
    int UnlabeledReadings, int FinalAlertReadings, string EventClassification,
    double? DetectionDelayMinutes, double? StrictEventRecall, int FalsePositiveEpisodes)
{
    // Campos aditivos: preservam o construtor/contrato v1 consumido antes da M4.2A.
    public double? ScorableReadingPrecision { get; init; }
    public double? ReadingRecall { get; init; }
    public int OnTimeDetectedEvents { get; init; }
    public int LateDetectedEvents { get; init; }
    public int MissedEvents { get; init; }
    public double? OnTimeEpisodePrecision { get; init; }
}

public sealed record ReplayModeComparisonResult(
    string SchemaVersion, string ScenarioId, string TestClientId, string TagName,
    string RunIdSix, string RunIdSeven, string ExecutionIdentityStatus,
    int AlignedReadings, int AlertDisagreements,
    ReplayClassificationSummary SixDetectors,
    ReplayClassificationSummary SevenDetectors,
    IReadOnlyList<ReplayComparisonPhase> Phases,
    IReadOnlyList<ReplayComparisonReading> Readings);
