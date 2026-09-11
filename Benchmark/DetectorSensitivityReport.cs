using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IAApi.Benchmark;

/// <summary>
/// Cruza as categorias comportamentais dos falsos positivos
/// com os detectores que participaram de cada evento.
///
/// Este relatório é somente diagnóstico:
/// não altera detectores, pesos, thresholds ou o ensemble.
/// </summary>
public sealed class DetectorSensitivityReport
{
    public async Task AnalyzeAsync(
        string resultDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(resultDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Diretório de resultados não encontrado: {resultDirectory}");
        }

        var inputFile =
            new DirectoryInfo(resultDirectory)
                .GetFiles("false_positive_structural_analysis_*.csv")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

        if (inputFile is null)
        {
            throw new FileNotFoundException(
                "Nenhum arquivo false_positive_structural_analysis_*.csv foi encontrado.");
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" RELATÓRIO DE SENSIBILIDADE DOS DETECTORES");
        Console.WriteLine("========================================");
        Console.WriteLine($"Arquivo: {inputFile.FullName}");

        var events =
            ReadEvents(
                inputFile.FullName,
                cancellationToken);

        Console.WriteLine($"Eventos lidos: {events.Count:N0}");

        var summary =
            BuildSummary(events);

        var identifier =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);

        var summaryJsonPath =
            Path.Combine(
                resultDirectory,
                $"detector_sensitivity_summary_{identifier}.json");

        var byCategoryCsvPath =
            Path.Combine(
                resultDirectory,
                $"detector_sensitivity_by_category_{identifier}.csv");

        var byTagCsvPath =
            Path.Combine(
                resultDirectory,
                $"detector_sensitivity_by_tag_{identifier}.csv");

        var combinationsCsvPath =
            Path.Combine(
                resultDirectory,
                $"detector_sensitivity_combinations_{identifier}.csv");

        var htmlPath =
            Path.Combine(
                resultDirectory,
                $"detector_sensitivity_report_{identifier}.html");

