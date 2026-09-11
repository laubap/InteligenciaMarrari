using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IAApi.Benchmark;

public sealed class CleanBenchmarkEvaluator
{
    private const int EventGapMinutes = 5;

    public async Task EvaluateAsync(
        string detectionsPath,
        string errorsPath,
        string summaryPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var totalReadings = await ReadTotalReadingsAsync(summaryPath, cancellationToken);

        long totalDetections = 0;
        long invalidLines = 0;
        long totalTagEvents = 0;
        long totalTagDetectorEvents = 0;

        var activeTags = new Dictionary<string, ActiveEvent>(StringComparer.OrdinalIgnoreCase);
        var activeTagDetectors = new Dictionary<string, ActiveEvent>(StringComparer.OrdinalIgnoreCase);
        var tags = new Dictionary<string, TagStats>(StringComparer.OrdinalIgnoreCase);
        var detectors = new Dictionary<string, DetectorStats>(StringComparer.OrdinalIgnoreCase);
        var tagDetectors = new Dictionary<string, TagDetectorStats>(StringComparer.OrdinalIgnoreCase);
        var combinations = new Dictionary<string, DetectorCombinationStats>(StringComparer.OrdinalIgnoreCase);

        await using var stream = new FileStream(
            detectionsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.SequentialScan);

        using var reader = new StreamReader(stream, bufferSize: 1 << 20);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            totalDetections++;

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                var tag = root.GetProperty("tagName").GetString() ?? "";
                var minute = root.GetProperty("minuteIndex").GetInt64();
                var score = root.TryGetProperty("score", out var scoreElement)
                    ? scoreElement.GetDouble()
                    : 0;

                if (!tags.TryGetValue(tag, out var tagStats))
                {
                    tagStats = new TagStats { TagName = tag };
                    tags[tag] = tagStats;
                }

                tagStats.Detections++;
                tagStats.ScoreSum += score;
                tagStats.MaxScore = Math.Max(tagStats.MaxScore, score);

                ProcessEvent(
                    tag,
                    minute,
                    activeTags,
                    () => CloseTagEvent(activeTags[tag], tagStats),
                    ref totalTagEvents);

                var responsible = ReadResponsibleDetectors(root);

                if (responsible.Count == 0)
                {
                    tagStats.DetectionsWithoutResponsibleDetector++;
                    continue;
                }

                var combinationName = string.Join(
                    " + ",
                    responsible
                        .Select(item => item.Name)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

                if (!combinations.TryGetValue(combinationName, out var combinationStats))
                {
                    combinationStats = new DetectorCombinationStats
                    {
                        Combination = combinationName
                    };
                    combinations[combinationName] = combinationStats;
                }

                combinationStats.DetectionRecords++;

                foreach (var item in responsible)
                {
                    if (!detectors.TryGetValue(item.Name, out var detectorStats))
                    {
                        detectorStats = new DetectorStats
                        {
                            DetectorName = item.Name
                        };
                        detectors[item.Name] = detectorStats;
                    }

                    detectorStats.DetectionRecords++;
                    detectorStats.ScoreSum += item.Score;
                    detectorStats.MaxScore = Math.Max(detectorStats.MaxScore, item.Score);

                    var key = CreateTagDetectorKey(tag, item.Name);

                    if (!tagDetectors.TryGetValue(key, out var tagDetectorStats))
                    {
                        tagDetectorStats = new TagDetectorStats
                        {
                            TagName = tag,
                            DetectorName = item.Name
                        };
                        tagDetectors[key] = tagDetectorStats;
                    }

                    tagDetectorStats.DetectionRecords++;
                    tagDetectorStats.ScoreSum += item.Score;
                    tagDetectorStats.MaxScore = Math.Max(tagDetectorStats.MaxScore, item.Score);

                    ProcessEvent(
                        key,
                        minute,
                        activeTagDetectors,
                        () => CloseTagDetectorEvent(
                            activeTagDetectors[key],
                            tagDetectorStats,
                            detectorStats),
                        ref totalTagDetectorEvents);
                }
            }
            catch
            {
                invalidLines++;
            }
        }

        foreach (var item in activeTags)
        {
            if (!tags.TryGetValue(item.Key, out var stats))
                continue;

            CloseTagEvent(item.Value, stats);
            totalTagEvents++;
        }

