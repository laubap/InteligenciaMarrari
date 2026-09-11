using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace IAApi.Benchmark;

/// <summary>
/// Analisa a anatomia do consenso dos detectores nos eventos agrupados.
///
/// O relatório responde:
/// - quantos detectores participaram de cada evento;
/// - quantas famílias independentes participaram;
/// - quais combinações de famílias são mais frequentes;
/// - quais detectores aparecem sozinhos;
/// - quais detectores da mesma família aparecem juntos;
/// - como o consenso se distribui por categoria.
///
/// Esta classe é somente diagnóstica.
/// Ela não altera detectores, ensemble ou produção.
/// </summary>
public sealed class FamilyConsensusReport
{
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
        Console.WriteLine(" FAMILY CONSENSUS REPORT");
        Console.WriteLine("========================================");
        Console.WriteLine($"Entrada: {inputFile.FullName}");

        var events =
            ReadEvents(
                inputFile.FullName,
                cancellationToken);

        Console.WriteLine($"Eventos lidos: {events.Count:N0}");

        var report =
            BuildReport(
                inputFile,
                events);

        var identifier =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);

        var summaryPath =
            Path.Combine(
                resultDirectory,
                $"family_consensus_summary_{identifier}.json");

        var eventsPath =
            Path.Combine(
                resultDirectory,
                $"family_consensus_events_{identifier}.csv");

        var categoriesPath =
            Path.Combine(
                resultDirectory,
                $"family_consensus_by_category_{identifier}.csv");

        var combinationsPath =
            Path.Combine(
                resultDirectory,
                $"family_consensus_combinations_{identifier}.csv");

        var isolatedPath =
            Path.Combine(
                resultDirectory,
                $"family_consensus_isolated_detectors_{identifier}.csv");

        var htmlPath =
            Path.Combine(
                resultDirectory,
                $"family_consensus_report_{identifier}.html");

        await File.WriteAllTextAsync(
            summaryPath,
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
            eventsPath,
            CreateEventsCsv(events),
            cancellationToken);

        await File.WriteAllTextAsync(
            categoriesPath,
            CreateCategoriesCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            combinationsPath,
            CreateCombinationsCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            isolatedPath,
            CreateIsolatedDetectorsCsv(report),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(report),
            cancellationToken);

        PrintSummary(
            report,
            summaryPath,
            eventsPath,
            categoriesPath,
            combinationsPath,
            isolatedPath,
            htmlPath);
    }

    private static FamilyConsensusSummary BuildReport(
        FileInfo inputFile,
        IReadOnlyCollection<FamilyConsensusEvent> events)
    {
        var totalEvents =
            events.Count;

        var detectorCountDistribution =
            events
                .GroupBy(item => item.Detectors.Count)
                .Select(
                    group =>
                        new ConsensusCountDistribution
                        {
                            Count =
                                group.Key,

                            EventCount =
                                group.LongCount(),

                            EventRate =
                                totalEvents > 0
                                    ? group.LongCount() /
                                      (double)totalEvents
                                    : 0
                        })
                .OrderBy(item => item.Count)
                .ToList();

        var familyCountDistribution =
            events
                .GroupBy(item => item.Families.Count)
                .Select(
                    group =>
                        new ConsensusCountDistribution
                        {
                            Count =
                                group.Key,

                            EventCount =
                                group.LongCount(),

                            EventRate =
                                totalEvents > 0
                                    ? group.LongCount() /
                                      (double)totalEvents
                                    : 0
                        })
                .OrderBy(item => item.Count)
                .ToList();

        var familyParticipation =
            events
                .SelectMany(
                    item =>
                        item.Families.Select(
                            family =>
                                new
                                {
                                    Event = item,
                                    Family = family
                                }))
                .GroupBy(item => item.Family)
                .Select(
                    group =>
                        new FamilyParticipationSummary
                        {
                            Family =
                                group.Key,

                            EventCount =
                                group.LongCount(),

                            EventRate =
                                totalEvents > 0
                                    ? group.LongCount() /
                                      (double)totalEvents
                                    : 0,

                            IsolatedEventCount =
                                group.LongCount(
                                    item =>
                                        item.Event.Families.Count == 1),

                            TagCount =
                                group
                                    .Select(item => item.Event.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount(),

                            CategoryCount =
                                group
                                    .Select(item => item.Event.Category)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount()
                        })
                .OrderByDescending(item => item.EventCount)
                .ToList();

        var familyCombinations =
            events
                .GroupBy(
                    item =>
                        item.FamilyCombination,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new FamilyCombinationSummary
                        {
                            Combination =
                                group.Key,

                            FamilyCount =
                                group.First().Families.Count,

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
                .OrderByDescending(item => item.EventCount)
                .ToList();

        var isolatedDetectors =
            events
                .Where(item => item.Detectors.Count == 1)
                .GroupBy(
                    item => item.Detectors[0],
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new IsolatedDetectorSummary
                        {
                            Detector =
                                group.Key,

                            Family =
                                ResolveFamily(group.Key),

                            EventCount =
                                group.LongCount(),

                            IsolatedEventRate =
                                totalEvents > 0
                                    ? group.LongCount() /
                                      (double)totalEvents
                                    : 0,

                            PrincipalCategory =
                                group
                                    .GroupBy(
                                        item => item.Category,
                                        StringComparer.OrdinalIgnoreCase)
                                    .OrderByDescending(
                                        categoryGroup =>
                                            categoryGroup.Count())
                                    .First()
                                    .Key,

                            OneMinuteEventCount =
                                group.LongCount(
                                    item =>
                                        item.DurationMinutes == 1),

                            TagCount =
                                group
                                    .Select(item => item.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount()
                        })
                .OrderByDescending(item => item.EventCount)
                .ToList();

        var sameFamilyMultipleDetectors =
            events
                .Where(
                    item =>
                        item.Detectors.Count >= 2 &&
                        item.Families.Count == 1)
                .GroupBy(
                    item => item.DetectorCombination,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new SameFamilyCombinationSummary
                        {
                            DetectorCombination =
                                group.Key,

                            Family =
                                group.First().Families.First(),

                            DetectorCount =
                                group.First().Detectors.Count,

                            EventCount =
                                group.LongCount(),

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
                .OrderByDescending(item => item.EventCount)
                .ToList();

        var byCategory =
            events
                .GroupBy(
                    item => item.Category,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                    {
                        var categoryEvents =
                            group.ToList();

                        return new CategoryConsensusSummary
                        {
                            Category =
                                group.Key,

                            EventCount =
                                categoryEvents.Count,

                            AverageDetectorCount =
                                categoryEvents.Average(
                                    item => item.Detectors.Count),

                            AverageFamilyCount =
                                categoryEvents.Average(
                                    item => item.Families.Count),

                            OneDetectorCount =
                                categoryEvents.LongCount(
                                    item =>
                                        item.Detectors.Count == 1),

                            OneFamilyCount =
                                categoryEvents.LongCount(
                                    item =>
                                        item.Families.Count == 1),

                            TwoOrMoreFamiliesCount =
                                categoryEvents.LongCount(
                                    item =>
                                        item.Families.Count >= 2),

                            ThreeOrMoreFamiliesCount =
                                categoryEvents.LongCount(
                                    item =>
                                        item.Families.Count >= 3),

                            PrincipalFamilyCombination =
                                categoryEvents
                                    .GroupBy(
                                        item =>
                                            item.FamilyCombination,
                                        StringComparer.OrdinalIgnoreCase)
                                    .OrderByDescending(
                                        combinationGroup =>
                                            combinationGroup.Count())
                                    .First()
                                    .Key,

                            PrincipalDetectorCombination =
                                categoryEvents
                                    .GroupBy(
                                        item =>
                                            item.DetectorCombination,
                                        StringComparer.OrdinalIgnoreCase)
                                    .OrderByDescending(
                                        combinationGroup =>
                                            combinationGroup.Count())
                                    .First()
                                    .Key
                        };
                    })
                .OrderByDescending(item => item.EventCount)
                .ToList();

        return new FamilyConsensusSummary
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            SourceFile =
                inputFile.FullName,

            TotalEvents =
                totalEvents,

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

            DetectorCountDistribution =
                detectorCountDistribution,

            FamilyCountDistribution =
                familyCountDistribution,

            FamilyParticipation =
                familyParticipation,

            FamilyCombinations =
                familyCombinations,

            IsolatedDetectors =
                isolatedDetectors,

            SameFamilyMultipleDetectorCombinations =
                sameFamilyMultipleDetectors,

            ByCategory =
                byCategory,

            OneDetectorEvents =
                events.LongCount(
                    item => item.Detectors.Count == 1),

            OneFamilyEvents =
                events.LongCount(
                    item => item.Families.Count == 1),

            MultipleDetectorsSameFamilyEvents =
                events.LongCount(
                    item =>
                        item.Detectors.Count >= 2 &&
                        item.Families.Count == 1),

            TwoOrMoreFamiliesEvents =
                events.LongCount(
                    item => item.Families.Count >= 2),

            ThreeOrMoreFamiliesEvents =
                events.LongCount(
                    item => item.Families.Count >= 3)
        };
    }

    private static List<FamilyConsensusEvent> ReadEvents(
        string csvPath,
        CancellationToken cancellationToken)
    {
        var result =
            new List<FamilyConsensusEvent>();

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
        RequireColumn(indexes, "inicioMinuto");
        RequireColumn(indexes, "fimMinuto");
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

            var detectors =
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
                detectors
                    .Select(ResolveFamily)
                    .Where(
                        family =>
                            family !=
                            ConsensusDetectorFamily.Unknown)
                    .Distinct()
                    .OrderBy(family => family)
                    .ToList();

            result.Add(
                new FamilyConsensusEvent
                {
                    TagName =
                        GetField(
                            fields,
                            indexes["tag"]),

                    StartMinute =
                        ParseInt(
                            GetField(
                                fields,
                                indexes["inicioMinuto"])),

                    EndMinute =
                        ParseInt(
                            GetField(
                                fields,
                                indexes["fimMinuto"])),

                    DurationMinutes =
                        ParseInt(
                            GetField(
                                fields,
                                indexes["duracaoMinutos"])),

                    Category =
                        GetField(
                            fields,
                            indexes["categoriaComportamental"]),

                    Detectors =
                        detectors,

                    Families =
                        families,

                    DetectorCombination =
                        detectors.Count > 0
                            ? string.Join(
                                " + ",
                                detectors)
                            : "SEM_DETECTOR",

                    FamilyCombination =
                        families.Count > 0
                            ? string.Join(
                                " + ",
                                families)
                            : "UNKNOWN"
                });
        }

        return result;
    }

    private static ConsensusDetectorFamily ResolveFamily(
        string detectorName)
    {
        if (EqualsName(
                detectorName,
                "Z-Score") ||
            EqualsName(
                detectorName,
                "Mediana e MAD"))
        {
            return ConsensusDetectorFamily.StatisticalDeviation;
        }

        if (EqualsName(
                detectorName,
                "EWMA"))
        {
            return ConsensusDetectorFamily.TrendAndSmoothing;
        }

        if (EqualsName(
                detectorName,
                "Taxa de variação") ||
            EqualsName(
                detectorName,
                "Mudança de patamar"))
        {
            return ConsensusDetectorFamily.Transition;
        }

        if (EqualsName(
                detectorName,
                "Persistência"))
        {
            return ConsensusDetectorFamily.TemporalConfirmation;
        }

        if (detectorName.StartsWith(
                "Multivariado/",
                StringComparison.OrdinalIgnoreCase))
        {
            return ConsensusDetectorFamily.Multivariate;
        }

        if (detectorName.Contains(
                "Isolation",
                StringComparison.OrdinalIgnoreCase))
        {
            return ConsensusDetectorFamily.MachineLearning;
        }

        return ConsensusDetectorFamily.Unknown;
    }

    private static string CreateEventsCsv(
        IEnumerable<FamilyConsensusEvent> events)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "tag,inicioMinuto,fimMinuto,duracaoMinutos,categoria," +
            "quantidadeDetectores,quantidadeFamilias,detectores,familias," +
            "combinacaoDetectores,combinacaoFamilias,detectorIsolado," +
            "multiplosDetectoresMesmaFamilia");

        foreach (var item in events)
        {
            builder
                .Append(Csv(item.TagName))
                .Append(',')
                .Append(item.StartMinute)
                .Append(',')
                .Append(item.EndMinute)
                .Append(',')
                .Append(item.DurationMinutes)
                .Append(',')
                .Append(Csv(item.Category))
                .Append(',')
                .Append(item.Detectors.Count)
                .Append(',')
                .Append(item.Families.Count)
                .Append(',')
                .Append(Csv(string.Join(" | ", item.Detectors)))
                .Append(',')
                .Append(Csv(string.Join(" | ", item.Families)))
                .Append(',')
                .Append(Csv(item.DetectorCombination))
                .Append(',')
                .Append(Csv(item.FamilyCombination))
                .Append(',')
                .Append(item.Detectors.Count == 1)
                .Append(',')
                .AppendLine(
                    (item.Detectors.Count >= 2 &&
                     item.Families.Count == 1)
                    .ToString());
        }

        return builder.ToString();
    }

    private static string CreateCategoriesCsv(
        FamilyConsensusSummary report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "categoria,eventos,mediaDetectores,mediaFamilias," +
            "eventosUmDetector,eventosUmaFamilia,eventosDuasOuMaisFamilias," +
            "eventosTresOuMaisFamilias,principalCombinacaoFamilias," +
            "principalCombinacaoDetectores");

        foreach (var item in report.ByCategory)
        {
            builder
                .Append(Csv(item.Category))
                .Append(',')
                .Append(item.EventCount)
                .Append(',')
                .Append(Number(item.AverageDetectorCount))
                .Append(',')
                .Append(Number(item.AverageFamilyCount))
                .Append(',')
                .Append(item.OneDetectorCount)
                .Append(',')
                .Append(item.OneFamilyCount)
                .Append(',')
                .Append(item.TwoOrMoreFamiliesCount)
                .Append(',')
                .Append(item.ThreeOrMoreFamiliesCount)
                .Append(',')
                .Append(Csv(item.PrincipalFamilyCombination))
                .Append(',')
                .AppendLine(Csv(item.PrincipalDetectorCombination));
        }

        return builder.ToString();
    }

    private static string CreateCombinationsCsv(
        FamilyConsensusSummary report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "combinacaoFamilias,quantidadeFamilias,eventos,percentualEventos," +
            "tags,categoriaPrincipal");

        foreach (var item in report.FamilyCombinations)
        {
            builder
                .Append(Csv(item.Combination))
                .Append(',')
                .Append(item.FamilyCount)
                .Append(',')
                .Append(item.EventCount)
                .Append(',')
                .Append(Number(item.EventRate))
                .Append(',')
                .Append(item.TagCount)
                .Append(',')
                .AppendLine(Csv(item.PrincipalCategory));
        }

        return builder.ToString();
    }

    private static string CreateIsolatedDetectorsCsv(
        FamilyConsensusSummary report)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "detector,familia,eventosIsolados,percentualTotal," +
            "eventosUmMinuto,tags,categoriaPrincipal");

        foreach (var item in report.IsolatedDetectors)
        {
            builder
                .Append(Csv(item.Detector))
                .Append(',')
                .Append(Csv(item.Family.ToString()))
                .Append(',')
                .Append(item.EventCount)
                .Append(',')
                .Append(Number(item.IsolatedEventRate))
                .Append(',')
                .Append(item.OneMinuteEventCount)
                .Append(',')
                .Append(item.TagCount)
                .Append(',')
                .AppendLine(Csv(item.PrincipalCategory));
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        FamilyConsensusSummary report)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Family Consensus Report</title>");
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
            ".value{font-size:28px;font-weight:800}table{width:100%;" +
            "border-collapse:collapse;font-size:13px}th,td{text-align:left;" +
            "padding:9px;border-bottom:1px solid #e5e7eb}th{background:#f9fafb}");
        html.AppendLine("</style></head><body><div class=\"container\">");

        html.AppendLine("<h1>Family Consensus Report</h1>");
        html.AppendLine(
            "<p>Anatomia do consenso dos detectores nos eventos do banco limpo.</p>");

        html.AppendLine("<div class=\"grid\">");

        AppendCard(
            html,
            "Eventos",
            report.TotalEvents.ToString("N0"));

        AppendCard(
            html,
            "Um detector",
            report.OneDetectorEvents.ToString("N0"));

        AppendCard(
            html,
            "Uma família",
            report.OneFamilyEvents.ToString("N0"));

        AppendCard(
            html,
            "Múltiplos detectores, mesma família",
            report.MultipleDetectorsSameFamilyEvents.ToString("N0"));

        AppendCard(
            html,
            "Duas ou mais famílias",
            report.TwoOrMoreFamiliesEvents.ToString("N0"));

        AppendCard(
            html,
            "Três ou mais famílias",
            report.ThreeOrMoreFamiliesEvents.ToString("N0"));

        html.AppendLine("</div>");

        html.AppendLine(
            "<section><h2>Distribuição por quantidade de famílias</h2>" +
            "<table><thead><tr><th>Famílias</th><th>Eventos</th>" +
            "<th>Percentual</th></tr></thead><tbody>");

        foreach (var item in report.FamilyCountDistribution)
        {
            html
                .Append("<tr><td>")
                .Append(item.Count)
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.EventRate.ToString("P1"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Participação das famílias</h2>" +
            "<table><thead><tr><th>Família</th><th>Eventos</th>" +
            "<th>Participação</th><th>Isolados</th><th>Tags</th>" +
            "<th>Categorias</th></tr></thead><tbody>");

        foreach (var item in report.FamilyParticipation)
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Family.ToString()))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.EventRate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.IsolatedEventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.CategoryCount.ToString("N0"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Detectores mais isolados</h2>" +
            "<table><thead><tr><th>Detector</th><th>Família</th>" +
            "<th>Eventos isolados</th><th>Um minuto</th><th>Tags</th>" +
            "<th>Categoria principal</th></tr></thead><tbody>");

        foreach (var item in report.IsolatedDetectors.Take(100))
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Detector))
                .Append("</td><td>")
                .Append(Encode(item.Family.ToString()))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.OneMinuteEventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.TagCount.ToString("N0"))
                .Append("</td><td>")
                .Append(Encode(item.PrincipalCategory))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Consenso por categoria</h2>" +
            "<table><thead><tr><th>Categoria</th><th>Eventos</th>" +
            "<th>Média detectores</th><th>Média famílias</th>" +
            "<th>Uma família</th><th>2+ famílias</th>" +
            "<th>Combinação principal</th></tr></thead><tbody>");

        foreach (var item in report.ByCategory)
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Category))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.AverageDetectorCount.ToString("F2"))
                .Append("</td><td>")
                .Append(item.AverageFamilyCount.ToString("F2"))
                .Append("</td><td>")
                .Append(item.OneFamilyCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.TwoOrMoreFamiliesCount.ToString("N0"))
                .Append("</td><td>")
                .Append(Encode(item.PrincipalFamilyCombination))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Combinações de famílias</h2>" +
            "<table><thead><tr><th>Combinação</th><th>Famílias</th>" +
            "<th>Eventos</th><th>Percentual</th><th>Tags</th>" +
            "<th>Categoria principal</th></tr></thead><tbody>");

        foreach (var item in report.FamilyCombinations.Take(100))
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.Combination))
                .Append("</td><td>")
                .Append(item.FamilyCount)
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
        FamilyConsensusSummary report,
        string summaryPath,
        string eventsPath,
        string categoriesPath,
        string combinationsPath,
        string isolatedPath,
        string htmlPath)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" FAMILY CONSENSUS FINALIZADO");
        Console.WriteLine("========================================");
        Console.WriteLine($"Eventos: {report.TotalEvents:N0}");
        Console.WriteLine($"Um detector: {report.OneDetectorEvents:N0}");
        Console.WriteLine($"Uma família: {report.OneFamilyEvents:N0}");
        Console.WriteLine(
            $"Múltiplos detectores da mesma família: " +
            $"{report.MultipleDetectorsSameFamilyEvents:N0}");
        Console.WriteLine(
            $"Duas ou mais famílias: " +
            $"{report.TwoOrMoreFamiliesEvents:N0}");
        Console.WriteLine(
            $"Três ou mais famílias: " +
            $"{report.ThreeOrMoreFamiliesEvents:N0}");
        Console.WriteLine();

        foreach (var item in report.FamilyCountDistribution)
        {
            Console.WriteLine(
                $"{item.Count} família(s): " +
                $"{item.EventCount:N0} ({item.EventRate:P1})");
        }

        Console.WriteLine();
        Console.WriteLine($"JSON: {summaryPath}");
        Console.WriteLine($"Eventos: {eventsPath}");
        Console.WriteLine($"Categorias: {categoriesPath}");
        Console.WriteLine($"Combinações: {combinationsPath}");
        Console.WriteLine($"Detectores isolados: {isolatedPath}");
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

