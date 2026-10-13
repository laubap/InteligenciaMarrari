using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace IAApi.Benchmark;

/// <summary>
/// Analisa automaticamente todos os eventos encontrados no banco limpo.
///
/// O objetivo é reduzir milhares de eventos individuais a padrões
/// recorrentes de comportamento que possam ser revisados pelo time técnico.
/// </summary>
public sealed class CalibrationAllInvestigator
{
    private const int EventGapMinutes = 5;
    private const int ContextBeforeMinutes = 30;
    private const int ContextAfterMinutes = 30;
    private const double MinimumRelativeChangeDenominator = 1e-9;
    private const int MaximumDetailedEventsInHtml = 500;
    private const int CoOccurrenceToleranceMinutes = 5;

    public async Task InvestigateAllAsync(
        string cleanDatabasePath,
        string detectionsPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(cleanDatabasePath))
        {
            throw new FileNotFoundException(
                "Banco limpo não encontrado.",
                cleanDatabasePath);
        }

        if (!File.Exists(detectionsPath))
        {
            throw new FileNotFoundException(
                "Arquivo de detecções não encontrado.",
                detectionsPath);
        }

        Console.WriteLine();
        Console.WriteLine(
            "Lendo e agrupando os eventos do banco limpo...");

        var events =
            await ReadAllEventsAsync(
                detectionsPath,
                cancellationToken);

