using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace IAApi.Benchmark;

public sealed class ConfidenceBenchmarkReportWriter
{
    public async Task<ConfidenceBenchmarkOutputPaths> WriteAsync(
        ConfidenceBenchmarkReport report,
        string resultDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(
            resultDirectory);

        var identifier =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);

        var jsonPath =
            Path.Combine(
                resultDirectory,
                $"confidence_benchmark_summary_{identifier}.json");

        var categoryCsvPath =
            Path.Combine(
                resultDirectory,
                $"confidence_benchmark_by_category_{identifier}.csv");

        var isolatedCsvPath =
            Path.Combine(
                resultDirectory,
                $"confidence_benchmark_isolated_detectors_{identifier}.csv");

        var familyCsvPath =
            Path.Combine(
                resultDirectory,
                $"confidence_benchmark_by_family_count_{identifier}.csv");

        var htmlPath =
            Path.Combine(
                resultDirectory,
                $"confidence_benchmark_report_{identifier}.html");

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
            categoryCsvPath,
            CreateCategoryCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            isolatedCsvPath,
            CreateIsolatedCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            familyCsvPath,
            CreateFamilyCountCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(report),
            cancellationToken);

        return new ConfidenceBenchmarkOutputPaths
        {
            JsonPath = jsonPath,
            CategoryCsvPath = categoryCsvPath,
            IsolatedDetectorCsvPath = isolatedCsvPath,
            FamilyCountCsvPath = familyCsvPath,
            HtmlPath = htmlPath
        };
    }

    public void Print(
        ConfidenceBenchmarkReport report,
        ConfidenceBenchmarkOutputPaths paths)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" CONFIDENCE ENGINE BENCHMARK");
        Console.WriteLine("========================================");
        Console.WriteLine($"Eventos: {report.TotalEvents:N0}");
        Console.WriteLine(
            $"Suspeitos: {report.SuspiciousEvents:N0} " +
            $"({report.SuspiciousRate:P1})");
        Console.WriteLine(
            $"Prováveis: {report.ProbableEvents:N0} " +
            $"({report.ProbableRate:P1})");
        Console.WriteLine(
            $"Confirmados: {report.ConfirmedEvents:N0} " +
            $"({report.ConfirmedRate:P1})");
        Console.WriteLine();
        Console.WriteLine($"JSON: {paths.JsonPath}");
        Console.WriteLine($"Categorias: {paths.CategoryCsvPath}");
        Console.WriteLine(
            $"Detectores isolados: {paths.IsolatedDetectorCsvPath}");
        Console.WriteLine($"Por famílias: {paths.FamilyCountCsvPath}");
        Console.WriteLine($"HTML: {paths.HtmlPath}");
        Console.WriteLine("========================================");
    }

    private static string CreateCategoryCsv(
        ConfidenceBenchmarkReport report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "categoria,eventos,suspeitos,provaveis,confirmados," +
            "scoreMedio,mediaFamilias");

        foreach (var item in report.ByCategory)
        {
            builder
                .Append(Csv(item.Category))
                .Append(',')
                .Append(item.InputEvents)
                .Append(',')
                .Append(item.SuspiciousEvents)
                .Append(',')
                .Append(item.ProbableEvents)
                .Append(',')
                .Append(item.ConfirmedEvents)
                .Append(',')
                .Append(Number(item.AverageScore))
                .Append(',')
                .AppendLine(Number(item.AverageFamilyCount));
        }

        return builder.ToString();
    }

    private static string CreateIsolatedCsv(
        ConfidenceBenchmarkReport report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "detector,eventos,suspeitos,provaveis,confirmados," +
            "categoriaPrincipal");

        foreach (var item in report.IsolatedDetectors)
        {
            builder
                .Append(Csv(item.Detector))
                .Append(',')
                .Append(item.InputEvents)
                .Append(',')
                .Append(item.SuspiciousEvents)
                .Append(',')
                .Append(item.ProbableEvents)
                .Append(',')
                .Append(item.ConfirmedEvents)
                .Append(',')
                .AppendLine(Csv(item.PrincipalCategory));
        }

        return builder.ToString();
    }

    private static string CreateFamilyCountCsv(
        ConfidenceBenchmarkReport report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "quantidadeFamilias,eventos,suspeitos,provaveis," +
            "confirmados,scoreMedio");

        foreach (var item in report.ByFamilyCount)
        {
            builder
                .Append(item.FamilyCount)
                .Append(',')
                .Append(item.InputEvents)
                .Append(',')
                .Append(item.SuspiciousEvents)
                .Append(',')
                .Append(item.ProbableEvents)
                .Append(',')
                .Append(item.ConfirmedEvents)
                .Append(',')
                .AppendLine(Number(item.AverageScore));
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        ConfidenceBenchmarkReport report)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Confidence Engine Benchmark</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;" +
            "margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1450px;margin:auto}.grid{display:grid;" +
            "grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:14px}");
        html.AppendLine(
            ".card,section,.warning{background:#fff;border-radius:12px;" +
            "padding:18px;margin:16px 0;box-shadow:0 1px 4px #0002}");
        html.AppendLine(
            ".warning{border-left:6px solid #d97706}.value{font-size:28px;" +
            "font-weight:800}table{width:100%;border-collapse:collapse;" +
            "font-size:13px}th,td{text-align:left;padding:9px;" +
            "border-bottom:1px solid #e5e7eb}th{background:#f9fafb}");
        html.AppendLine("</style></head><body><div class=\"container\">");

        html.AppendLine("<h1>Confidence Engine Benchmark</h1>");
        html.Append("<div class=\"warning\">")
            .Append(WebUtility.HtmlEncode(report.ImportantWarning))
            .AppendLine("</div>");

        html.AppendLine("<div class=\"grid\">");
        AppendCard(html, "Eventos", report.TotalEvents.ToString("N0"));
        AppendCard(html, "Suspeitos", report.SuspiciousEvents.ToString("N0"));
        AppendCard(html, "Prováveis", report.ProbableEvents.ToString("N0"));
        AppendCard(html, "Confirmados", report.ConfirmedEvents.ToString("N0"));
        html.AppendLine("</div>");

        html.AppendLine(
            "<section><h2>Por categoria</h2><table><thead><tr>" +
            "<th>Categoria</th><th>Eventos</th><th>Suspeitos</th>" +
            "<th>Prováveis</th><th>Confirmados</th><th>Score médio</th>" +
            "<th>Média famílias</th></tr></thead><tbody>");

        foreach (var item in report.ByCategory)
        {
            html
                .Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(item.Category))
                .Append("</td><td>")
                .Append(item.InputEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.SuspiciousEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ProbableEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ConfirmedEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.AverageScore.ToString("F3"))
                .Append("</td><td>")
                .Append(item.AverageFamilyCount.ToString("F2"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Detectores isolados</h2><table><thead><tr>" +
            "<th>Detector</th><th>Eventos</th><th>Suspeitos</th>" +
            "<th>Prováveis</th><th>Confirmados</th>" +
            "<th>Categoria principal</th></tr></thead><tbody>");

        foreach (var item in report.IsolatedDetectors.Take(100))
        {
            html
                .Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(item.Detector))
                .Append("</td><td>")
                .Append(item.InputEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.SuspiciousEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ProbableEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ConfirmedEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(item.PrincipalCategory))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
        html.AppendLine("</div></body></html>");

        return html.ToString();
    }

    private static void AppendCard(
        StringBuilder html,
        string title,
        string value)
    {
        html
            .Append("<div class=\"card\"><div>")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</div><div class=\"value\">")
            .Append(WebUtility.HtmlEncode(value))
            .AppendLine("</div></div>");
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
}

public sealed class ConfidenceBenchmarkOutputPaths
{
    public string JsonPath { get; init; } = "";
    public string CategoryCsvPath { get; init; } = "";
    public string IsolatedDetectorCsvPath { get; init; } = "";
    public string FamilyCountCsvPath { get; init; } = "";
    public string HtmlPath { get; init; } = "";
}