public enum ConsensusDetectorFamily
{
    Unknown = 0,
    StatisticalDeviation = 1,
    TrendAndSmoothing = 2,
    Transition = 3,
    TemporalConfirmation = 4,
    Multivariate = 5,
    MachineLearning = 6
}

public sealed class FamilyConsensusEvent
{
    public string TagName { get; init; } = "";
    public int StartMinute { get; init; }
    public int EndMinute { get; init; }
    public int DurationMinutes { get; init; }
    public string Category { get; init; } = "";

    public IReadOnlyList<string> Detectors { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<ConsensusDetectorFamily> Families { get; init; } =
        Array.Empty<ConsensusDetectorFamily>();

    public string DetectorCombination { get; init; } = "";
    public string FamilyCombination { get; init; } = "";
}

public sealed class FamilyConsensusSummary
{
    public DateTime GeneratedAtUtc { get; init; }
    public string SourceFile { get; init; } = "";
    public int TotalEvents { get; init; }
    public int TagCount { get; init; }
    public int CategoryCount { get; init; }

    public long OneDetectorEvents { get; init; }
    public long OneFamilyEvents { get; init; }
    public long MultipleDetectorsSameFamilyEvents { get; init; }
    public long TwoOrMoreFamiliesEvents { get; init; }
    public long ThreeOrMoreFamiliesEvents { get; init; }