        foreach (var item in activeTagDetectors)
        {
            if (!tagDetectors.TryGetValue(item.Key, out var tagDetectorStats))
                continue;

            if (!detectors.TryGetValue(tagDetectorStats.DetectorName, out var detectorStats))
                continue;

            CloseTagDetectorEvent(item.Value, tagDetectorStats, detectorStats);
            totalTagDetectorEvents++;
        }

        var totalErrors = await CountNonEmptyLinesAsync(errorsPath, cancellationToken);

        var report = new CleanEvaluationReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DetectionsFile = Path.GetFullPath(detectionsPath),
            ErrorsFile = Path.GetFullPath(errorsPath),
            SummaryFile = Path.GetFullPath(summaryPath),
            TotalReadings = totalReadings,
            TotalDetectionRecords = totalDetections,
            InvalidDetectionLines = invalidLines,
            TotalGroupedEvents = totalTagEvents,
            TotalDetectorGroupedEvents = totalTagDetectorEvents,
            TotalErrorRecords = totalErrors,
            DetectionRate = totalReadings > 0
                ? totalDetections / (double)totalReadings
                : 0,
            EventsPerMillionReadings = totalReadings > 0
                ? totalTagEvents * 1_000_000.0 / totalReadings
                : 0,
            Tags = tags.Values
                .OrderByDescending(item => item.Events)
                .ThenByDescending(item => item.Detections)
                .ToList(),
            Detectors = detectors.Values
                .OrderByDescending(item => item.Events)
                .ThenByDescending(item => item.DetectionRecords)
                .ToList(),
            TagDetectors = tagDetectors.Values
                .OrderByDescending(item => item.Events)
                .ThenByDescending(item => item.DetectionRecords)
                .ToList(),
            DetectorCombinations = combinations.Values
                .OrderByDescending(item => item.DetectionRecords)
                .ToList()
        };

        Directory.CreateDirectory(outputDirectory);
        var id = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        var jsonPath = Path.Combine(outputDirectory, $"clean_evaluation_detailed_{id}.json");
        var detectorsCsvPath = Path.Combine(outputDirectory, $"clean_evaluation_detectors_{id}.csv");
        var tagDetectorsCsvPath = Path.Combine(outputDirectory, $"clean_evaluation_tag_detectors_{id}.csv");
        var htmlPath = Path.Combine(outputDirectory, $"clean_evaluation_detailed_{id}.html");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
            cancellationToken);

        await File.WriteAllTextAsync(detectorsCsvPath, CreateDetectorsCsv(report), cancellationToken);
        await File.WriteAllTextAsync(tagDetectorsCsvPath, CreateTagDetectorsCsv(report), cancellationToken);
        await File.WriteAllTextAsync(htmlPath, CreateHtml(report), cancellationToken);

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" AVALIAÇÃO DETALHADA DO BANCO LIMPO");
        Console.WriteLine("========================================");
        Console.WriteLine($"Leituras: {totalReadings:N0}");
        Console.WriteLine($"Detecções: {totalDetections:N0}");
        Console.WriteLine($"Eventos por tag: {totalTagEvents:N0}");
        Console.WriteLine($"Eventos por tag/detector: {totalTagDetectorEvents:N0}");
        Console.WriteLine($"Taxa por leitura: {report.DetectionRate:P4}");
        Console.WriteLine($"Eventos por milhão: {report.EventsPerMillionReadings:N2}");
        Console.WriteLine($"Erros: {totalErrors:N0}");
        Console.WriteLine();
        Console.WriteLine("TOP 10 DETECTORES POR EVENTOS:");

        foreach (var detector in report.Detectors.Take(10))
        {
            Console.WriteLine(
                $"  {detector.DetectorName,-40} " +
                $"{detector.Events,10:N0} evento(s) | " +
                $"{detector.DetectionRecords,12:N0} linha(s)");
        }

        Console.WriteLine();
        Console.WriteLine($"JSON: {jsonPath}");
        Console.WriteLine($"CSV detectores: {detectorsCsvPath}");
        Console.WriteLine($"CSV tag/detector: {tagDetectorsCsvPath}");
        Console.WriteLine($"HTML: {htmlPath}");
        Console.WriteLine("========================================");
    }

    private static void ProcessEvent(
        string key,
        long minute,
        Dictionary<string, ActiveEvent> activeEvents,
        Action closeCurrent,
        ref long totalEvents)
    {
        if (!activeEvents.TryGetValue(key, out var current))
        {
            activeEvents[key] = new ActiveEvent
            {
                StartMinute = minute,
                LastMinute = minute,
                DetectionCount = 1
            };
            return;
        }

        var gap = minute - current.LastMinute;

        if (gap >= 0 && gap <= EventGapMinutes)
        {
            current.LastMinute = minute;
            current.DetectionCount++;
            return;
        }

        closeCurrent();
        totalEvents++;

        activeEvents[key] = new ActiveEvent
        {
            StartMinute = minute,
            LastMinute = minute,
            DetectionCount = 1
        };
    }

    private static void CloseTagEvent(ActiveEvent current, TagStats stats)
    {
        stats.Events++;
        stats.LongestEventMinutes = Math.Max(
            stats.LongestEventMinutes,
            current.LastMinute - current.StartMinute + 1);
        stats.MaxDetectionsInEvent = Math.Max(
            stats.MaxDetectionsInEvent,
            current.DetectionCount);
    }

    private static void CloseTagDetectorEvent(
        ActiveEvent current,
        TagDetectorStats tagDetectorStats,
        DetectorStats detectorStats)
    {
        var duration = current.LastMinute - current.StartMinute + 1;

        tagDetectorStats.Events++;
        tagDetectorStats.LongestEventMinutes = Math.Max(
            tagDetectorStats.LongestEventMinutes,
            duration);
        tagDetectorStats.MaxDetectionsInEvent = Math.Max(
            tagDetectorStats.MaxDetectionsInEvent,
            current.DetectionCount);

        detectorStats.Events++;
        detectorStats.LongestEventMinutes = Math.Max(
            detectorStats.LongestEventMinutes,
            duration);
        detectorStats.MaxDetectionsInEvent = Math.Max(
            detectorStats.MaxDetectionsInEvent,
            current.DetectionCount);
    }

    private static List<ResponsibleDetector> ReadResponsibleDetectors(JsonElement root)
    {
        var result = new List<ResponsibleDetector>();

        if (!root.TryGetProperty("detectores", out var detectorsElement) ||
            detectorsElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var detector in detectorsElement.EnumerateArray())
        {
            var applicable = !detector.TryGetProperty("aplicavel", out var applicableElement) ||
                             applicableElement.ValueKind != JsonValueKind.False;

            var voted = detector.TryGetProperty("votouAnomalia", out var votedElement) &&
                        votedElement.ValueKind == JsonValueKind.True;

            if (!applicable || !voted)
                continue;

            var name = detector.TryGetProperty("nome", out var nameElement)
                ? nameElement.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var score = detector.TryGetProperty("scoreNormalizado", out var scoreElement)
                ? scoreElement.GetDouble()
                : 0;

            result.Add(new ResponsibleDetector
            {
                Name = name.Trim(),
                Score = score
            });
        }

        return result
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .ToList();
    }

    private static string CreateTagDetectorKey(string tag, string detector) =>
        $"{tag}\u001F{detector}";

    private static async Task<long> ReadTotalReadingsAsync(
        string summaryPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(summaryPath);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        return document.RootElement
            .GetProperty("totalLeiturasLidas")
            .GetInt64();
    }

    private static async Task<long> CountNonEmptyLinesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        long count = 0;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.SequentialScan);

        using var reader = new StreamReader(stream, bufferSize: 1 << 20);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                count++;
        }

        return count;
    }

    private static string CreateDetectorsCsv(CleanEvaluationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "detector,linhasResponsaveis,eventos,scoreMedio,maiorScore," +
            "maiorDuracaoEventoMinutos,maiorDeteccoesEvento");

        foreach (var detector in report.Detectors)
        {
            var average = detector.DetectionRecords > 0
                ? detector.ScoreSum / detector.DetectionRecords
                : 0;

            builder.Append(EscapeCsv(detector.DetectorName)).Append(',')
                .Append(detector.DetectionRecords).Append(',')
                .Append(detector.Events).Append(',')
                .Append(average.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(detector.MaxScore.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(detector.LongestEventMinutes).Append(',')
                .AppendLine(detector.MaxDetectionsInEvent.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string CreateTagDetectorsCsv(CleanEvaluationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "tag,detector,linhasResponsaveis,eventos,scoreMedio,maiorScore," +
            "maiorDuracaoEventoMinutos,maiorDeteccoesEvento");

        foreach (var item in report.TagDetectors)
        {
            var average = item.DetectionRecords > 0
                ? item.ScoreSum / item.DetectionRecords
                : 0;

            builder.Append(EscapeCsv(item.TagName)).Append(',')
                .Append(EscapeCsv(item.DetectorName)).Append(',')
                .Append(item.DetectionRecords).Append(',')
                .Append(item.Events).Append(',')
                .Append(average.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(item.MaxScore.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(item.LongestEventMinutes).Append(',')
                .AppendLine(item.MaxDetectionsInEvent.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string CreateHtml(CleanEvaluationReport report)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Calibração do Banco Limpo</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:Inter,Segoe UI,Arial,sans-serif;background:#f3f4f6;margin:0;padding:24px;color:#111827}");
        html.AppendLine(".container{max-width:1600px;margin:auto}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:14px;margin:20px 0}");
        html.AppendLine(".card,section{background:white;border-radius:12px;padding:18px;box-shadow:0 1px 4px #0002}section{margin:18px 0;overflow:auto}");
        html.AppendLine(".value{font-size:28px;font-weight:800;margin-top:6px}table{width:100%;border-collapse:collapse;font-size:13px}");
        html.AppendLine("th,td{text-align:left;padding:10px;border-bottom:1px solid #e5e7eb;vertical-align:top}th{position:sticky;top:0;background:#f9fafb}");
        html.AppendLine(".warning{border-left:5px solid #ef4444;background:#fef2f2;padding:14px;border-radius:8px}");
        html.AppendLine("</style></head><body><div class=\"container\">");
        html.AppendLine("<h1>Calibração do Banco Limpo</h1><div class=\"grid\">");

        AppendCard(html, "Leituras", report.TotalReadings.ToString("N0"));
        AppendCard(html, "Detecções", report.TotalDetectionRecords.ToString("N0"));
        AppendCard(html, "Eventos por tag", report.TotalGroupedEvents.ToString("N0"));
        AppendCard(html, "Eventos tag/detector", report.TotalDetectorGroupedEvents.ToString("N0"));
        AppendCard(html, "Taxa por leitura", report.DetectionRate.ToString("P4"));
        AppendCard(html, "Eventos por milhão", report.EventsPerMillionReadings.ToString("N2"));
        AppendCard(html, "Erros", report.TotalErrorRecords.ToString("N0"));

        html.AppendLine("</div>");
        html.AppendLine("<div class=\"warning\"><strong>Interpretação:</strong> o banco é limpo. Conte apenas detectores com <code>votouAnomalia=true</code>. A tabela mostra quem está produzindo os possíveis falsos positivos.</div>");

        html.AppendLine("<section><h2>Responsabilidade por detector</h2><table><thead><tr><th>Detector</th><th>Linhas</th><th>Eventos</th><th>Score médio</th><th>Maior score</th><th>Maior duração</th></tr></thead><tbody>");
        foreach (var detector in report.Detectors)
        {
            var average = detector.DetectionRecords > 0
                ? detector.ScoreSum / detector.DetectionRecords
                : 0;

            html.Append("<tr><td><code>").Append(Encode(detector.DetectorName))
                .Append("</code></td><td>").Append(detector.DetectionRecords.ToString("N0"))
                .Append("</td><td>").Append(detector.Events.ToString("N0"))
                .Append("</td><td>").Append(average.ToString("F2"))
                .Append("</td><td>").Append(detector.MaxScore.ToString("F2"))
                .Append("</td><td>").Append(detector.LongestEventMinutes.ToString("N0"))
                .AppendLine(" min</td></tr>");
        }
        html.AppendLine("</tbody></table></section>");

        html.AppendLine("<section><h2>Combinação tag + detector</h2><table><thead><tr><th>Tag</th><th>Detector</th><th>Linhas</th><th>Eventos</th><th>Score médio</th><th>Maior score</th><th>Maior duração</th></tr></thead><tbody>");
        foreach (var item in report.TagDetectors)
        {
            var average = item.DetectionRecords > 0
                ? item.ScoreSum / item.DetectionRecords
                : 0;

            html.Append("<tr><td><code>").Append(Encode(item.TagName))
                .Append("</code></td><td><code>").Append(Encode(item.DetectorName))
                .Append("</code></td><td>").Append(item.DetectionRecords.ToString("N0"))
                .Append("</td><td>").Append(item.Events.ToString("N0"))
                .Append("</td><td>").Append(average.ToString("F2"))
                .Append("</td><td>").Append(item.MaxScore.ToString("F2"))
                .Append("</td><td>").Append(item.LongestEventMinutes.ToString("N0"))
                .AppendLine(" min</td></tr>");
        }
        html.AppendLine("</tbody></table></section>");

        html.AppendLine("<section><h2>Combinações de detectores</h2><table><thead><tr><th>Detectores que votaram juntos</th><th>Linhas</th></tr></thead><tbody>");
        foreach (var combination in report.DetectorCombinations)
        {
            html.Append("<tr><td><code>").Append(Encode(combination.Combination))
                .Append("</code></td><td>").Append(combination.DetectionRecords.ToString("N0"))
                .AppendLine("</td></tr>");
        }
        html.AppendLine("</tbody></table></section>");

        html.AppendLine("</div></body></html>");
        return html.ToString();
    }

    private static void AppendCard(StringBuilder builder, string title, string value)
    {
        builder.Append("<div class=\"card\"><div>")
            .Append(Encode(title))
            .Append("</div><div class=\"value\">")
            .Append(Encode(value))
            .AppendLine("</div></div>");
    }

    private static string Encode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private sealed class ResponsibleDetector
    {
        public string Name { get; init; } = "";
        public double Score { get; init; }
    }

    private sealed class ActiveEvent
    {
        public long StartMinute { get; init; }
        public long LastMinute { get; set; }
        public long DetectionCount { get; set; }
    }
}

public sealed class CleanEvaluationReport
{
    public DateTime GeneratedAtUtc { get; set; }
    public string DetectionsFile { get; set; } = "";
    public string ErrorsFile { get; set; } = "";
    public string SummaryFile { get; set; } = "";
    public long TotalReadings { get; set; }
    public long TotalDetectionRecords { get; set; }
    public long InvalidDetectionLines { get; set; }
    public long TotalGroupedEvents { get; set; }
    public long TotalDetectorGroupedEvents { get; set; }
    public long TotalErrorRecords { get; set; }
    public double DetectionRate { get; set; }
    public double EventsPerMillionReadings { get; set; }
    public List<TagStats> Tags { get; set; } = [];
    public List<DetectorStats> Detectors { get; set; } = [];
    public List<TagDetectorStats> TagDetectors { get; set; } = [];
    public List<DetectorCombinationStats> DetectorCombinations { get; set; } = [];
}

public sealed class TagStats
{
    public string TagName { get; set; } = "";
    public long Detections { get; set; }
    public long Events { get; set; }
    public double ScoreSum { get; set; }
    public double MaxScore { get; set; }
    public long LongestEventMinutes { get; set; }
    public long MaxDetectionsInEvent { get; set; }
    public long DetectionsWithoutResponsibleDetector { get; set; }
}

public sealed class DetectorStats
{
    public string DetectorName { get; set; } = "";
    public long DetectionRecords { get; set; }
    public long Events { get; set; }
    public double ScoreSum { get; set; }
    public double MaxScore { get; set; }
    public long LongestEventMinutes { get; set; }
    public long MaxDetectionsInEvent { get; set; }
}

public sealed class TagDetectorStats
{
    public string TagName { get; set; } = "";
    public string DetectorName { get; set; } = "";
    public long DetectionRecords { get; set; }
    public long Events { get; set; }
    public double ScoreSum { get; set; }
    public double MaxScore { get; set; }
    public long LongestEventMinutes { get; set; }
    public long MaxDetectionsInEvent { get; set; }
}

public sealed class DetectorCombinationStats
{
    public string Combination { get; set; } = "";
    public long DetectionRecords { get; set; }
}