        await File.WriteAllTextAsync(
            summaryJsonPath,
            JsonSerializer.Serialize(
                summary,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                }),
            cancellationToken);

        await File.WriteAllTextAsync(
            byCategoryCsvPath,
            CreateByCategoryCsv(summary.ByCategory),
            cancellationToken);

        await File.WriteAllTextAsync(
            byTagCsvPath,
            CreateByTagCsv(summary.ByTag),
            cancellationToken);

        await File.WriteAllTextAsync(
            combinationsCsvPath,
            CreateCombinationsCsv(summary.Combinations),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(summary),
            cancellationToken);

        PrintSummary(
            summary,
            summaryJsonPath,
            byCategoryCsvPath,
            byTagCsvPath,
            combinationsCsvPath,
            htmlPath);
    }

    private static List<DetectorSensitivityEvent> ReadEvents(
        string csvPath,
        CancellationToken cancellationToken)
    {
        var result =
            new List<DetectorSensitivityEvent>();

        using var reader =
            new StreamReader(csvPath);

        var headerLine =
            reader.ReadLine();

        if (headerLine is null)
        {
            return result;
        }

        var headers =
            ParseCsvLine(headerLine);

        var indexes =
            headers
                .Select(
                    (name, index) =>
                        new
                        {
                            Name = name.Trim(),
                            Index = index
                        })
                .ToDictionary(
                    item => item.Name,
                    item => item.Index,
                    StringComparer.OrdinalIgnoreCase);

        RequireColumn(indexes, "tag");
        RequireColumn(indexes, "duracaoMinutos");
        RequireColumn(indexes, "detectores");
        RequireColumn(indexes, "retornouDepois");
        RequireColumn(indexes, "categoriaComportamental");
        RequireColumn(indexes, "confiancaClassificacao");

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields =
                ParseCsvLine(line);

            var detectors =
                GetField(
                        fields,
                        indexes["detectores"])
                    .Split(
                        '|',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (detectors.Count == 0)
            {
                detectors.Add("SEM_DETECTOR_INFORMADO");
            }

            result.Add(
                new DetectorSensitivityEvent
                {
                    TagName =
                        GetField(
                            fields,
                            indexes["tag"]),

                    DurationMinutes =
                        ParseInt(
                            GetField(
                                fields,
                                indexes["duracaoMinutos"])),

                    Detectors =
                        detectors,

                    ReturnedAfterEvent =
                        ParseBool(
                            GetField(
                                fields,
                                indexes["retornouDepois"])),

                    Category =
                        GetField(
                            fields,
                            indexes["categoriaComportamental"]),

                    ClassificationConfidence =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["confiancaClassificacao"]))
                });
        }

        return result;
    }

    private static DetectorSensitivitySummary BuildSummary(
        IReadOnlyCollection<DetectorSensitivityEvent> events)
    {
        var totalEvents =
            events.Count;

        var detectorSummaries =
            events
                .SelectMany(
                    item =>
                        item.Detectors.Select(
                            detector =>
                                new
                                {
                                    Detector = detector,
                                    Event = item
                                }))
                .GroupBy(
                    item => item.Detector,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                    {
                        var detectorEvents =
                            group
                                .Select(item => item.Event)
                                .ToList();

                        return new DetectorOverallSensitivity
                        {
                            Detector =
                                group.Key,

                            EventCount =
                                detectorEvents.Count,

                            EventParticipationRate =
                                totalEvents > 0
                                    ? detectorEvents.Count /
                                      (double)totalEvents
                                    : 0,

                            TagCount =
                                detectorEvents
                                    .Select(item => item.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount(),

                            CategoryCount =
                                detectorEvents
                                    .Select(item => item.Category)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount(),

                            AverageDurationMinutes =
                                detectorEvents.Average(
                                    item => item.DurationMinutes),

                            OneMinuteEventCount =
                                detectorEvents.LongCount(
                                    item => item.DurationMinutes == 1),

                            ReturnedEventCount =
                                detectorEvents.LongCount(
                                    item => item.ReturnedAfterEvent),

                            SustainedShiftEventCount =
                                detectorEvents.LongCount(
                                    item =>
                                        string.Equals(
                                            item.Category,
                                            "Mudança sustentada de patamar",
                                            StringComparison.OrdinalIgnoreCase))
                        };
                    })
                .OrderByDescending(
                    item => item.EventCount)
                .ToList();

        var categoryTotals =
            events
                .GroupBy(
                    item => item.Category,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var byCategory =
            events
                .SelectMany(
                    item =>
                        item.Detectors.Select(
                            detector =>
                                new
                                {
                                    Detector = detector,
                                    Event = item
                                }))
                .GroupBy(
                    item =>
                        new
                        {
                            item.Event.Category,
                            item.Detector
                        })
                .Select(
                    group =>
                    {
                        var categoryTotal =
                            categoryTotals.TryGetValue(
                                group.Key.Category,
                                out var total)
                                ? total
                                : 0;

                        return new DetectorCategorySensitivity
                        {
                            Category =
                                group.Key.Category,

                            Detector =
                                group.Key.Detector,

                            EventCount =
                                group.LongCount(),

                            CategoryParticipationRate =
                                categoryTotal > 0
                                    ? group.LongCount() /
                                      (double)categoryTotal
                                    : 0,

                            TagCount =
                                group
                                    .Select(item => item.Event.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount(),

                            AverageConfidence =
                                group.Average(
                                    item =>
                                        item.Event.ClassificationConfidence),

                            AverageDurationMinutes =
                                group.Average(
                                    item =>
                                        item.Event.DurationMinutes)
                        };
                    })
                .OrderBy(
                    item => item.Category,
                    StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(
                    item => item.EventCount)
                .ToList();

        var byTag =
            events
                .SelectMany(
                    item =>
                        item.Detectors.Select(
                            detector =>
                                new
                                {
                                    Detector = detector,
                                    Event = item
                                }))
                .GroupBy(
                    item =>
                        new
                        {
                            item.Event.TagName,
                            item.Detector,
                            item.Event.Category
                        })
                .Select(
                    group =>
                        new DetectorTagSensitivity
                        {
                            TagName =
                                group.Key.TagName,

                            Detector =
                                group.Key.Detector,

                            Category =
                                group.Key.Category,

                            EventCount =
                                group.LongCount(),

                            AverageDurationMinutes =
                                group.Average(
                                    item =>
                                        item.Event.DurationMinutes),

                            ReturnedEventCount =
                                group.LongCount(
                                    item =>
                                        item.Event.ReturnedAfterEvent)
                        })
                .OrderByDescending(
                    item => item.EventCount)
                .ThenBy(
                    item => item.TagName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        var combinations =
            events
                .GroupBy(
                    item =>
                        string.Join(
                            " + ",
                            item.Detectors),
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new DetectorCombinationSensitivity
                        {
                            Combination =
                                group.Key,

                            DetectorCount =
                                group.First().Detectors.Count,

                            EventCount =
                                group.LongCount(),

                            EventRate =
                                totalEvents > 0
                                    ? group.LongCount() /
                                      (double)totalEvents
                                    : 0,

                            TagCount =
                                group
                                    .Select(item => item.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount(),

                            CategoryCount =
                                group
                                    .Select(item => item.Category)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount(),

                            PrincipalCategory =
                                group
                                    .GroupBy(
                                        item => item.Category,
                                        StringComparer.OrdinalIgnoreCase)
                                    .OrderByDescending(
                                        categoryGroup =>
                                            categoryGroup.Count())
                                    .First()
                                    .Key
                        })
                .OrderByDescending(
                    item => item.EventCount)
                .ToList();

        return new DetectorSensitivitySummary
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            TotalEvents =
                totalEvents,

            DetectorCount =
                detectorSummaries.Count,

            CategoryCount =
                events
                    .Select(item => item.Category)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count(),

            TagCount =
                events
                    .Select(item => item.TagName)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count(),

            Detectors =
                detectorSummaries,

            ByCategory =
                byCategory,

            ByTag =
                byTag,

            Combinations =
                combinations
        };
    }

    private static string CreateByCategoryCsv(
        IEnumerable<DetectorCategorySensitivity> items)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "categoria,detector,eventos,participacaoNaCategoria," +
            "tagsAfetadas,confiancaMedia,duracaoMediaMinutos");

        foreach (var item in items)
        {
            builder
                .Append(Csv(item.Category))
                .Append(',')
                .Append(Csv(item.Detector))
                .Append(',')
                .Append(item.EventCount)
                .Append(',')
                .Append(Number(item.CategoryParticipationRate))
                .Append(',')
                .Append(item.TagCount)
                .Append(',')
                .Append(Number(item.AverageConfidence))
                .Append(',')
                .AppendLine(Number(item.AverageDurationMinutes));
        }

        return builder.ToString();
    }

    private static string CreateByTagCsv(
        IEnumerable<DetectorTagSensitivity> items)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "tag,detector,categoria,eventos,duracaoMediaMinutos,eventosComRetorno");

        foreach (var item in items)
        {
            builder
                .Append(Csv(item.TagName))
                .Append(',')
                .Append(Csv(item.Detector))
                .Append(',')
                .Append(Csv(item.Category))
                .Append(',')
                .Append(item.EventCount)
                .Append(',')
                .Append(Number(item.AverageDurationMinutes))
                .Append(',')
                .AppendLine(item.ReturnedEventCount.ToString());
        }

        return builder.ToString();
    }

    private static string CreateCombinationsCsv(
        IEnumerable<DetectorCombinationSensitivity> items)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "combinacao,quantidadeDetectores,eventos,percentualEventos," +
            "tags,categorias,categoriaPrincipal");

        foreach (var item in items)
        {
            builder
                .Append(Csv(item.Combination))
                .Append(',')
                .Append(item.DetectorCount)
                .Append(',')
                .Append(item.EventCount)
                .Append(',')
                .Append(Number(item.EventRate))
                .Append(',')
                .Append(item.TagCount)
                .Append(',')
                .Append(item.CategoryCount)
                .Append(',')
                .AppendLine(Csv(item.PrincipalCategory));
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        DetectorSensitivitySummary summary)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Sensibilidade dos detectores</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;" +
            "margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1500px;margin:auto}.grid{display:grid;" +
            "grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:14px}");
        html.AppendLine(
            ".card,section{background:#fff;border-radius:12px;padding:18px;" +
            "margin:16px 0;box-shadow:0 1px 4px #0002}");
        html.AppendLine(
            ".value{font-size:30px;font-weight:800}" +
            "table{width:100%;border-collapse:collapse;font-size:13px}" +
            "th,td{text-align:left;padding:9px;border-bottom:1px solid #e5e7eb}" +
            "th{background:#f9fafb}");
        html.AppendLine("</style></head><body><div class=\"container\">");

        html.AppendLine("<h1>Relatório de sensibilidade dos detectores</h1>");
        html.AppendLine("<div class=\"grid\">");

        AppendCard(
            html,
            "Eventos",
            summary.TotalEvents.ToString("N0"));

        AppendCard(
            html,
            "Detectores",
            summary.DetectorCount.ToString("N0"));

        AppendCard(
            html,
            "Categorias",
            summary.CategoryCount.ToString("N0"));

        AppendCard(
            html,
            "Tags",
            summary.TagCount.ToString("N0"));

        html.AppendLine("</div>");

        html.AppendLine(
            "<section><h2>Resumo por detector</h2>" +
            "<table><thead><tr><th>Detector</th><th>Eventos</th>" +
            "<th>Participação</th><th>Tags</th><th>Categorias</th>" +
            "<th>1 minuto</th><th>Com retorno</th><th>Patamar sustentado</th>" +
            "</tr></thead><tbody>");

        foreach (var item in summary.Detectors)
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Detector))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.EventParticipationRate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.CategoryCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.OneMinuteEventRate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.ReturnedEventRate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.SustainedShiftEventRate.ToString("P1"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Participação por categoria</h2>" +
            "<table><thead><tr><th>Categoria</th><th>Detector</th>" +
            "<th>Eventos</th><th>Participação</th><th>Tags</th>" +
            "<th>Duração média</th></tr></thead><tbody>");

        foreach (var item in summary.ByCategory.Take(200))
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Category))
                .Append("</td><td>")
                .Append(Encode(item.Detector))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.CategoryParticipationRate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.AverageDurationMinutes.ToString("F1"))
                .AppendLine(" min</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Combinações mais frequentes</h2>" +
            "<table><thead><tr><th>Combinação</th><th>Eventos</th>" +
            "<th>Participação</th><th>Tags</th><th>Categoria principal</th>" +
            "</tr></thead><tbody>");

        foreach (var item in summary.Combinations.Take(100))
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Combination))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.EventRate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .Append("</td><td>")
                .Append(Encode(item.PrincipalCategory))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
        html.AppendLine("</div></body></html>");

        return html.ToString();
    }

    private static void PrintSummary(
        DetectorSensitivitySummary summary,
        string summaryJsonPath,
        string byCategoryCsvPath,
        string byTagCsvPath,
        string combinationsCsvPath,
        string htmlPath)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" RELATÓRIO FINALIZADO");
        Console.WriteLine("========================================");
        Console.WriteLine($"Eventos: {summary.TotalEvents:N0}");
        Console.WriteLine($"Detectores: {summary.DetectorCount:N0}");
        Console.WriteLine($"Categorias: {summary.CategoryCount:N0}");
        Console.WriteLine($"Tags: {summary.TagCount:N0}");
        Console.WriteLine();

        foreach (var detector in summary.Detectors.Take(20))
        {
            Console.WriteLine(
                $"{detector.Detector,-45} " +
                $"{detector.EventCount,8:N0} eventos");
        }

        Console.WriteLine();
        Console.WriteLine($"JSON: {summaryJsonPath}");
        Console.WriteLine($"Por categoria: {byCategoryCsvPath}");
        Console.WriteLine($"Por tag: {byTagCsvPath}");
        Console.WriteLine($"Combinações: {combinationsCsvPath}");
        Console.WriteLine($"HTML: {htmlPath}");
        Console.WriteLine("========================================");
    }

    private static List<string> ParseCsvLine(
        string line)
    {
        var fields =
            new List<string>();

        var current =
            new StringBuilder();

        var insideQuotes =
            false;

        for (var index = 0;
             index < line.Length;
             index++)
        {
            var character =
                line[index];

            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                insideQuotes =
                    !insideQuotes;

                continue;
            }

            if (character == ',' &&
                !insideQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        fields.Add(current.ToString());

        return fields;
    }

    private static void RequireColumn(
        IReadOnlyDictionary<string, int> indexes,
        string columnName)
    {
        if (!indexes.ContainsKey(columnName))
        {
            throw new InvalidDataException(
                $"A coluna obrigatória '{columnName}' não existe no CSV.");
        }
    }

    private static string GetField(
        IReadOnlyList<string> fields,
        int index)
    {
        return index >= 0 &&
               index < fields.Count
            ? fields[index]
            : "";
    }

    private static int ParseInt(
        string value)
    {
        return int.TryParse(
                   value,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out var result)
            ? result
            : 0;
    }

    private static double ParseDouble(
        string value)
    {
        return double.TryParse(
                   value,
                   NumberStyles.Float |
                   NumberStyles.AllowThousands,
                   CultureInfo.InvariantCulture,
                   out var result)
            ? result
            : 0;
    }

    private static bool ParseBool(
        string value)
    {
        return bool.TryParse(
                   value,
                   out var result) &&
               result;
    }

    private static string Csv(
        string value)
    {
        return
            "\"" +
            value.Replace(
                "\"",
                "\"\"") +
            "\"";
    }

    private static string Number(
        double value)
    {
        return value.ToString(
            "G17",
            CultureInfo.InvariantCulture);
    }

    private static string Encode(
        string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private static void AppendCard(
        StringBuilder html,
        string title,
        string value)
    {
        html
            .Append("<div class=\"card\"><div>")
            .Append(Encode(title))
            .Append("</div><div class=\"value\">")
            .Append(Encode(value))
            .AppendLine("</div></div>");
    }
}

public sealed class DetectorSensitivityEvent
{
    public string TagName { get; set; } = "";
    public int DurationMinutes { get; set; }
    public List<string> Detectors { get; set; } = [];
    public bool ReturnedAfterEvent { get; set; }
    public string Category { get; set; } = "";
    public double ClassificationConfidence { get; set; }
}

public sealed class DetectorOverallSensitivity
{
    public string Detector { get; set; } = "";
    public long EventCount { get; set; }
    public double EventParticipationRate { get; set; }
    public long TagCount { get; set; }
    public long CategoryCount { get; set; }
    public double AverageDurationMinutes { get; set; }
    public long OneMinuteEventCount { get; set; }
    public long ReturnedEventCount { get; set; }
    public long SustainedShiftEventCount { get; set; }

    public double OneMinuteEventRate =>
        EventCount > 0
            ? OneMinuteEventCount / (double)EventCount
            : 0;

    public double ReturnedEventRate =>
        EventCount > 0
            ? ReturnedEventCount / (double)EventCount
            : 0;

    public double SustainedShiftEventRate =>
        EventCount > 0
            ? SustainedShiftEventCount / (double)EventCount
            : 0;
}

public sealed class DetectorCategorySensitivity
{
    public string Category { get; set; } = "";
    public string Detector { get; set; } = "";
    public long EventCount { get; set; }
    public double CategoryParticipationRate { get; set; }
    public long TagCount { get; set; }
    public double AverageConfidence { get; set; }
    public double AverageDurationMinutes { get; set; }
}

public sealed class DetectorTagSensitivity
{
    public string TagName { get; set; } = "";
    public string Detector { get; set; } = "";
    public string Category { get; set; } = "";
    public long EventCount { get; set; }
    public double AverageDurationMinutes { get; set; }
    public long ReturnedEventCount { get; set; }
}

public sealed class DetectorCombinationSensitivity
{
    public string Combination { get; set; } = "";
    public int DetectorCount { get; set; }
    public long EventCount { get; set; }
    public double EventRate { get; set; }
    public long TagCount { get; set; }
    public long CategoryCount { get; set; }
    public string PrincipalCategory { get; set; } = "";
}

public sealed class DetectorSensitivitySummary
{
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalEvents { get; set; }
    public int DetectorCount { get; set; }
    public int CategoryCount { get; set; }
    public int TagCount { get; set; }

    public List<DetectorOverallSensitivity> Detectors { get; set; } = [];
    public List<DetectorCategorySensitivity> ByCategory { get; set; } = [];
    public List<DetectorTagSensitivity> ByTag { get; set; } = [];
    public List<DetectorCombinationSensitivity> Combinations { get; set; } = [];
}