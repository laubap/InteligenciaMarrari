using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace IAApi.Benchmark;

/// <summary>
/// Investiga possíveis falsos positivos do banco limpo.
///
/// O serviço:
/// 1. encontra os eventos da tag no clean_detections;
/// 2. identifica os detectores responsáveis;
/// 3. busca valores antes, durante e depois no SQLite;
/// 4. gera relatório HTML e CSV.
/// </summary>
public sealed class CalibrationInvestigator
{
    private const int EventGapMinutes = 5;
    private const int ContextBeforeMinutes = 30;
    private const int ContextAfterMinutes = 30;

    public async Task InvestigateAsync(
        string cleanDatabasePath,
        string detectionsPath,
        string tagName,
        int maximumEvents,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            cleanDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            detectionsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            tagName);

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

        var events =
            await ReadEventsAsync(
                detectionsPath,
                tagName,
                cancellationToken);

        if (events.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Nenhum evento foi encontrado para a tag '{tagName}'."
            );
            return;
        }

        var selectedEvents =
            events
                .OrderByDescending(
                    item => item.MaximumScore)
                .ThenByDescending(
                    item => item.EndMinute -
                            item.StartMinute)
                .Take(maximumEvents)
                .ToList();

        var investigations =
            new List<CalibrationInvestigationEvent>();

        foreach (var eventItem in selectedEvents)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var context =
                ReadContext(
                    cleanDatabasePath,
                    tagName,
                    Math.Max(
                        0,
                        eventItem.StartMinute -
                        ContextBeforeMinutes),
                    eventItem.EndMinute +
                    ContextAfterMinutes);

            investigations.Add(
                BuildInvestigation(
                    eventItem,
                    context));
        }

        Directory.CreateDirectory(
            outputDirectory);

        var safeTag =
            string.Concat(
                tagName.Select(
                    character =>
                        char.IsLetterOrDigit(character)
                            ? character
                            : '_'));

        var id =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss");

        var csvPath =
            Path.Combine(
                outputDirectory,
                $"investigation_{safeTag}_{id}.csv");

        var htmlPath =
            Path.Combine(
                outputDirectory,
                $"investigation_{safeTag}_{id}.html");

        await File.WriteAllTextAsync(
            csvPath,
            CreateCsv(
                tagName,
                investigations),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(
                tagName,
                investigations),
            cancellationToken);

        PrintConsole(
            tagName,
            events.Count,
            investigations,
            csvPath,
            htmlPath);
    }

    private static async Task<List<CalibrationEvent>>
        ReadEventsAsync(
            string detectionsPath,
            string tagName,
            CancellationToken cancellationToken)
    {
        var events =
            new List<CalibrationEvent>();

        CalibrationEvent? current =
            null;

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

            if (!root.TryGetProperty(
                    "tagName",
                    out var tagElement))
            {
                continue;
            }

            var currentTag =
                tagElement.GetString() ?? "";

            if (!string.Equals(
                    currentTag,
                    tagName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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

            if (current is null)
            {
                current =
                    CreateEvent(
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

            current =
                CreateEvent(
                    minute,
                    score,
                    detectors);
        }

        if (current is not null)
        {
            events.Add(current);
        }

        return events;
    }

    private static CalibrationEvent CreateEvent(
        int minute,
        double score,
        IReadOnlyCollection<string> detectors)
    {
        return new CalibrationEvent
        {
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
            var voted =
                detectorElement.TryGetProperty(
                    "votouAnomalia",
                    out var votedElement) &&
                votedElement.ValueKind ==
                    JsonValueKind.True;

            if (!voted)
            {
                continue;
            }

            if (!detectorElement.TryGetProperty(
                    "nome",
                    out var nameElement))
            {
                continue;
            }

            var name =
                nameElement.GetString();

            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name);
            }
        }

        return result;
    }

    private static List<CalibrationSample>
        ReadContext(
            string databasePath,
            string tagName,
            int startMinute,
            int endMinute)
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource =
                    databasePath,
                Mode =
                    SqliteOpenMode.ReadOnly
            }
            .ToString();

        var result =
            new List<CalibrationSample>();

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

        command.Parameters.AddWithValue(
            "$tagName",
            tagName);

        command.Parameters.AddWithValue(
            "$startMinute",
            startMinute);

        command.Parameters.AddWithValue(
            "$endMinute",
            endMinute);

        using var reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new CalibrationSample
                {
                    MinuteIndex =
                        reader.GetInt32(0),

                    Value =
                        reader.GetDouble(1),

                    Quality =
                        reader.GetInt32(2)
                });
        }

        return result;
    }

    private static CalibrationInvestigationEvent
        BuildInvestigation(
            CalibrationEvent eventItem,
            IReadOnlyList<CalibrationSample> context)
    {
        var before =
            context
                .Where(
                    item =>
                        item.MinuteIndex <
                        eventItem.StartMinute)
                .ToList();

        var during =
            context
                .Where(
                    item =>
                        item.MinuteIndex >=
                            eventItem.StartMinute &&
                        item.MinuteIndex <=
                            eventItem.EndMinute)
                .ToList();

        var after =
            context
                .Where(
                    item =>
                        item.MinuteIndex >
                        eventItem.EndMinute)
                .ToList();

        return new CalibrationInvestigationEvent
        {
            StartMinute =
                eventItem.StartMinute,

            EndMinute =
                eventItem.EndMinute,

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
                Summarize(before),

            During =
                Summarize(during),

            After =
                Summarize(after),

            Samples =
                context.ToList()
        };
    }

    private static CalibrationWindowSummary Summarize(
        IReadOnlyList<CalibrationSample> samples)
    {
        if (samples.Count == 0)
        {
            return new CalibrationWindowSummary();
        }

        var values =
            samples
                .Select(
                    item => item.Value)
                .Where(
                    double.IsFinite)
                .ToArray();

        if (values.Length == 0)
        {
            return new CalibrationWindowSummary();
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

        return new CalibrationWindowSummary
        {
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

    private static string CreateCsv(
        string tagName,
        IReadOnlyList<CalibrationInvestigationEvent> events)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "tag,evento,inicioMinuto,fimMinuto,minuto,periodo," +
            "valor,qualidade,detectores,scoreMaximo");

        for (var eventIndex = 0;
             eventIndex < events.Count;
             eventIndex++)
        {
            var item =
                events[eventIndex];

            foreach (var sample in item.Samples)
            {
                var period =
                    sample.MinuteIndex <
                        item.StartMinute
                        ? "antes"
                        : sample.MinuteIndex >
                            item.EndMinute
                            ? "depois"
                            : "durante";

                builder.Append(
                        EscapeCsv(tagName))
                    .Append(',')
                    .Append(eventIndex + 1)
                    .Append(',')
                    .Append(item.StartMinute)
                    .Append(',')
                    .Append(item.EndMinute)
                    .Append(',')
                    .Append(sample.MinuteIndex)
                    .Append(',')
                    .Append(period)
                    .Append(',')
                    .Append(
                        sample.Value.ToString(
                            "G17",
                            CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(sample.Quality)
                    .Append(',')
                    .Append(
                        EscapeCsv(
                            string.Join(
                                " | ",
                                item.Detectors)))
                    .Append(',')
                    .AppendLine(
                        item.MaximumScore.ToString(
                            "F6",
                            CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        string tagName,
        IReadOnlyList<CalibrationInvestigationEvent> events)
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
            "<title>Investigação de calibração</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Inter,Segoe UI,Arial,sans-serif;" +
            "background:#f3f4f6;margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1500px;margin:auto}");
        html.AppendLine(
            ".event{background:white;border-radius:12px;padding:18px;" +
            "margin:18px 0;box-shadow:0 1px 4px #0002}");
        html.AppendLine(
            ".grid{display:grid;grid-template-columns:" +
            "repeat(auto-fit,minmax(180px,1fr));gap:12px}");
        html.AppendLine(
            ".card{background:#f9fafb;border-radius:8px;padding:12px}");
        html.AppendLine(
            "table{width:100%;border-collapse:collapse;font-size:13px}");
        html.AppendLine(
            "th,td{text-align:left;padding:8px;" +
            "border-bottom:1px solid #e5e7eb}");
        html.AppendLine(
            ".during{background:#fef3c7}");
        html.AppendLine("</style></head><body>");
        html.AppendLine("<div class=\"container\">");
        html.Append("<h1>Investigação: <code>")
            .Append(Encode(tagName))
            .AppendLine("</code></h1>");

        for (var eventIndex = 0;
             eventIndex < events.Count;
             eventIndex++)
        {
            var item =
                events[eventIndex];

            html.Append("<div class=\"event\"><h2>Evento ")
                .Append(eventIndex + 1)
                .AppendLine("</h2>");

            html.Append("<p><strong>Detectores:</strong> ")
                .Append(
                    Encode(
                        string.Join(
                            ", ",
                            item.Detectors)))
                .AppendLine("</p>");

            html.AppendLine("<div class=\"grid\">");

            AppendCard(
                html,
                "Início",
                item.StartMinute.ToString());

            AppendCard(
                html,
                "Fim",
                item.EndMinute.ToString());

            AppendCard(
                html,
                "Duração",
                $"{item.EndMinute - item.StartMinute + 1} min");

            AppendCard(
                html,
                "Detecções",
                item.DetectionRecords.ToString());

            AppendCard(
                html,
                "Score máximo",
                item.MaximumScore.ToString("F2"));

            html.AppendLine("</div>");

            html.AppendLine(
                "<h3>Resumo antes, durante e depois</h3>");
            html.AppendLine(
                "<table><thead><tr><th>Período</th><th>Pontos</th>" +
                "<th>Média</th><th>Mínimo</th><th>Máximo</th>" +
                "<th>Desvio</th><th>Primeiro</th><th>Último</th>" +
                "</tr></thead><tbody>");

            AppendSummaryRow(
                html,
                "Antes",
                item.Before);

            AppendSummaryRow(
                html,
                "Durante",
                item.During);

            AppendSummaryRow(
                html,
                "Depois",
                item.After);

            html.AppendLine("</tbody></table>");

            html.AppendLine(
                "<h3>Leituras no contexto</h3>");
            html.AppendLine(
                "<table><thead><tr><th>Minuto</th><th>Período</th>" +
                "<th>Valor</th><th>Qualidade</th></tr></thead><tbody>");

            foreach (var sample in item.Samples)
            {
                var period =
                    sample.MinuteIndex <
                        item.StartMinute
                        ? "Antes"
                        : sample.MinuteIndex >
                            item.EndMinute
                            ? "Depois"
                            : "Durante";

                html.Append(
                        period == "Durante"
                            ? "<tr class=\"during\">"
                            : "<tr>")
                    .Append("<td>")
                    .Append(sample.MinuteIndex)
                    .Append("</td><td>")
                    .Append(period)
                    .Append("</td><td>")
                    .Append(
                        sample.Value.ToString("F6"))
                    .Append("</td><td>")
                    .Append(sample.Quality)
                    .AppendLine("</td></tr>");
            }

            html.AppendLine(
                "</tbody></table></div>");
        }

        html.AppendLine("</div></body></html>");

        return html.ToString();
    }

    private static void AppendSummaryRow(
        StringBuilder html,
        string period,
        CalibrationWindowSummary summary)
    {
        html.Append("<tr><td>")
            .Append(period)
            .Append("</td><td>")
            .Append(summary.Count)
            .Append("</td><td>")
            .Append(summary.Mean.ToString("F6"))
            .Append("</td><td>")
            .Append(summary.Minimum.ToString("F6"))
            .Append("</td><td>")
            .Append(summary.Maximum.ToString("F6"))
            .Append("</td><td>")
            .Append(summary.StandardDeviation.ToString("F6"))
            .Append("</td><td>")
            .Append(summary.FirstValue.ToString("F6"))
            .Append("</td><td>")
            .Append(summary.LastValue.ToString("F6"))
            .AppendLine("</td></tr>");
    }

    private static void AppendCard(
        StringBuilder html,
        string title,
        string value)
    {
        html.Append("<div class=\"card\"><strong>")
            .Append(Encode(title))
            .Append("</strong><div>")
            .Append(Encode(value))
            .AppendLine("</div></div>");
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

    private static string Encode(
        string value)
    {
        return System.Net.WebUtility
            .HtmlEncode(value);
    }

    private static void PrintConsole(
        string tagName,
        int totalEvents,
        IReadOnlyList<CalibrationInvestigationEvent> investigations,
        string csvPath,
        string htmlPath)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            " INVESTIGAÇÃO DE FALSOS POSITIVOS");
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            $"Tag: {tagName}");
        Console.WriteLine(
            $"Eventos encontrados: {totalEvents:N0}");
        Console.WriteLine(
            $"Eventos analisados: {investigations.Count:N0}");

        for (var index = 0;
             index < investigations.Count;
             index++)
        {
            var item =
                investigations[index];

            Console.WriteLine();
            Console.WriteLine(
                $"Evento {index + 1}: " +
                $"{item.StartMinute}–{item.EndMinute}");

            Console.WriteLine(
                $"Detectores: " +
                $"{string.Join(", ", item.Detectors)}");

            Console.WriteLine(
                $"Score máximo: " +
                $"{item.MaximumScore:F2}");

            Console.WriteLine(
                $"Antes: média {item.Before.Mean:F4} | " +
                $"desvio {item.Before.StandardDeviation:F4}");

            Console.WriteLine(
                $"Durante: média {item.During.Mean:F4} | " +
                $"desvio {item.During.StandardDeviation:F4}");

            Console.WriteLine(
                $"Depois: média {item.After.Mean:F4} | " +
                $"desvio {item.After.StandardDeviation:F4}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"CSV: {csvPath}");
        Console.WriteLine(
            $"HTML: {htmlPath}");
        Console.WriteLine(
            "========================================");
    }

    private sealed class CalibrationEvent
    {
        public int StartMinute { get; init; }

        public int EndMinute { get; set; }

        public int DetectionRecords { get; set; }

        public double MaximumScore { get; set; }

        public required HashSet<string>
            Detectors { get; init; }
    }
}

public sealed class CalibrationInvestigationEvent
{
    public int StartMinute { get; set; }

    public int EndMinute { get; set; }

    public int DetectionRecords { get; set; }

    public double MaximumScore { get; set; }

    public List<string> Detectors { get; set; } = [];

    public CalibrationWindowSummary Before { get; set; } = new();

    public CalibrationWindowSummary During { get; set; } = new();

    public CalibrationWindowSummary After { get; set; } = new();

    public List<CalibrationSample> Samples { get; set; } = [];
}

public sealed class CalibrationWindowSummary
{
    public int Count { get; set; }

    public double Minimum { get; set; }

    public double Maximum { get; set; }

    public double Mean { get; set; }

    public double StandardDeviation { get; set; }

    public double FirstValue { get; set; }

    public double LastValue { get; set; }
}

public sealed class CalibrationSample
{
    public int MinuteIndex { get; set; }

    public double Value { get; set; }

    public int Quality { get; set; }
}