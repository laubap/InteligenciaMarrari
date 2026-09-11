using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IAApi.Benchmark;

/// <summary>
/// Gera um pacote pequeno de diagnóstico a partir da pasta Results.
///
/// O analisador:
/// - ignora arquivos grandes como *.jsonl;
/// - localiza os relatórios mais recentes;
/// - resume os arquivos disponíveis;
/// - copia somente os resultados leves e úteis;
/// - gera um JSON, CSV e HTML pequenos para compartilhamento.
///
/// Ele não altera detectores nem benchmarks.
/// </summary>
public sealed class ResultsAnalyzer
{
    private const long MaxInputFileBytes = 10 * 1024 * 1024;

    private static readonly string[] PreferredPatterns =
    [
        "false_positive_structural_summary_*.json",
        "false_positive_structural_analysis_*.csv",
        "false_positive_root_cause_summary_*.json",
        "deadband_impact_summary_*.json",
        "deadband_impact_detectors_*.csv",
        "deadband_impact_oracle_*.csv",
        "clean_summary_*.json",
        "clean_performance_*.json",
        "benchmark_evaluation_v2_*.json",
        "calibration_all_summary_*.json"
    ];

    public async Task AnalyzeAsync(
        string resultDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(resultDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Diretório de resultados não encontrado: {resultDirectory}");
        }

        var directory =
            new DirectoryInfo(resultDirectory);

        var selectedFiles =
            SelectLatestUsefulFiles(directory);

        var inventory =
            directory
                .GetFiles()
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => new ResultsFileInventoryItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    Extension = file.Extension,
                    SizeBytes = file.Length,
                    SizeMegabytes = file.Length / 1024d / 1024d,
                    LastModifiedUtc = file.LastWriteTimeUtc,
                    IsLarge = file.Length > MaxInputFileBytes,
                    IsJsonLines =
                        file.Extension.Equals(
                            ".jsonl",
                            StringComparison.OrdinalIgnoreCase),
                    SelectedForPackage =
                        selectedFiles.Any(
                            selected =>
                                string.Equals(
                                    selected.FullName,
                                    file.FullName,
                                    StringComparison.OrdinalIgnoreCase))
                })
                .ToList();

        var structuralSummary =
            await TryReadStructuralSummaryAsync(
                selectedFiles,
                cancellationToken);

        var report =
            new ResultsAnalysisReport
            {
                GeneratedAtUtc =
                    DateTime.UtcNow,

                ResultDirectory =
                    resultDirectory,

                TotalFiles =
                    inventory.Count,

                TotalSizeBytes =
                    inventory.Sum(item => item.SizeBytes),

                TotalSizeMegabytes =
                    inventory.Sum(item => item.SizeMegabytes),

                LargeFiles =
                    inventory.LongCount(item => item.IsLarge),

                JsonLinesFiles =
                    inventory.LongCount(item => item.IsJsonLines),

                SelectedFiles =
                    selectedFiles
                        .Select(file => new ResultsSelectedFile
                        {
                            Name =
                                file.Name,

                            FullPath =
                                file.FullName,

                            SizeBytes =
                                file.Length,

                            SizeMegabytes =
                                file.Length / 1024d / 1024d,

                            LastModifiedUtc =
                                file.LastWriteTimeUtc
                        })
                        .ToList(),

                Inventory =
                    inventory,

                FalsePositiveStructuralSummary =
                    structuralSummary
            };

        var identifier =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);

        var packageDirectory =
            Path.Combine(
                resultDirectory,
                $"share_package_{identifier}");

        Directory.CreateDirectory(packageDirectory);

        foreach (var file in selectedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Length > MaxInputFileBytes)
            {
                continue;
            }

            var destination =
                Path.Combine(
                    packageDirectory,
                    file.Name);

            file.CopyTo(
                destination,
                overwrite: true);
        }

        var jsonPath =
            Path.Combine(
                packageDirectory,
                "results_analysis_report.json");

        var csvPath =
            Path.Combine(
                packageDirectory,
                "results_files_inventory.csv");

        var htmlPath =
            Path.Combine(
                packageDirectory,
                "results_analysis_report.html");

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
            CreateInventoryCsv(inventory),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(report),
            cancellationToken);

        PrintSummary(
            report,
            packageDirectory);
    }

    private static List<FileInfo> SelectLatestUsefulFiles(
        DirectoryInfo directory)
    {
        var selected =
            new List<FileInfo>();

        foreach (var pattern in PreferredPatterns)
        {
            var latest =
                directory
                    .GetFiles(pattern)
                    .Where(
                        file =>
                            file.Length <= MaxInputFileBytes)
                    .OrderByDescending(
                        file =>
                            file.LastWriteTimeUtc)
                    .FirstOrDefault();

            if (latest is not null)
            {
                selected.Add(latest);
            }
        }

        return selected
            .GroupBy(
                file => file.FullName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
    }

    private static async Task<StructuralSummarySnapshot?>
        TryReadStructuralSummaryAsync(
            IReadOnlyCollection<FileInfo> selectedFiles,
            CancellationToken cancellationToken)
    {
        var file =
            selectedFiles
                .FirstOrDefault(
                    item =>
                        item.Name.StartsWith(
                            "false_positive_structural_summary_",
                            StringComparison.OrdinalIgnoreCase) &&
                        item.Extension.Equals(
                            ".json",
                            StringComparison.OrdinalIgnoreCase));

        if (file is null)
        {
            return null;
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                    file.FullName,
                    cancellationToken);

            using var document =
                JsonDocument.Parse(json);

            var root =
                document.RootElement;

            return new StructuralSummarySnapshot
            {
                SourceFile =
                    file.Name,

                TotalEvents =
                    GetInt64(root, "totalEvents"),

                ZeroDuringEventCount =
                    GetInt64(root, "zeroDuringEventCount"),

                StartedAtMinuteZeroCount =
                    GetInt64(root, "startedAtMinuteZeroCount"),

                LastedExactlyOneDayCount =
                    GetInt64(root, "lastedExactlyOneDayCount"),

                ConstantDuringEventCount =
                    GetInt64(root, "constantDuringEventCount"),

                ReturnedAfterEventCount =
                    GetInt64(root, "returnedAfterEventCount"),

                Categories =
                    ReadCategories(root)
            };
        }
        catch
        {
            return null;
        }
    }

    private static List<StructuralCategorySnapshot>
        ReadCategories(
            JsonElement root)
    {
        if (!root.TryGetProperty(
                "categories",
                out var categoriesElement) ||
            categoriesElement.ValueKind !=
                JsonValueKind.Array)
        {
            return [];
        }

        var categories =
            new List<StructuralCategorySnapshot>();

        foreach (var item in categoriesElement.EnumerateArray())
        {
            categories.Add(
                new StructuralCategorySnapshot
                {
                    Category =
                        GetString(
                            item,
                            "category"),

                    EventCount =
                        GetInt64(
                            item,
                            "eventCount"),

                    TagCount =
                        GetInt64(
                            item,
                            "tagCount"),

                    AverageConfidence =
                        GetDouble(
                            item,
                            "averageConfidence"),

                    AverageDurationMinutes =
                        GetDouble(
                            item,
                            "averageDurationMinutes")
                });
        }

        return categories
            .OrderByDescending(item => item.EventCount)
            .ToList();
    }

    private static long GetInt64(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.Number)
        {
            return 0;
        }

        return property.TryGetInt64(out var value)
            ? value
            : 0;
    }

    private static double GetDouble(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.Number)
        {
            return 0;
        }

        return property.TryGetDouble(out var value)
            ? value
            : 0;
    }

    private static string GetString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.String)
        {
            return "";
        }

        return property.GetString() ?? "";
    }

    private static string CreateInventoryCsv(
        IEnumerable<ResultsFileInventoryItem> inventory)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "nome,extensao,tamanhoBytes,tamanhoMB,ultimaAlteracaoUtc," +
            "arquivoGrande,jsonl,selecionadoParaPacote");

        foreach (var item in inventory)
        {
            builder
                .Append(Csv(item.Name))
                .Append(',')
                .Append(Csv(item.Extension))
                .Append(',')
                .Append(item.SizeBytes)
                .Append(',')
                .Append(
                    item.SizeMegabytes.ToString(
                        "F3",
                        CultureInfo.InvariantCulture))
                .Append(',')
                .Append(
                    item.LastModifiedUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture))
                .Append(',')
                .Append(item.IsLarge)
                .Append(',')
                .Append(item.IsJsonLines)
                .Append(',')
                .AppendLine(
                    item.SelectedForPackage.ToString());
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        ResultsAnalysisReport report)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Resumo da pasta Results</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;" +
            "margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1400px;margin:auto}.grid{display:grid;" +
            "grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:14px}");
        html.AppendLine(
            ".card,section{background:white;border-radius:12px;padding:18px;" +
            "margin:16px 0;box-shadow:0 1px 4px #0002}");
        html.AppendLine(
            ".value{font-size:28px;font-weight:800}table{width:100%;" +
            "border-collapse:collapse;font-size:13px}th,td{text-align:left;" +
            "padding:9px;border-bottom:1px solid #e5e7eb}th{background:#f9fafb}");
        html.AppendLine("</style></head><body><div class=\"container\">");
        html.AppendLine("<h1>Resumo compartilhável da pasta Results</h1>");
        html.AppendLine("<div class=\"grid\">");

        AppendCard(
            html,
            "Arquivos",
            report.TotalFiles.ToString("N0"));

        AppendCard(
            html,
            "Tamanho total",
            $"{report.TotalSizeMegabytes:N1} MB");

        AppendCard(
            html,
            "Arquivos grandes",
            report.LargeFiles.ToString("N0"));

        AppendCard(
            html,
            "Arquivos JSONL",
            report.JsonLinesFiles.ToString("N0"));

        AppendCard(
            html,
            "Arquivos selecionados",
            report.SelectedFiles.Count.ToString("N0"));

        html.AppendLine("</div>");

        if (report.FalsePositiveStructuralSummary is not null)
        {
            var summary =
                report.FalsePositiveStructuralSummary;

            html.AppendLine(
                "<section><h2>Falsos positivos</h2><div class=\"grid\">");

            AppendCard(
                html,
                "Eventos analisados",
                summary.TotalEvents.ToString("N0"));

            AppendCard(
                html,
                "Valor zerado",
                summary.ZeroDuringEventCount.ToString("N0"));

            AppendCard(
                html,
                "Constantes",
                summary.ConstantDuringEventCount.ToString("N0"));

            AppendCard(
                html,
                "Retornaram",
                summary.ReturnedAfterEventCount.ToString("N0"));

            html.AppendLine("</div>");

            html.AppendLine(
                "<table><thead><tr><th>Categoria</th><th>Eventos</th>" +
                "<th>Tags</th><th>Confiança média</th>" +
                "<th>Duração média</th></tr></thead><tbody>");

            foreach (var item in summary.Categories)
            {
                html
                    .Append("<tr><td>")
                    .Append(Encode(item.Category))
                    .Append("</td><td>")
                    .Append(item.EventCount.ToString("N0"))
                    .Append("</td><td>")
                    .Append(item.TagCount.ToString("N0"))
                    .Append("</td><td>")
                    .Append(item.AverageConfidence.ToString("F1"))
                    .Append("%</td><td>")
                    .Append(item.AverageDurationMinutes.ToString("F1"))
                    .AppendLine(" min</td></tr>");
            }

            html.AppendLine("</tbody></table></section>");
        }

        html.AppendLine(
            "<section><h2>Arquivos incluídos no pacote</h2>" +
            "<table><thead><tr><th>Arquivo</th><th>Tamanho</th>" +
            "<th>Última alteração</th></tr></thead><tbody>");

        foreach (var item in report.SelectedFiles)
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Name))
                .Append("</td><td>")
                .Append(item.SizeMegabytes.ToString("F3"))
                .Append(" MB</td><td>")
                .Append(item.LastModifiedUtc.ToString("O"))
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
            .Append(Encode(title))
            .Append("</div><div class=\"value\">")
            .Append(Encode(value))
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

    private static string Encode(
        string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private static void PrintSummary(
        ResultsAnalysisReport report,
        string packageDirectory)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" ANÁLISE DA PASTA RESULTS");
        Console.WriteLine("========================================");
        Console.WriteLine($"Arquivos encontrados: {report.TotalFiles:N0}");
        Console.WriteLine($"Tamanho total: {report.TotalSizeMegabytes:N2} MB");
        Console.WriteLine($"Arquivos grandes: {report.LargeFiles:N0}");
        Console.WriteLine($"Arquivos JSONL: {report.JsonLinesFiles:N0}");
        Console.WriteLine($"Arquivos incluídos: {report.SelectedFiles.Count:N0}");

        if (report.FalsePositiveStructuralSummary is not null)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Eventos analisados: " +
                $"{report.FalsePositiveStructuralSummary.TotalEvents:N0}");

            foreach (var category in
                     report.FalsePositiveStructuralSummary.Categories)
            {
                Console.WriteLine(
                    $"{category.Category,-48} {category.EventCount,8:N0}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Pacote criado em: {packageDirectory}");
        Console.WriteLine("========================================");
    }
}

