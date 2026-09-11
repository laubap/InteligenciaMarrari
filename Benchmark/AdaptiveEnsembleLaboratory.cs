using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace IAApi.Benchmark;

/// <summary>
/// Laboratório offline de estratégias de ensemble.
///
/// Primeira versão da Camada 4:
/// - usa os eventos já agrupados do banco limpo;
/// - não reprocessa milhões de leituras;
/// - não altera o EnsembleDetectionService;
/// - compara estratégias estruturais usando os detectores participantes;
/// - gera JSON, CSV e HTML pequenos.
///
/// Limitação deliberada:
/// esta fase mede redução potencial de falsos positivos no banco limpo.
/// Ela NÃO aprova uma estratégia para produção sem posterior avaliação Oracle.
/// </summary>
public sealed class AdaptiveEnsembleLaboratory
{
    private static readonly IReadOnlyList<AdaptiveStrategyDefinition>
        StrategyDefinitions =
        [
            new(
                "CURRENT",
                "Regra atual observada",
                "Mantém todos os eventos produzidos pelo ensemble atual."),

            new(
                "MIN_TWO_VOTES",
                "Mínimo de dois votos",
                "Confirma somente eventos com pelo menos dois detectores participantes."),

            new(
                "MIN_TWO_FAMILIES",
                "Mínimo de duas famílias",
                "Confirma somente eventos sustentados por duas famílias independentes."),

            new(
                "ISOLATED_TRANSITION_PENDING",
                "Transição isolada fica pendente",
                "Eventos sustentados apenas pela família de transição não são confirmados imediatamente."),

            new(
                "DISCRETE_ROUTING",
                "Roteamento de tags discretas",
                "Eventos classificados como tag discreta analisada como contínua não são confirmados pelo ensemble contínuo."),

            new(
                "SAFE_COMBINED_PROTOTYPE",
                "Protótipo seguro combinado",
                "Exige duas famílias, trata transição isolada como pendente e remove tags discretas do ensemble contínuo.")
        ];

    public async Task RunAsync(
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
        Console.WriteLine(" ADAPTIVE ENSEMBLE LABORATORY");
        Console.WriteLine("========================================");
        Console.WriteLine($"Entrada: {inputFile.FullName}");

        var events =
            ReadEvents(
                inputFile.FullName,
                cancellationToken);

        Console.WriteLine($"Eventos lidos: {events.Count:N0}");

        var report =
            Evaluate(
                inputFile,
                events);

        var identifier =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);

        var jsonPath =
            Path.Combine(
                resultDirectory,
                $"adaptive_ensemble_lab_summary_{identifier}.json");

        var csvPath =
            Path.Combine(
                resultDirectory,
                $"adaptive_ensemble_lab_strategies_{identifier}.csv");

        var categoryCsvPath =
            Path.Combine(
                resultDirectory,
                $"adaptive_ensemble_lab_by_category_{identifier}.csv");

