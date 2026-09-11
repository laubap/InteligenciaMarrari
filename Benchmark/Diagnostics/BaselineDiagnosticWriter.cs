using System.Globalization;
using System.Text;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.Diagnostics;

public sealed class BaselineDiagnosticWriter : IAsyncDisposable
{
    private static readonly string[] DetectorNames =
    [
        "Z-Score",
        "Mediana e MAD",
        "EWMA",
        "Persistência",
        "Mudança de patamar",
        "Taxa de variação"
    ];

    private readonly StreamWriter _writer;

    public BaselineDiagnosticWriter(string path)
    {
        Path = path;
        _writer = new StreamWriter(
            new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        WriteHeader();
    }

    public string Path { get; }

    public async ValueTask WriteAsync(
        int fastWindowIndex,
        int minuteIndex,
        DateTime timestamp,
        string tag,
        double currentValue,
        int historyWindowCapacity,
        DetectorInput input,
        DetectorResult result)
    {
        var context = input.Contexto;
        var history = context.Historico;
        var historyStart = history.Count > 0 ? history[0].DataHora : (DateTime?)null;
        var historyEnd = history.Count > 0 ? history[^1].DataHora : (DateTime?)null;
        var median = context.Mediana;
        var protection = BaselineProtectionProfile.Build(input, median);

        var detectors = result.DetalhesDetectores
            .ToDictionary(
                detail => detail.Nome,
                detail => new DetectorDiagnostic
                {
                    Applicable = detail.Aplicavel,
                    Vote = detail.VotouAnomalia,
                    Score = detail.ScoreOriginal,
                    NormalizedScore = detail.ScoreNormalizado,
                    Metrics = detail.Metricas
                },
                StringComparer.OrdinalIgnoreCase);

        var record = new BaselineDiagnosticRecord
        {
            FastWindowIndex = fastWindowIndex,
            MinuteIndex = minuteIndex,
            Timestamp = timestamp,
            Tag = tag,
            CurrentValue = currentValue,
            HistoryCount = history.Count,
            HistoryWindowCapacity = historyWindowCapacity,
            HistoryWindowUtilization = historyWindowCapacity > 0
                ? history.Count / (double)historyWindowCapacity
                : 0,
            HistoryStart = historyStart,
            HistoryEnd = historyEnd,
            HistoryDurationMinutes = historyStart.HasValue && historyEnd.HasValue
                ? (historyEnd.Value - historyStart.Value).TotalMinutes
                : 0,
            MedianCadenceMinutes = context.IntervaloMedianoMinutos,
            CurrentTimestampAlreadyInHistory = history.Any(point =>
                point.DataHora == timestamp),
            HistoryLatestBeforeCurrent = !historyEnd.HasValue ||
                                         historyEnd.Value < timestamp,
            Mean = input.MediaHistorica,
            StdDev = input.DesvioPadraoHistorico,
            Median = median,
            Mad = context.Mad,
            Deadband = protection.Deadband,
            DeadbandSource = protection.DeadbandSource,
            DeadbandConfidence = protection.DeadbandConfidence,
            AutomaticDeadbandP95 = protection.AutomaticDeadbandP95,
            Detectors = detectors,
            FinalApplicable = result.Aplicavel,
            FinalAnomaly = result.EhAnomalia,
            FinalScore = result.Score,
            FinalNormalizedScore = result.ScoreNormalizado,
            ApplicableDetectorCount = detectors.Values.Count(item => item.Applicable),
            AnomalousDetectorCount = detectors.Values.Count(item => item.Vote)
        };

        await _writer.WriteLineAsync(CreateCsvLine(record));
    }

    public ValueTask DisposeAsync() => _writer.DisposeAsync();

    private void WriteHeader()
    {
        var columns = new List<string>
        {
            "fastWindowIndex", "minuteIndex", "timestamp", "tag", "currentValue",
            "historyCount", "historyWindowCapacity", "historyWindowUtilization",
            "historyStart", "historyEnd", "historyDurationMinutes",
            "medianCadenceMinutes", "currentTimestampAlreadyInHistory",
            "historyLatestBeforeCurrent", "mean", "stdDev", "median", "mad",
            "deadband", "deadbandSource", "deadbandConfidence",
            "automaticDeadbandP95"
        };

        foreach (var detector in DetectorNames)
        {
            var prefix = Prefix(detector);
            columns.AddRange(
            [
                prefix + "Applicable",
                prefix + "Vote",
                prefix + "Score",
                prefix + "Normalized"
            ]);
        }

        columns.AddRange(
        [
            "ewmaTarget", "ewmaAlphaBase", "ewmaAlphaEffective",
            "ewmaFinalValue", "ewmaStdDev", "ewmaRawScore", "ewmaInsideDeadband",
            "persistenceConsecutivePoints", "persistenceDurationMinutes",
            "persistenceMinimumDurationMinutes", "persistenceGapLimitMinutes",
            "persistenceSequenceBrokenByGap", "persistenceUsesMad",
            "persistenceUsesDeadband",
            "levelShiftPreviousMedian", "levelShiftRecentMedian",
            "levelShiftDifference", "levelShiftConsistency",
            "levelShiftPreviousPointCount", "levelShiftRecentPointCount",
            "rocCurrentRate", "rocMedianRate", "rocMadRate", "rocStdDevRate",
            "rocFallbackStdDev",
            "finalApplicable", "finalAnomaly", "finalScore",
            "finalNormalizedScore", "applicableDetectorCount",
            "anomalousDetectorCount"
        ]);

        _writer.WriteLine(string.Join(',', columns));
    }

    private static string CreateCsvLine(BaselineDiagnosticRecord record)
    {
        var values = new List<string>
        {
            I(record.FastWindowIndex), I(record.MinuteIndex), D(record.Timestamp),
            S(record.Tag), N(record.CurrentValue), I(record.HistoryCount),
            I(record.HistoryWindowCapacity), N(record.HistoryWindowUtilization),
            D(record.HistoryStart), D(record.HistoryEnd),
            N(record.HistoryDurationMinutes), N(record.MedianCadenceMinutes),
            B(record.CurrentTimestampAlreadyInHistory),
            B(record.HistoryLatestBeforeCurrent), N(record.Mean), N(record.StdDev),
            N(record.Median), N(record.Mad), N(record.Deadband),
            S(record.DeadbandSource), N(record.DeadbandConfidence),
            N(record.AutomaticDeadbandP95)
        };

        foreach (var detectorName in DetectorNames)
        {
            record.Detectors.TryGetValue(detectorName, out var detector);
            values.Add(B(detector?.Applicable ?? false));
            values.Add(B(detector?.Vote ?? false));
            values.Add(N(detector?.Score));
            values.Add(N(detector?.NormalizedScore));
        }

        var ewma = Find(record, "EWMA");
        values.AddRange(
        [
            N(record.Mean),
            M(ewma, "lambdaBase"), M(ewma, "lambdaAtualEfetivo"),
            M(ewma, "ewmaAtual"), M(ewma, "desvioEwma"),
            M(ewma, "scoreEwmaAntesDeadband"), M(ewma, "dentroDeadband")
        ]);

        var persistence = Find(record, "Persistência");
        values.AddRange(
        [
            M(persistence, "pontosConsecutivos"), M(persistence, "duracaoMinutos"),
            M(persistence, "duracaoMinimaMinutos"), M(persistence, "limiteGapMinutos"),
            M(persistence, "sequenciaQuebradaPorGap"), M(persistence, "usaMad"),
            M(persistence, "usaDeadbandComoFronteira")
        ]);

        var levelShift = Find(record, "Mudança de patamar");
        values.AddRange(
        [
            M(levelShift, "medianaAnterior"), M(levelShift, "medianaRecente"),
            M(levelShift, "diferencaPatamar"), M(levelShift, "consistencia"),
            M(levelShift, "pontosJanelaAnterior"), M(levelShift, "pontosJanelaRecente")
        ]);

        var roc = Find(record, "Taxa de variação");
        values.AddRange(
        [
            M(roc, "taxaAtualPorMinuto"), M(roc, "medianaTaxas"),
            M(roc, "madTaxas"), M(roc, "desvioPadraoTaxas"),
            M(roc, "fallbackDesvioPadrao")
        ]);

        values.AddRange(
        [
            B(record.FinalApplicable), B(record.FinalAnomaly), N(record.FinalScore),
            N(record.FinalNormalizedScore), I(record.ApplicableDetectorCount),
            I(record.AnomalousDetectorCount)
        ]);

        return string.Join(',', values);
    }

    private static DetectorDiagnostic? Find(
        BaselineDiagnosticRecord record,
        string name) =>
        record.Detectors.TryGetValue(name, out var detector)
            ? detector
            : null;

    private static string M(
        DetectorDiagnostic? detector,
        params string[] names)
    {
        if (detector is null)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (detector.Metrics.TryGetValue(name, out var value))
            {
                return N(value);
            }
        }

        return "";
    }

    private static string Prefix(string detector) => detector switch
    {
        "Z-Score" => "zScore",
        "Mediana e MAD" => "mad",
        "EWMA" => "ewma",
        "Persistência" => "persistence",
        "Mudança de patamar" => "levelShift",
        "Taxa de variação" => "roc",
        _ => detector
    };

    private static string B(bool value) => value ? "true" : "false";
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string N(double? value) => value.HasValue && double.IsFinite(value.Value)
        ? value.Value.ToString("G17", CultureInfo.InvariantCulture)
        : "";
    private static string D(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static string D(DateTime? value) => value.HasValue ? D(value.Value) : "";
    private static string S(string value) =>
        '"' + value.Replace("\"", "\"\"") + '"';
}