        Console.WriteLine(
            $"Eventos agrupados encontrados: {events.Count:N0}");

        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource =
                    cleanDatabasePath,
                Mode =
                    SqliteOpenMode.ReadOnly
            }
            .ToString();

        var analyses =
            new List<CalibrationAllEventAnalysis>(
                events.Count);

        using var connection =
            new SqliteConnection(
                connectionString);

        connection.Open();

        using var command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT
                ms.MinuteIndex,
                ms.Value,
                COALESCE(ms.Quality, 0)
            FROM MinuteSamples AS ms
            INNER JOIN Tags AS t
                ON t.TagId = ms.TagId
            WHERE
                t.TagName = $tagName
                AND ms.MinuteIndex BETWEEN
                    $startMinute AND $endMinute
                AND ms.Value IS NOT NULL
            ORDER BY
                ms.MinuteIndex;
            """;

        var tagParameter =
            command.Parameters.Add(
                "$tagName",
                SqliteType.Text);

        var startParameter =
            command.Parameters.Add(
                "$startMinute",
                SqliteType.Integer);

        var endParameter =
            command.Parameters.Add(
                "$endMinute",
                SqliteType.Integer);

        var processed = 0;

        foreach (var eventItem in events)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            tagParameter.Value =
                eventItem.TagName;

            startParameter.Value =
                Math.Max(
                    0,
                    eventItem.StartMinute -
                    ContextBeforeMinutes);

            endParameter.Value =
                eventItem.EndMinute +
                ContextAfterMinutes;

            var samples =
                new List<CalibrationAllSample>();

            using (
                var reader =
                    command.ExecuteReader())
            {
                while (reader.Read())
                {
                    samples.Add(
                        new CalibrationAllSample
                        {
                            MinuteIndex =
                                reader.GetInt32(0),

                            Value =
                                reader.GetDouble(1),

                            Quality =
                                reader.GetInt32(2)
                        });
                }
            }

            analyses.Add(
                AnalyzeEvent(
                    eventItem,
                    samples));

            processed++;

            if (processed % 1000 == 0 ||
                processed == events.Count)
            {
                Console.WriteLine(
                    $"Analisados: {processed:N0}/{events.Count:N0}");
            }
        }

        var report =
            BuildReport(
                cleanDatabasePath,
                detectionsPath,
                analyses);

        Directory.CreateDirectory(
            outputDirectory);

        var id =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss");

        var jsonPath =
            Path.Combine(
                outputDirectory,
                $"calibration_all_summary_{id}.json");

        var csvPath =
            Path.Combine(
                outputDirectory,
                $"calibration_all_events_{id}.csv");

        var htmlPath =
            Path.Combine(
                outputDirectory,
                $"calibration_all_summary_{id}.html");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                }),
            cancellationToken);

        await File.WriteAllTextAsync(
            csvPath,
            CreateCsv(
                report.Events),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(report),
            cancellationToken);

        PrintConsole(
            report,
            jsonPath,
            csvPath,
            htmlPath);
    }

    private static async Task<List<CalibrationAllEvent>>
        ReadAllEventsAsync(
            string detectionsPath,
            CancellationToken cancellationToken)
    {
        var events =
            new List<CalibrationAllEvent>();

        var active =
            new Dictionary<string, CalibrationAllEvent>(
                StringComparer.OrdinalIgnoreCase);

        await using var stream =
            new FileStream(
                detectionsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1 << 20,
                FileOptions.SequentialScan);

        using var reader =
            new StreamReader(
                stream,
                bufferSize: 1 << 20);

        while (await reader.ReadLineAsync(
                   cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document =
                JsonDocument.Parse(line);

            var root =
                document.RootElement;

            var tag =
                root.GetProperty(
                    "tagName")
                    .GetString() ?? "";

            var minute =
                root.GetProperty(
                    "minuteIndex")
                    .GetInt32();

            var score =
                root.TryGetProperty(
                    "score",
                    out var scoreElement)
                    ? scoreElement.GetDouble()
                    : 0;

            var detectors =
                ReadResponsibleDetectors(
                    root);

            if (!active.TryGetValue(
                    tag,
                    out var current))
            {
                active[tag] =
                    CreateEvent(
                        tag,
                        minute,
                        score,
                        detectors);

                continue;
            }

            var gap =
                minute -
                current.EndMinute;

            if (gap >= 0 &&
                gap <= EventGapMinutes)
            {
                current.EndMinute =
                    minute;

                current.DetectionRecords++;

                current.MaximumScore =
                    Math.Max(
                        current.MaximumScore,
                        score);

                foreach (var detector in detectors)
                {
                    current.Detectors.Add(
                        detector);
                }

                continue;
            }

            events.Add(current);

            active[tag] =
                CreateEvent(
                    tag,
                    minute,
                    score,
                    detectors);
        }

        events.AddRange(
            active.Values);

        return events;
    }

    private static CalibrationAllEvent CreateEvent(
        string tag,
        int minute,
        double score,
        IReadOnlyCollection<string> detectors)
    {
        return new CalibrationAllEvent
        {
            TagName =
                tag,

            StartMinute =
                minute,

            EndMinute =
                minute,

            DetectionRecords =
                1,

            MaximumScore =
                score,

            Detectors =
                new HashSet<string>(
                    detectors,
                    StringComparer.OrdinalIgnoreCase)
        };
    }

    private static List<string>
        ReadResponsibleDetectors(
            JsonElement root)
    {
        var result =
            new List<string>();

        if (!root.TryGetProperty(
                "detectores",
                out var detectorArray) ||
            detectorArray.ValueKind !=
                JsonValueKind.Array)
        {
            return result;
        }

        foreach (var detectorElement in
                 detectorArray.EnumerateArray())
        {
            var applicable =
                !detectorElement.TryGetProperty(
                    "aplicavel",
                    out var applicableElement) ||
                applicableElement.ValueKind ==
                    JsonValueKind.True;

            var voted =
                detectorElement.TryGetProperty(
                    "votouAnomalia",
                    out var votedElement) &&
                votedElement.ValueKind ==
                    JsonValueKind.True;

            if (!applicable ||
                !voted)
            {
                continue;
            }

            var name =
                detectorElement.TryGetProperty(
                    "nome",
                    out var nameElement)
                    ? nameElement.GetString()
                    : null;

            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(
                    name);
            }
        }

        return result;
    }

    private static CalibrationAllEventAnalysis
        AnalyzeEvent(
            CalibrationAllEvent eventItem,
            IReadOnlyList<CalibrationAllSample> samples)
    {
        var before =
            samples
                .Where(
                    item =>
                        item.MinuteIndex <
                        eventItem.StartMinute)
                .ToList();

        var during =
            samples
                .Where(
                    item =>
                        item.MinuteIndex >=
                            eventItem.StartMinute &&
                        item.MinuteIndex <=
                            eventItem.EndMinute)
                .ToList();

        var after =
            samples
                .Where(
                    item =>
                        item.MinuteIndex >
                        eventItem.EndMinute)
                .ToList();

        var beforeSummary =
            Summarize(before);

        var duringSummary =
            Summarize(during);

        var afterSummary =
            Summarize(after);

        var absoluteChange =
            duringSummary.HasData &&
            beforeSummary.HasData
                ? duringSummary.Mean -
                  beforeSummary.Mean
                : 0;

        var relativeChangePercent =
            beforeSummary.HasData &&
            Math.Abs(beforeSummary.Mean) >
                MinimumRelativeChangeDenominator
                ? absoluteChange /
                  Math.Abs(beforeSummary.Mean) *
                  100.0
                : 0;

        var returnedToPreviousLevel =
            beforeSummary.HasData &&
            afterSummary.HasData &&
            Math.Abs(
                afterSummary.Mean -
                beforeSummary.Mean) <=
            Math.Max(
                Math.Abs(absoluteChange) * 0.25,
                Math.Max(
                    beforeSummary.StandardDeviation * 3.0,
                    1e-9));

        var classification =
            Classify(
                eventItem,
                beforeSummary,
                duringSummary,
                afterSummary,
                absoluteChange,
                relativeChangePercent,
                returnedToPreviousLevel);

        var confidence =
            DetermineClassificationConfidence(
                eventItem,
                beforeSummary,
                duringSummary,
                relativeChangePercent,
                returnedToPreviousLevel,
                classification);

        var suggestedAction =
            DetermineSuggestedAction(
                eventItem.TagName,
                classification,
                eventItem.Detectors);

        var diagnosticFlags =
            BuildDiagnosticFlags(
                eventItem,
                beforeSummary,
                duringSummary,
                afterSummary,
                relativeChangePercent,
                returnedToPreviousLevel);

        return new CalibrationAllEventAnalysis
        {
            TagName =
                eventItem.TagName,

            StartMinute =
                eventItem.StartMinute,

            EndMinute =
                eventItem.EndMinute,

            DurationMinutes =
                eventItem.EndMinute -
                eventItem.StartMinute + 1,

            DetectionRecords =
                eventItem.DetectionRecords,

            MaximumScore =
                eventItem.MaximumScore,

            Detectors =
                eventItem.Detectors
                    .OrderBy(
                        item => item)
                    .ToList(),

            Before =
                beforeSummary,

            During =
                duringSummary,

            After =
                afterSummary,

            AbsoluteChange =
                absoluteChange,

            RelativeChangePercent =
                relativeChangePercent,

            ReturnedToPreviousLevel =
                returnedToPreviousLevel,

            Classification =
                classification,

            ClassificationConfidence =
                confidence,

            SuggestedAction =
                suggestedAction,

            DiagnosticFlags =
                diagnosticFlags
        };
    }

    private static string Classify(
        CalibrationAllEvent eventItem,
        CalibrationAllWindowSummary before,
        CalibrationAllWindowSummary during,
        CalibrationAllWindowSummary after,
        double absoluteChange,
        double relativeChangePercent,
        bool returnedToPreviousLevel)
    {
        if (eventItem.TagName.Contains(
                "MODE",
                StringComparison.OrdinalIgnoreCase) ||
            eventItem.TagName.Contains(
                "STATE",
                StringComparison.OrdinalIgnoreCase) ||
            eventItem.TagName.Contains(
                "ONLINE",
                StringComparison.OrdinalIgnoreCase) ||
            eventItem.TagName.EndsWith(
                ".CODE",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Tag discreta analisada por detector contínuo";
        }

        if (!before.HasData ||
            !during.HasData)
        {
            return
                "Contexto insuficiente";
        }

        var baselineScale =
            Math.Max(
                before.StandardDeviation,
                1e-9);

        var standardizedChange =
            Math.Abs(absoluteChange) /
            baselineScale;

        if (Math.Abs(relativeChangePercent) < 1.0 &&
            standardizedChange >= 3.0)
        {
            return
                "Pequena mudança amplificada pela baixa variância";
        }

        if (returnedToPreviousLevel &&
            absoluteChange > 0)
        {
            return
                "Pico temporário com retorno";
        }

        if (returnedToPreviousLevel &&
            absoluteChange < 0)
        {
            return
                "Queda temporária com retorno";
        }

        if (eventItem.EndMinute -
            eventItem.StartMinute + 1 >= 360)
        {
            return
                "Transição ou regime operacional prolongado";
        }

        if (Math.Abs(relativeChangePercent) >= 10.0)
        {
            return
                "Mudança relevante de patamar";
        }

        if (eventItem.Detectors.Any(
                detector =>
                    detector.StartsWith(
                        "Multivariado/",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return
                "Relação multivariada fora do padrão";
        }

        return
            "Variação estatística para revisão";
    }

    private static string DetermineClassificationConfidence(
        CalibrationAllEvent eventItem,
        CalibrationAllWindowSummary before,
        CalibrationAllWindowSummary during,
        double relativeChangePercent,
        bool returnedToPreviousLevel,
        string classification)
    {
        if (classification ==
                "Contexto insuficiente")
        {
            return "Baixa";
        }

        if (classification ==
                "Tag discreta analisada por detector contínuo")
        {
            return "Alta";
        }

        if (classification ==
                "Pequena mudança amplificada pela baixa variância" &&
            before.HasData &&
            before.StandardDeviation <=
                Math.Max(
                    Math.Abs(before.Mean) * 0.001,
                    1e-9))
        {
            return "Alta";
        }

        if (returnedToPreviousLevel ||
            Math.Abs(relativeChangePercent) >= 10.0 ||
            eventItem.Detectors.Count >= 4)
        {
            return "Alta";
        }

        if (during.HasData &&
            eventItem.Detectors.Count >= 2)
        {
            return "Média";
        }

        return "Baixa";
    }

    private static string DetermineSuggestedAction(
        string tagName,
        string classification,
        IReadOnlyCollection<string> detectors)
    {
        if (classification ==
                "Tag discreta analisada por detector contínuo")
        {
            return
                "Remover do ensemble contínuo e usar detector de estado";
        }

        if (tagName.StartsWith(
                "WEATHER.",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usar como contexto ou aplicar perfil sazonal";
        }

        if (classification ==
                "Pequena mudança amplificada pela baixa variância")
        {
            return
                "Adicionar mudança absoluta mínima configurável";
        }

        if (classification ==
                "Transição ou regime operacional prolongado")
        {
            return
                "Validar estado operacional, lote, receita ou partida/parada";
        }

        if (classification ==
                "Pico temporário com retorno" ||
            classification ==
                "Queda temporária com retorno")
        {
            return
                "Validar ciclo normal e aplicar debounce operacional";
        }

        if (detectors.Any(
                detector =>
                    detector.StartsWith(
                        "Multivariado/",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return
                "Revisar baseline e estados válidos do grupo multivariado";
        }

        if (classification ==
                "Contexto insuficiente")
        {
            return
                "Ignorar aquecimento/bordas ou ampliar contexto";
        }

        return
            "Revisar amostra e calibrar detector dominante";
    }

    private static List<string> BuildDiagnosticFlags(
        CalibrationAllEvent eventItem,
        CalibrationAllWindowSummary before,
        CalibrationAllWindowSummary during,
        CalibrationAllWindowSummary after,
        double relativeChangePercent,
        bool returnedToPreviousLevel)
    {
        var flags =
            new List<string>();

        if (eventItem.Detectors.Count >= 4)
        {
            flags.Add(
                "consenso-forte-entre-detectores");
        }

        if (before.HasData &&
            before.StandardDeviation <=
                Math.Max(
                    Math.Abs(before.Mean) * 0.001,
                    1e-9))
        {
            flags.Add(
                "baseline-com-variancia-muito-baixa");
        }

        if (returnedToPreviousLevel)
        {
            flags.Add(
                "retorno-ao-baseline");
        }

        if (eventItem.EndMinute -
            eventItem.StartMinute + 1 >= 360)
        {
            flags.Add(
                "evento-prolongado");
        }

        if (Math.Abs(relativeChangePercent) >= 10.0)
        {
            flags.Add(
                "mudanca-percentual-relevante");
        }

        if (during.HasData &&
            Math.Abs(during.Mean) <= 1e-12 &&
            before.HasData &&
            Math.Abs(before.Mean) > 1e-9)
        {
            flags.Add(
                "valor-zero-durante-evento");
        }

        if (!before.HasData ||
            !after.HasData)
        {
            flags.Add(
                "evento-na-borda-do-dataset");
        }

        if (eventItem.Detectors.Any(
                detector =>
                    detector.StartsWith(
                        "Multivariado/",
                        StringComparison.OrdinalIgnoreCase)))
        {
            flags.Add(
                "origem-multivariada");
        }

        return flags;
    }

    private static List<CalibrationDetectorSummary>
        BuildDetectorSummaries(
            IReadOnlyCollection<CalibrationAllEventAnalysis> analyses)
    {
        return analyses
            .SelectMany(
                item =>
                    item.Detectors.Select(
                        detector =>
                            new
                            {
                                Detector =
                                    detector,
                                Event =
                                    item
                            }))
            .GroupBy(
                item => item.Detector,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                    new CalibrationDetectorSummary
                    {
                        Detector =
                            group.Key,

                        EventCount =
                            group.LongCount(),

                        TagCount =
                            group.Select(
                                    item =>
                                        item.Event.TagName)
                                .Distinct(
                                    StringComparer.OrdinalIgnoreCase)
                                .LongCount(),

                        AverageMaximumScore =
                            group.Average(
                                item =>
                                    item.Event.MaximumScore),

                        AverageDurationMinutes =
                            group.Average(
                                item =>
                                    item.Event.DurationMinutes),

                        HighConfidenceRate =
                            group.LongCount(
                                item =>
                                    string.Equals(
                                        item.Event.ClassificationConfidence,
                                        "Alta",
                                        StringComparison.OrdinalIgnoreCase)) /
                            (double)Math.Max(
                                1,
                                group.LongCount())
                    })
            .OrderByDescending(
                item => item.EventCount)
            .ToList();
    }

    private static List<CalibrationCoOccurrenceSummary>
        BuildCoOccurrenceSummaries(
            IReadOnlyCollection<CalibrationAllEventAnalysis> analyses)
    {
        var ordered =
            analyses
                .OrderBy(
                    item => item.StartMinute)
                .ThenBy(
                    item => item.EndMinute)
                .ToList();

        var counts =
            new Dictionary<string, CalibrationCoOccurrenceSummary>(
                StringComparer.OrdinalIgnoreCase);

        for (var i = 0;
             i < ordered.Count;
             i++)
        {
            var left =
                ordered[i];

            for (var j = i + 1;
                 j < ordered.Count;
                 j++)
            {
                var right =
                    ordered[j];

                if (right.StartMinute >
                    left.EndMinute +
                    CoOccurrenceToleranceMinutes)
                {
                    break;
                }

                if (string.Equals(
                        left.TagName,
                        right.TagName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var overlaps =
                    right.EndMinute >=
                        left.StartMinute -
                        CoOccurrenceToleranceMinutes;

                if (!overlaps)
                {
                    continue;
                }

                var first =
                    string.Compare(
                        left.TagName,
                        right.TagName,
                        StringComparison.OrdinalIgnoreCase) <= 0
                        ? left.TagName
                        : right.TagName;

                var second =
                    string.Equals(
                        first,
                        left.TagName,
                        StringComparison.OrdinalIgnoreCase)
                        ? right.TagName
                        : left.TagName;

                var key =
                    $"{first}\u001F{second}";

                if (!counts.TryGetValue(
                        key,
                        out var summary))
                {
                    summary =
                        new CalibrationCoOccurrenceSummary
                        {
                            TagA =
                                first,
                            TagB =
                                second
                        };

                    counts[key] =
                        summary;
                }

                summary.OverlapCount++;
            }
        }

        return counts.Values
            .OrderByDescending(
                item => item.OverlapCount)
            .Take(100)
            .ToList();
    }

    private static CalibrationAllWindowSummary Summarize(
        IReadOnlyList<CalibrationAllSample> samples)
    {
        var values =
            samples
                .Select(
                    item => item.Value)
                .Where(
                    double.IsFinite)
                .ToArray();

        if (values.Length == 0)
        {
            return new CalibrationAllWindowSummary();
        }

        var mean =
            values.Average();

        var variance =
            values
                .Select(
                    value =>
                        Math.Pow(
                            value - mean,
                            2))
                .Average();

        return new CalibrationAllWindowSummary
        {
            HasData =
                true,

            Count =
                values.Length,

            Minimum =
                values.Min(),

            Maximum =
                values.Max(),

            Mean =
                mean,

            StandardDeviation =
                Math.Sqrt(
                    variance),

            FirstValue =
                values[0],

            LastValue =
                values[^1]
        };
    }

    private static CalibrationAllReport BuildReport(
        string cleanDatabasePath,
        string detectionsPath,
        List<CalibrationAllEventAnalysis> analyses)
    {
        var classifications =
            analyses
                .GroupBy(
                    item => item.Classification,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new CalibrationAllClassificationSummary
                        {
                            Classification =
                                group.Key,

                            EventCount =
                                group.LongCount(),

                            TagCount =
                                group
                                    .Select(
                                        item => item.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount()
                        })
                .OrderByDescending(
                    item => item.EventCount)
                .ToList();

        var tags =
            analyses
                .GroupBy(
                    item => item.TagName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                    {
                        var topClassification =
                            group
                                .GroupBy(
                                    item =>
                                        item.Classification,
                                    StringComparer.OrdinalIgnoreCase)
                                .OrderByDescending(
                                    item =>
                                        item.Count())
                                .First();

                        var topDetector =
                            group
                                .SelectMany(
                                    item =>
                                        item.Detectors)
                                .GroupBy(
                                    item => item,
                                    StringComparer.OrdinalIgnoreCase)
                                .OrderByDescending(
                                    item =>
                                        item.Count())
                                .Select(
                                    item =>
                                        item.Key)
                                .FirstOrDefault() ?? "";

                        return new CalibrationAllTagSummary
                        {
                            TagName =
                                group.Key,

                            EventCount =
                                group.LongCount(),

                            PrimaryClassification =
                                topClassification.Key,

                            PrimaryDetector =
                                topDetector,

                            AverageMaximumScore =
                                group.Average(
                                    item =>
                                        item.MaximumScore),

                            AverageAbsoluteChange =
                                group.Average(
                                    item =>
                                        Math.Abs(
                                            item.AbsoluteChange))
                        };
                    })
                .OrderByDescending(
                    item =>
                        item.EventCount)
                .ToList();

        var detectors =
            BuildDetectorSummaries(
                analyses);

        var coOccurrences =
            BuildCoOccurrenceSummaries(
                analyses);

        var actions =
            analyses
                .GroupBy(
                    item => item.SuggestedAction,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new CalibrationActionSummary
                        {
                            SuggestedAction =
                                group.Key,
                            EventCount =
                                group.LongCount(),
                            TagCount =
                                group.Select(
                                        item => item.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount()
                        })
                .OrderByDescending(
                    item => item.EventCount)
                .ToList();

        return new CalibrationAllReport
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            CleanDatabasePath =
                Path.GetFullPath(
                    cleanDatabasePath),

            DetectionsPath =
                Path.GetFullPath(
                    detectionsPath),

            TotalEvents =
                analyses.Count,

            HighConfidenceEvents =
                analyses.LongCount(
                    item =>
                        string.Equals(
                            item.ClassificationConfidence,
                            "Alta",
                            StringComparison.OrdinalIgnoreCase)),

            MultiDetectorConsensusEvents =
                analyses.LongCount(
                    item =>
                        item.Detectors.Count >= 4),

            ReturnedToBaselineEvents =
                analyses.LongCount(
                    item =>
                        item.ReturnedToPreviousLevel),

            Classifications =
                classifications,

            Tags =
                tags,

            Detectors =
                detectors,

            CoOccurrences =
                coOccurrences,

            Actions =
                actions,

            Events =
                analyses
                    .OrderBy(
                        item => item.TagName)
                    .ThenBy(
                        item => item.StartMinute)
                    .ToList()
        };
    }

    private static string CreateCsv(
        IReadOnlyList<CalibrationAllEventAnalysis> events)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "tag,inicioMinuto,fimMinuto,duracaoMinutos," +
            "detecoes,scoreMaximo,detectores,classificacao," +
            "mediaAntes,desvioAntes,mediaDurante,desvioDurante," +
            "mediaDepois,desvioDepois,mudancaAbsoluta," +
            "mudancaPercentual,retornouAoNivelAnterior," +
            "confiancaClassificacao,acaoSugerida,flagsDiagnosticas");

        foreach (var item in events)
        {
            builder.Append(
                    EscapeCsv(item.TagName))
                .Append(',')
                .Append(item.StartMinute)
                .Append(',')
                .Append(item.EndMinute)
                .Append(',')
                .Append(item.DurationMinutes)
                .Append(',')
                .Append(item.DetectionRecords)
                .Append(',')
                .Append(
                    item.MaximumScore.ToString(
                        "F6",
                        CultureInfo.InvariantCulture))
                .Append(',')
                .Append(
                    EscapeCsv(
                        string.Join(
                            " | ",
                            item.Detectors)))
                .Append(',')
                .Append(
                    EscapeCsv(
                        item.Classification))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.Before.Mean))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.Before.StandardDeviation))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.During.Mean))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.During.StandardDeviation))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.After.Mean))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.After.StandardDeviation))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.AbsoluteChange))
                .Append(',')
                .Append(
                    FormatNumber(
                        item.RelativeChangePercent))
                .Append(',')
                .Append(
                    item.ReturnedToPreviousLevel
                        ? "true"
                        : "false")
                .Append(',')
                .Append(
                    EscapeCsv(
                        item.ClassificationConfidence))
                .Append(',')
                .Append(
                    EscapeCsv(
                        item.SuggestedAction))
                .Append(',')
                .AppendLine(
                    EscapeCsv(
                        string.Join(
                            " | ",
                            item.DiagnosticFlags)));
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        CalibrationAllReport report)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" " +
            "content=\"width=device-width,initial-scale=1\">");
        html.AppendLine(
            "<title>Auditoria de Calibração</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Inter,Segoe UI,Arial,sans-serif;" +
            "background:#f3f4f6;margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1700px;margin:auto}");
        html.AppendLine(
            ".grid{display:grid;grid-template-columns:" +
            "repeat(auto-fit,minmax(190px,1fr));gap:14px}");
        html.AppendLine(
            ".card,section{background:white;border-radius:14px;" +
            "padding:18px;margin:18px 0;box-shadow:0 1px 5px #0002}");
        html.AppendLine(
            ".value{font-size:30px;font-weight:800;margin-top:6px}");
        html.AppendLine(
            ".muted{color:#6b7280;font-size:13px}");
        html.AppendLine(
            "table{width:100%;border-collapse:collapse;font-size:13px}");
        html.AppendLine(
            "th,td{text-align:left;padding:9px;" +
            "border-bottom:1px solid #e5e7eb;vertical-align:top}");
        html.AppendLine(
            "th{position:sticky;top:0;background:#f9fafb}");
        html.AppendLine(
            ".warning{border-left:5px solid #f59e0b;" +
            "background:#fffbeb;padding:14px;border-radius:8px}");
        html.AppendLine(
            ".bar{height:14px;background:#e5e7eb;border-radius:999px;" +
            "overflow:hidden;min-width:160px}");
        html.AppendLine(
            ".bar>span{display:block;height:100%;background:#4f46e5}");
        html.AppendLine(
            ".tag{font-family:Consolas,monospace;font-size:12px}");
        html.AppendLine(
            ".pill{display:inline-block;border-radius:999px;" +
            "padding:3px 8px;background:#eef2ff;margin:2px;font-size:11px}");
        html.AppendLine(
            "input{width:100%;max-width:500px;padding:10px 12px;" +
            "border:1px solid #d1d5db;border-radius:8px;margin-bottom:12px}");
        html.AppendLine("</style>");
        html.AppendLine("<script>");
        html.AppendLine(
            "function filtrar(){const q=document.getElementById('busca')" +
            ".value.toLowerCase();document.querySelectorAll('#eventos tbody tr')" +
            ".forEach(r=>r.style.display=r.innerText.toLowerCase().includes(q)" +
            "?'':'none');}");
        html.AppendLine("</script></head><body>");
        html.AppendLine("<div class=\"container\">");
        html.AppendLine(
            "<h1>Auditoria de calibração do banco limpo</h1>");
        html.AppendLine(
            "<div class=\"muted\">Classificação automática, diagnóstico " +
            "dos detectores e priorização das próximas ações.</div>");

        html.AppendLine("<div class=\"grid\">");
        AppendMetricCard(
            html,
            "Eventos",
            report.TotalEvents.ToString("N0"));
        AppendMetricCard(
            html,
            "Alta confiança",
            report.HighConfidenceEvents.ToString("N0"));
        AppendMetricCard(
            html,
            "Consenso ≥ 4 detectores",
            report.MultiDetectorConsensusEvents.ToString("N0"));
        AppendMetricCard(
            html,
            "Retorno ao baseline",
            report.ReturnedToBaselineEvents.ToString("N0"));
        AppendMetricCard(
            html,
            "Tags avaliadas",
            report.Tags.Count.ToString("N0"));
        AppendMetricCard(
            html,
            "Detectores envolvidos",
            report.Detectors.Count.ToString("N0"));
        html.AppendLine("</div>");

        html.AppendLine(
            "<div class=\"warning\"><strong>Importante:</strong> " +
            "o banco é rotulado como limpo. Portanto, os eventos são " +
            "candidatos a falsos positivos operacionais. A classificação " +
            "é matemática e deve ser confirmada pelo especialista do processo." +
            "</div>");

        AppendClassificationSection(
            html,
            report);

        AppendActionSection(
            html,
            report);

        AppendDetectorSection(
            html,
            report);

        AppendTagSection(
            html,
            report);

        AppendCoOccurrenceSection(
            html,
            report);

        html.AppendLine(
            "<section id=\"eventos\"><h2>Eventos prioritários</h2>");
        html.AppendLine(
            "<p class=\"muted\">O HTML mostra os " +
            $"{Math.Min(MaximumDetailedEventsInHtml, report.Events.Count):N0} " +
            "eventos mais prioritários. O CSV e o JSON contêm todos.</p>");
        html.AppendLine(
            "<input id=\"busca\" oninput=\"filtrar()\" " +
            "placeholder=\"Filtrar por tag, detector, classificação ou ação...\">");
        html.AppendLine(
            "<table><thead><tr><th>Tag</th><th>Início</th>" +
            "<th>Duração</th><th>Score</th><th>Detectores</th>" +
            "<th>Classificação</th><th>Confiança</th>" +
            "<th>Mudança %</th><th>Flags</th><th>Ação sugerida</th>" +
            "</tr></thead><tbody>");

        var prioritized =
            report.Events
                .OrderByDescending(
                    item =>
                        item.ClassificationConfidence == "Alta")
                .ThenByDescending(
                    item =>
                        item.Detectors.Count)
                .ThenByDescending(
                    item =>
                        item.MaximumScore)
                .ThenByDescending(
                    item =>
                        item.DurationMinutes)
                .Take(
                    MaximumDetailedEventsInHtml);

        foreach (var item in prioritized)
        {
            html.Append("<tr><td class=\"tag\">")
                .Append(
                    Encode(item.TagName))
                .Append("</td><td>")
                .Append(item.StartMinute)
                .Append("</td><td>")
                .Append(item.DurationMinutes)
                .Append(" min</td><td>")
                .Append(item.MaximumScore.ToString("F2"))
                .Append("</td><td>")
                .Append(
                    Encode(
                        string.Join(
                            ", ",
                            item.Detectors)))
                .Append("</td><td>")
                .Append(
                    Encode(item.Classification))
                .Append("</td><td>")
                .Append(
                    Encode(
                        item.ClassificationConfidence))
                .Append("</td><td>")
                .Append(
                    item.RelativeChangePercent.ToString("F2"))
                .Append("%</td><td>");

            foreach (var flag in item.DiagnosticFlags)
            {
                html.Append("<span class=\"pill\">")
                    .Append(Encode(flag))
                    .Append("</span>");
            }

            html.Append("</td><td>")
                .Append(
                    Encode(item.SuggestedAction))
                .AppendLine("</td></tr>");
        }

        html.AppendLine(
            "</tbody></table></section>");
        html.AppendLine("</div></body></html>");

        return html.ToString();
    }

    private static void AppendMetricCard(
        StringBuilder html,
        string title,
        string value)
    {
        html.Append("<div class=\"card\"><div class=\"muted\">")
            .Append(Encode(title))
            .Append("</div><div class=\"value\">")
            .Append(Encode(value))
            .AppendLine("</div></div>");
    }

    private static void AppendClassificationSection(
        StringBuilder html,
        CalibrationAllReport report)
    {
        var maximum =
            Math.Max(
                1,
                report.Classifications
                    .Select(
                        item => item.EventCount)
                    .DefaultIfEmpty(1)
                    .Max());

        html.AppendLine(
            "<section><h2>Classificações preliminares</h2>");
        html.AppendLine(
            "<table><thead><tr><th>Classificação</th>" +
            "<th>Eventos</th><th>Tags</th><th>Distribuição</th>" +
            "</tr></thead><tbody>");

        foreach (var item in report.Classifications)
        {
            var width =
                item.EventCount /
                (double)maximum *
                100.0;

            html.Append("<tr><td>")
                .Append(Encode(item.Classification))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .Append("</td><td><div class=\"bar\"><span style=\"width:")
                .Append(width.ToString("F2", CultureInfo.InvariantCulture))
                .AppendLine("%\"></span></div></td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void AppendActionSection(
        StringBuilder html,
        CalibrationAllReport report)
    {
        html.AppendLine(
            "<section><h2>Plano de ação sugerido</h2>");
        html.AppendLine(
            "<table><thead><tr><th>Ação</th><th>Eventos</th>" +
            "<th>Tags</th></tr></thead><tbody>");

        foreach (var item in report.Actions)
        {
            html.Append("<tr><td>")
                .Append(Encode(item.SuggestedAction))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void AppendDetectorSection(
        StringBuilder html,
        CalibrationAllReport report)
    {
        html.AppendLine(
            "<section><h2>Desempenho por detector</h2>");
        html.AppendLine(
            "<table><thead><tr><th>Detector</th><th>Eventos</th>" +
            "<th>Tags</th><th>Score médio</th><th>Duração média</th>" +
            "<th>Alta confiança</th></tr></thead><tbody>");

        foreach (var item in report.Detectors)
        {
            html.Append("<tr><td class=\"tag\">")
                .Append(Encode(item.Detector))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.AverageMaximumScore.ToString("F2"))
                .Append("</td><td>")
                .Append(item.AverageDurationMinutes.ToString("F1"))
                .Append(" min</td><td>")
                .Append(item.HighConfidenceRate.ToString("P1"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void AppendTagSection(
        StringBuilder html,
        CalibrationAllReport report)
    {
        html.AppendLine(
            "<section><h2>Resumo por tag</h2>");
        html.AppendLine(
            "<table><thead><tr><th>Tag</th><th>Eventos</th>" +
            "<th>Classificação principal</th><th>Detector principal</th>" +
            "<th>Score médio</th><th>Mudança absoluta média</th>" +
            "</tr></thead><tbody>");

        foreach (var item in report.Tags)
        {
            html.Append("<tr><td class=\"tag\">")
                .Append(Encode(item.TagName))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(Encode(item.PrimaryClassification))
                .Append("</td><td class=\"tag\">")
                .Append(Encode(item.PrimaryDetector))
                .Append("</td><td>")
                .Append(item.AverageMaximumScore.ToString("F2"))
                .Append("</td><td>")
                .Append(item.AverageAbsoluteChange.ToString("F6"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void AppendCoOccurrenceSection(
        StringBuilder html,
        CalibrationAllReport report)
    {
        html.AppendLine(
            "<section><h2>Tags que mudam juntas</h2>");
        html.AppendLine(
            "<p class=\"muted\">Sobreposição temporal de eventos, útil " +
            "para identificar ciclos, partidas, paradas e relações de processo.</p>");
        html.AppendLine(
            "<table><thead><tr><th>Tag A</th><th>Tag B</th>" +
            "<th>Sobreposições</th></tr></thead><tbody>");

        foreach (var item in report.CoOccurrences)
        {
            html.Append("<tr><td class=\"tag\">")
                .Append(Encode(item.TagA))
                .Append("</td><td class=\"tag\">")
                .Append(Encode(item.TagB))
                .Append("</td><td>")
                .Append(item.OverlapCount.ToString("N0"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static string EscapeCsv(
        string value)
    {
        return
            "\"" +
            value.Replace(
                "\"",
                "\"\"") +
            "\"";
    }

    private static string FormatNumber(
        double value)
    {
        return value.ToString(
            "G17",
            CultureInfo.InvariantCulture);
    }

    private static string Encode(
        string value)
    {
        return System.Net.WebUtility
            .HtmlEncode(value);
    }

    private static void PrintConsole(
        CalibrationAllReport report,
        string jsonPath,
        string csvPath,
        string htmlPath)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            " INVESTIGAÇÃO COMPLETA DO BANCO LIMPO");
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            $"Eventos analisados: {report.TotalEvents:N0}");
        Console.WriteLine(
            $"Alta confiança: {report.HighConfidenceEvents:N0}");
        Console.WriteLine(
            $"Consenso de 4+ detectores: " +
            $"{report.MultiDetectorConsensusEvents:N0}");
        Console.WriteLine(
            $"Retorno ao baseline: " +
            $"{report.ReturnedToBaselineEvents:N0}");

        Console.WriteLine();
        Console.WriteLine(
            "CLASSIFICAÇÕES:");

        foreach (var item in report.Classifications)
        {
            Console.WriteLine(
                $"  {item.Classification,-55} " +
                $"{item.EventCount,8:N0}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"JSON: {jsonPath}");
        Console.WriteLine(
            $"CSV:  {csvPath}");
        Console.WriteLine(
            $"HTML: {htmlPath}");
        Console.WriteLine(
            "========================================");
    }
}

public sealed class CalibrationAllEvent
{
    public string TagName { get; set; } = "";

    public int StartMinute { get; set; }

    public int EndMinute { get; set; }

    public int DetectionRecords { get; set; }

    public double MaximumScore { get; set; }

    public HashSet<string> Detectors { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CalibrationAllEventAnalysis
{
    public string TagName { get; set; } = "";

    public int StartMinute { get; set; }

    public int EndMinute { get; set; }

    public int DurationMinutes { get; set; }

    public int DetectionRecords { get; set; }

    public double MaximumScore { get; set; }

    public List<string> Detectors { get; set; } = [];

    public CalibrationAllWindowSummary Before { get; set; } = new();

    public CalibrationAllWindowSummary During { get; set; } = new();

    public CalibrationAllWindowSummary After { get; set; } = new();

    public double AbsoluteChange { get; set; }

    public double RelativeChangePercent { get; set; }

    public bool ReturnedToPreviousLevel { get; set; }

    public string Classification { get; set; } = "";

    public string ClassificationConfidence { get; set; } = "";

    public string SuggestedAction { get; set; } = "";

    public List<string> DiagnosticFlags { get; set; } = [];
}

public sealed class CalibrationAllWindowSummary
{
    public bool HasData { get; set; }

    public int Count { get; set; }

    public double Minimum { get; set; }

    public double Maximum { get; set; }

    public double Mean { get; set; }

    public double StandardDeviation { get; set; }

    public double FirstValue { get; set; }

    public double LastValue { get; set; }
}

public sealed class CalibrationAllSample
{
    public int MinuteIndex { get; set; }

    public double Value { get; set; }

    public int Quality { get; set; }
}

public sealed class CalibrationAllReport
{
    public DateTime GeneratedAtUtc { get; set; }

    public string CleanDatabasePath { get; set; } = "";

    public string DetectionsPath { get; set; } = "";

    public int TotalEvents { get; set; }

    public long HighConfidenceEvents { get; set; }

    public long MultiDetectorConsensusEvents { get; set; }

    public long ReturnedToBaselineEvents { get; set; }

    public List<CalibrationAllClassificationSummary>
        Classifications { get; set; } = [];

    public List<CalibrationAllTagSummary>
        Tags { get; set; } = [];

    public List<CalibrationDetectorSummary>
        Detectors { get; set; } = [];

    public List<CalibrationCoOccurrenceSummary>
        CoOccurrences { get; set; } = [];

    public List<CalibrationActionSummary>
        Actions { get; set; } = [];

    public List<CalibrationAllEventAnalysis>
        Events { get; set; } = [];
}

public sealed class CalibrationAllClassificationSummary
{
    public string Classification { get; set; } = "";

    public long EventCount { get; set; }

    public long TagCount { get; set; }
}

public sealed class CalibrationDetectorSummary
{
    public string Detector { get; set; } = "";

    public long EventCount { get; set; }

    public long TagCount { get; set; }

    public double AverageMaximumScore { get; set; }

    public double AverageDurationMinutes { get; set; }

    public double HighConfidenceRate { get; set; }
}

public sealed class CalibrationCoOccurrenceSummary
{
    public string TagA { get; set; } = "";

    public string TagB { get; set; } = "";

    public long OverlapCount { get; set; }
}

public sealed class CalibrationActionSummary
{
    public string SuggestedAction { get; set; } = "";

    public long EventCount { get; set; }

    public long TagCount { get; set; }
}

public sealed class CalibrationAllTagSummary
{
    public string TagName { get; set; } = "";

    public long EventCount { get; set; }

    public string PrimaryClassification { get; set; } = "";

    public string PrimaryDetector { get; set; } = "";

    public double AverageMaximumScore { get; set; }

    public double AverageAbsoluteChange { get; set; }
} 