        var htmlPath =
            Path.Combine(
                resultDirectory,
                $"adaptive_ensemble_lab_report_{identifier}.html");

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
            CreateStrategiesCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            categoryCsvPath,
            CreateCategoryCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(report),
            cancellationToken);

        PrintSummary(
            report,
            jsonPath,
            csvPath,
            categoryCsvPath,
            htmlPath);
    }

    private static AdaptiveEnsembleLaboratoryReport Evaluate(
        FileInfo inputFile,
        IReadOnlyCollection<AdaptiveLabEvent> events)
    {
        var total =
            events.Count;

        var strategyResults =
            StrategyDefinitions
                .Select(
                    definition =>
                        EvaluateStrategy(
                            definition,
                            events,
                            total))
                .ToList();

        var current =
            strategyResults
                .Single(
                    item =>
                        item.StrategyId ==
                        "CURRENT");

        var candidates =
            strategyResults
                .Where(
                    item =>
                        item.StrategyId !=
                        "CURRENT")
                .OrderBy(
                    item =>
                        item.ConfirmedEvents)
                .ToList();

        return new AdaptiveEnsembleLaboratoryReport
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            SourceFile =
                inputFile.FullName,

            DatasetScope =
                "CleanBaselineFalsePositiveEvents",

            TotalInputEvents =
                total,

            TagCount =
                events
                    .Select(item => item.TagName)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count(),

            CategoryCount =
                events
                    .Select(item => item.Category)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count(),

            Strategies =
                strategyResults,

            BestCleanBaselineCandidate =
                candidates.FirstOrDefault(),

            CurrentConfirmedEvents =
                current.ConfirmedEvents,

            ImportantWarning =
                "A redução calculada é somente sobre o banco limpo. " +
                "Nenhuma estratégia deve substituir a produção antes de " +
                "ser avaliada no banco com anomalias e no Oracle."
        };
    }

    private static AdaptiveStrategyResult EvaluateStrategy(
        AdaptiveStrategyDefinition definition,
        IReadOnlyCollection<AdaptiveLabEvent> events,
        int total)
    {
        var evaluations =
            events
                .Select(
                    item =>
                        new
                        {
                            Event = item,
                            Decision =
                                Decide(
                                    definition.Id,
                                    item)
                        })
                .ToList();

        var confirmed =
            evaluations.LongCount(
                item =>
                    item.Decision ==
                    AdaptiveLabDecision.Confirmed);

        var pending =
            evaluations.LongCount(
                item =>
                    item.Decision ==
                    AdaptiveLabDecision.Pending);

        var rejected =
            evaluations.LongCount(
                item =>
                    item.Decision ==
                    AdaptiveLabDecision.Rejected);

        var byCategory =
            evaluations
                .GroupBy(
                    item =>
                        item.Event.Category,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new AdaptiveStrategyCategoryResult
                        {
                            Category =
                                group.Key,

                            InputEvents =
                                group.LongCount(),

                            ConfirmedEvents =
                                group.LongCount(
                                    item =>
                                        item.Decision ==
                                        AdaptiveLabDecision.Confirmed),

                            PendingEvents =
                                group.LongCount(
                                    item =>
                                        item.Decision ==
                                        AdaptiveLabDecision.Pending),

                            RejectedEvents =
                                group.LongCount(
                                    item =>
                                        item.Decision ==
                                        AdaptiveLabDecision.Rejected)
                        })
                .OrderByDescending(
                    item =>
                        item.InputEvents)
                .ToList();

        return new AdaptiveStrategyResult
        {
            StrategyId =
                definition.Id,

            StrategyName =
                definition.Name,

            Description =
                definition.Description,

            InputEvents =
                total,

            ConfirmedEvents =
                confirmed,

            PendingEvents =
                pending,

            RejectedEvents =
                rejected,

            PotentialFalsePositiveReduction =
                total > 0
                    ? 1 -
                      (confirmed / (double)total)
                    : 0,

            ConfirmedRate =
                total > 0
                    ? confirmed / (double)total
                    : 0,

            PendingRate =
                total > 0
                    ? pending / (double)total
                    : 0,

            CategoryResults =
                byCategory
        };
    }

    private static AdaptiveLabDecision Decide(
        string strategyId,
        AdaptiveLabEvent item)
    {
        return strategyId switch
        {
            "CURRENT" =>
                AdaptiveLabDecision.Confirmed,

            "MIN_TWO_VOTES" =>
                item.Detectors.Count >= 2
                    ? AdaptiveLabDecision.Confirmed
                    : AdaptiveLabDecision.Pending,

            "MIN_TWO_FAMILIES" =>
                item.Families.Count >= 2
                    ? AdaptiveLabDecision.Confirmed
                    : AdaptiveLabDecision.Pending,

            "ISOLATED_TRANSITION_PENDING" =>
                item.Families.Count == 1 &&
                item.Families.Contains(
                    AdaptiveDetectorFamily.Transition)
                    ? AdaptiveLabDecision.Pending
                    : AdaptiveLabDecision.Confirmed,

            "DISCRETE_ROUTING" =>
                item.IsDiscreteTagCategory
                    ? AdaptiveLabDecision.Rejected
                    : AdaptiveLabDecision.Confirmed,

            "SAFE_COMBINED_PROTOTYPE" =>
                DecideSafeCombined(item),

            _ =>
                throw new InvalidOperationException(
                    $"Estratégia desconhecida: {strategyId}")
        };
    }

    private static AdaptiveLabDecision DecideSafeCombined(
        AdaptiveLabEvent item)
    {
        if (item.IsDiscreteTagCategory)
        {
            return AdaptiveLabDecision.Rejected;
        }

        if (item.Families.Count < 2)
        {
            return AdaptiveLabDecision.Pending;
        }

        /*
         * Um evento de um minuto dominado somente por transição
         * não deveria chegar aqui porque teria uma única família.
         * Caso existam duas famílias, ele continua confirmado nesta
         * primeira versão para evitar uma filtragem excessiva sem Oracle.
         */
        return AdaptiveLabDecision.Confirmed;
    }

    private static List<AdaptiveLabEvent> ReadEvents(
        string csvPath,
        CancellationToken cancellationToken)
    {
        var result =
            new List<AdaptiveLabEvent>();

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
        RequireColumn(indexes, "categoriaComportamental");

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields =
                ParseCsvLine(line);

            var detectorNames =
                GetField(
                        fields,
                        indexes["detectores"])
                    .Split(
                        '|',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Where(
                        item =>
                            !string.IsNullOrWhiteSpace(item))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        item => item,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var families =
                detectorNames
                    .Select(ResolveFamily)
                    .Where(
                        family =>
                            family !=
                            AdaptiveDetectorFamily.Unknown)
                    .Distinct()
                    .ToHashSet();

            var category =
                GetField(
                    fields,
                    indexes["categoriaComportamental"]);

            result.Add(
                new AdaptiveLabEvent
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

                    Category =
                        category,

                    Detectors =
                        detectorNames,

                    Families =
                        families,

                    IsDiscreteTagCategory =
                        category.Equals(
                            "Tag discreta analisada como sinal contínuo",
                            StringComparison.OrdinalIgnoreCase)
                });
        }

        return result;
    }

    private static AdaptiveDetectorFamily ResolveFamily(
        string detectorName)
    {
        if (EqualsName(
                detectorName,
                "Z-Score") ||
            EqualsName(
                detectorName,
                "Mediana e MAD"))
        {
            return AdaptiveDetectorFamily.StatisticalDeviation;
        }

        if (EqualsName(
                detectorName,
                "EWMA"))
        {
            return AdaptiveDetectorFamily.TrendAndSmoothing;
        }

        if (EqualsName(
                detectorName,
                "Taxa de variação") ||
            EqualsName(
                detectorName,
                "Mudança de patamar"))
        {
            return AdaptiveDetectorFamily.Transition;
        }

        if (EqualsName(
                detectorName,
                "Persistência"))
        {
            return AdaptiveDetectorFamily.TemporalConfirmation;
        }

        if (detectorName.StartsWith(
                "Multivariado/",
                StringComparison.OrdinalIgnoreCase))
        {
            return AdaptiveDetectorFamily.Multivariate;
        }

        if (detectorName.Contains(
                "Isolation",
                StringComparison.OrdinalIgnoreCase))
        {
            return AdaptiveDetectorFamily.MachineLearning;
        }

        return AdaptiveDetectorFamily.Unknown;
    }

    private static string CreateStrategiesCsv(
        AdaptiveEnsembleLaboratoryReport report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "estrategia,nome,eventosEntrada,eventosConfirmados," +
            "eventosPendentes,eventosRejeitados,taxaConfirmacao," +
            "reducaoPotencialFalsosPositivos");

        foreach (var item in report.Strategies)
        {
            builder
                .Append(Csv(item.StrategyId))
                .Append(',')
                .Append(Csv(item.StrategyName))
                .Append(',')
                .Append(item.InputEvents)
                .Append(',')
                .Append(item.ConfirmedEvents)
                .Append(',')
                .Append(item.PendingEvents)
                .Append(',')
                .Append(item.RejectedEvents)
                .Append(',')
                .Append(Number(item.ConfirmedRate))
                .Append(',')
                .AppendLine(
                    Number(
                        item.PotentialFalsePositiveReduction));
        }

        return builder.ToString();
    }

    private static string CreateCategoryCsv(
        AdaptiveEnsembleLaboratoryReport report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "estrategia,categoria,eventosEntrada,eventosConfirmados," +
            "eventosPendentes,eventosRejeitados,reducaoPotencial");

        foreach (var strategy in report.Strategies)
        {
            foreach (var category in strategy.CategoryResults)
            {
                builder
                    .Append(Csv(strategy.StrategyId))
                    .Append(',')
                    .Append(Csv(category.Category))
                    .Append(',')
                    .Append(category.InputEvents)
                    .Append(',')
                    .Append(category.ConfirmedEvents)
                    .Append(',')
                    .Append(category.PendingEvents)
                    .Append(',')
                    .Append(category.RejectedEvents)
                    .Append(',')
                    .AppendLine(
                        Number(
                            category.PotentialReduction));
            }
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        AdaptiveEnsembleLaboratoryReport report)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Adaptive Ensemble Laboratory</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;" +
            "margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1450px;margin:auto}.grid{display:grid;" +
            "grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:14px}");
        html.AppendLine(
            ".card,section,.warning{background:#fff;border-radius:12px;padding:18px;" +
            "margin:16px 0;box-shadow:0 1px 4px #0002}");
        html.AppendLine(
            ".warning{border-left:6px solid #d97706}.value{font-size:28px;" +
            "font-weight:800}table{width:100%;border-collapse:collapse;" +
            "font-size:13px}th,td{text-align:left;padding:9px;" +
            "border-bottom:1px solid #e5e7eb}th{background:#f9fafb}");
        html.AppendLine("</style></head><body><div class=\"container\">");

        html.AppendLine("<h1>Adaptive Ensemble Laboratory</h1>");
        html.AppendLine(
            "<p>Comparação offline de estratégias sobre eventos do banco limpo.</p>");

        html.Append("<div class=\"warning\">")
            .Append(Encode(report.ImportantWarning))
            .AppendLine("</div>");

        html.AppendLine("<div class=\"grid\">");
        AppendCard(
            html,
            "Eventos de entrada",
            report.TotalInputEvents.ToString("N0"));

        AppendCard(
            html,
            "Tags",
            report.TagCount.ToString("N0"));

        AppendCard(
            html,
            "Categorias",
            report.CategoryCount.ToString("N0"));

        AppendCard(
            html,
            "Estratégias",
            report.Strategies.Count.ToString("N0"));
        html.AppendLine("</div>");

        html.AppendLine(
            "<section><h2>Comparação das estratégias</h2>" +
            "<table><thead><tr><th>Estratégia</th><th>Confirmados</th>" +
            "<th>Pendentes</th><th>Rejeitados</th><th>Redução potencial</th>" +
            "<th>Descrição</th></tr></thead><tbody>");

        foreach (var item in report.Strategies)
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.StrategyName))
                .Append("</td><td>")
                .Append(item.ConfirmedEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.PendingEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.RejectedEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.PotentialFalsePositiveReduction.ToString("P1"))
                .Append("</td><td>")
                .Append(Encode(item.Description))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        foreach (var strategy in report.Strategies)
        {
            html
                .Append("<section><h2>")
                .Append(Encode(strategy.StrategyName))
                .AppendLine(" — por categoria</h2>");

            html.AppendLine(
                "<table><thead><tr><th>Categoria</th><th>Entrada</th>" +
                "<th>Confirmados</th><th>Pendentes</th><th>Rejeitados</th>" +
                "<th>Redução potencial</th></tr></thead><tbody>");

            foreach (var category in strategy.CategoryResults)
            {
                html
                    .Append("<tr><td>")
                    .Append(Encode(category.Category))
                    .Append("</td><td>")
                    .Append(category.InputEvents.ToString("N0"))
                    .Append("</td><td>")
                    .Append(category.ConfirmedEvents.ToString("N0"))
                    .Append("</td><td>")
                    .Append(category.PendingEvents.ToString("N0"))
                    .Append("</td><td>")
                    .Append(category.RejectedEvents.ToString("N0"))
                    .Append("</td><td>")
                    .Append(category.PotentialReduction.ToString("P1"))
                    .AppendLine("</td></tr>");
            }

            html.AppendLine("</tbody></table></section>");
        }

        html.AppendLine("</div></body></html>");
        return html.ToString();
    }

    private static void PrintSummary(
        AdaptiveEnsembleLaboratoryReport report,
        string jsonPath,
        string csvPath,
        string categoryCsvPath,
        string htmlPath)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" RESULTADO DO LABORATÓRIO");
        Console.WriteLine("========================================");

        foreach (var item in report.Strategies)
        {
            Console.WriteLine(
                $"{item.StrategyName,-38} " +
                $"{item.ConfirmedEvents,8:N0} confirmados | " +
                $"{item.PendingEvents,8:N0} pendentes | " +
                $"redução {item.PotentialFalsePositiveReduction,7:P1}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "ATENÇÃO: redução calculada somente no banco limpo.");
        Console.WriteLine($"JSON: {jsonPath}");
        Console.WriteLine($"CSV: {csvPath}");
        Console.WriteLine($"Por categoria: {categoryCsvPath}");
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

    private static bool EqualsName(
        string left,
        string right)
    {
        return string.Equals(
            left,
            right,
            StringComparison.OrdinalIgnoreCase);
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
        return WebUtility.HtmlEncode(value);
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

public enum AdaptiveLabDecision
{
    Confirmed = 0,
    Pending = 1,
    Rejected = 2
}

public enum AdaptiveDetectorFamily
{
    Unknown = 0,
    StatisticalDeviation = 1,
    TrendAndSmoothing = 2,
    Transition = 3,
    TemporalConfirmation = 4,
    Multivariate = 5,
    MachineLearning = 6
}

public sealed record AdaptiveStrategyDefinition(
    string Id,
    string Name,
    string Description);

public sealed class AdaptiveLabEvent
{
    public string TagName { get; init; } = "";
    public int DurationMinutes { get; init; }
    public string Category { get; init; } = "";
    public IReadOnlyList<string> Detectors { get; init; } =
        Array.Empty<string>();
    public IReadOnlySet<AdaptiveDetectorFamily> Families { get; init; } =
        new HashSet<AdaptiveDetectorFamily>();
    public bool IsDiscreteTagCategory { get; init; }
}

public sealed class AdaptiveEnsembleLaboratoryReport
{
    public DateTime GeneratedAtUtc { get; init; }
    public string SourceFile { get; init; } = "";
    public string DatasetScope { get; init; } = "";
    public int TotalInputEvents { get; init; }
    public int TagCount { get; init; }
    public int CategoryCount { get; init; }
    public long CurrentConfirmedEvents { get; init; }
    public string ImportantWarning { get; init; } = "";

    public IReadOnlyList<AdaptiveStrategyResult> Strategies { get; init; } =
        Array.Empty<AdaptiveStrategyResult>();

    public AdaptiveStrategyResult? BestCleanBaselineCandidate { get; init; }
}

public sealed class AdaptiveStrategyResult
{
    public string StrategyId { get; init; } = "";
    public string StrategyName { get; init; } = "";
    public string Description { get; init; } = "";
    public int InputEvents { get; init; }
    public long ConfirmedEvents { get; init; }
    public long PendingEvents { get; init; }
    public long RejectedEvents { get; init; }
    public double PotentialFalsePositiveReduction { get; init; }
    public double ConfirmedRate { get; init; }
    public double PendingRate { get; init; }

    public IReadOnlyList<AdaptiveStrategyCategoryResult> CategoryResults
        { get; init; } =
        Array.Empty<AdaptiveStrategyCategoryResult>();
}

public sealed class AdaptiveStrategyCategoryResult
{
    public string Category { get; init; } = "";
    public long InputEvents { get; init; }
    public long ConfirmedEvents { get; init; }
    public long PendingEvents { get; init; }
    public long RejectedEvents { get; init; }

    public double PotentialReduction =>
        InputEvents > 0
            ? 1 -
              (ConfirmedEvents / (double)InputEvents)
            : 0;
}