    public IReadOnlyList<ConsensusCountDistribution>
        DetectorCountDistribution { get; init; } =
        Array.Empty<ConsensusCountDistribution>();

    public IReadOnlyList<ConsensusCountDistribution>
        FamilyCountDistribution { get; init; } =
        Array.Empty<ConsensusCountDistribution>();

    public IReadOnlyList<FamilyParticipationSummary>
        FamilyParticipation { get; init; } =
        Array.Empty<FamilyParticipationSummary>();

    public IReadOnlyList<FamilyCombinationSummary>
        FamilyCombinations { get; init; } =
        Array.Empty<FamilyCombinationSummary>();

    public IReadOnlyList<IsolatedDetectorSummary>
        IsolatedDetectors { get; init; } =
        Array.Empty<IsolatedDetectorSummary>();

    public IReadOnlyList<SameFamilyCombinationSummary>
        SameFamilyMultipleDetectorCombinations { get; init; } =
        Array.Empty<SameFamilyCombinationSummary>();

    public IReadOnlyList<CategoryConsensusSummary>
        ByCategory { get; init; } =
        Array.Empty<CategoryConsensusSummary>();
}

public sealed class ConsensusCountDistribution
{
    public int Count { get; init; }
    public long EventCount { get; init; }
    public double EventRate { get; init; }
}

public sealed class FamilyParticipationSummary
{
    public ConsensusDetectorFamily Family { get; init; }
    public long EventCount { get; init; }
    public double EventRate { get; init; }
    public long IsolatedEventCount { get; init; }
    public long TagCount { get; init; }
    public long CategoryCount { get; init; }
}

public sealed class FamilyCombinationSummary
{
    public string Combination { get; init; } = "";
    public int FamilyCount { get; init; }
    public long EventCount { get; init; }
    public double EventRate { get; init; }
    public long TagCount { get; init; }
    public string PrincipalCategory { get; init; } = "";
}

public sealed class IsolatedDetectorSummary
{
    public string Detector { get; init; } = "";
    public ConsensusDetectorFamily Family { get; init; }
    public long EventCount { get; init; }
    public double IsolatedEventRate { get; init; }
    public long OneMinuteEventCount { get; init; }
    public long TagCount { get; init; }
    public string PrincipalCategory { get; init; } = "";
}

public sealed class SameFamilyCombinationSummary
{
    public string DetectorCombination { get; init; } = "";
    public ConsensusDetectorFamily Family { get; init; }
    public int DetectorCount { get; init; }
    public long EventCount { get; init; }
    public string PrincipalCategory { get; init; } = "";
}

public sealed class CategoryConsensusSummary
{
    public string Category { get; init; } = "";
    public int EventCount { get; init; }
    public double AverageDetectorCount { get; init; }
    public double AverageFamilyCount { get; init; }
    public long OneDetectorCount { get; init; }
    public long OneFamilyCount { get; init; }
    public long TwoOrMoreFamiliesCount { get; init; }
    public long ThreeOrMoreFamiliesCount { get; init; }
    public string PrincipalFamilyCombination { get; init; } = "";
    public string PrincipalDetectorCombination { get; init; } = "";
}