public sealed class ResultsAnalysisReport
{
    public DateTime GeneratedAtUtc { get; set; }
    public string ResultDirectory { get; set; } = "";
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public double TotalSizeMegabytes { get; set; }
    public long LargeFiles { get; set; }
    public long JsonLinesFiles { get; set; }

    public List<ResultsSelectedFile> SelectedFiles { get; set; } = [];
    public List<ResultsFileInventoryItem> Inventory { get; set; } = [];
    public StructuralSummarySnapshot? FalsePositiveStructuralSummary { get; set; }
}

public sealed class ResultsSelectedFile
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public double SizeMegabytes { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}

public sealed class ResultsFileInventoryItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Extension { get; set; } = "";
    public long SizeBytes { get; set; }
    public double SizeMegabytes { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public bool IsLarge { get; set; }
    public bool IsJsonLines { get; set; }
    public bool SelectedForPackage { get; set; }
}

public sealed class StructuralSummarySnapshot
{
    public string SourceFile { get; set; } = "";
    public long TotalEvents { get; set; }
    public long ZeroDuringEventCount { get; set; }
    public long StartedAtMinuteZeroCount { get; set; }
    public long LastedExactlyOneDayCount { get; set; }
    public long ConstantDuringEventCount { get; set; }
    public long ReturnedAfterEventCount { get; set; }
    public List<StructuralCategorySnapshot> Categories { get; set; } = [];
}

public sealed class StructuralCategorySnapshot
{
    public string Category { get; set; } = "";
    public long EventCount { get; set; }
    public long TagCount { get; set; }
    public double AverageConfidence { get; set; }
    public double AverageDurationMinutes { get; set; }
}