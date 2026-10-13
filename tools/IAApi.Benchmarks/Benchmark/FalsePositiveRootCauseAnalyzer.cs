using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IAApi.Benchmark;

/// <summary>
/// Analisa os eventos agrupados do banco limpo e adiciona
/// classificações estruturais simples para investigação de falsos positivos.
///
/// Esta etapa não altera nenhum detector, score ou voto.
/// </summary>
public sealed class FalsePositiveRootCauseAnalyzer
{
    private const double ZeroTolerance = 1e-9;
    private const double ConstantTolerance = 1e-9;
    private const int OneDayMinutes = 1440;

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
                .GetFiles("calibration_all_events_*.csv")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

        if (inputFile is null)
        {
            throw new FileNotFoundException(
                "Nenhum arquivo calibration_all_events_*.csv foi encontrado.");
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" ANÁLISE ESTRUTURAL DOS FALSOS POSITIVOS");
        Console.WriteLine("========================================");
        Console.WriteLine($"Arquivo: {inputFile.FullName}");

        var events =
            ReadEvents(
                inputFile.FullName,
                cancellationToken);

        Console.WriteLine($"Eventos encontrados: {events.Count:N0}");

        foreach (var item in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            item.ZeroDuringEvent =
                Math.Abs(item.MeanDuring) <= ZeroTolerance;

            item.StartedAtMinuteZero =
                item.StartMinute == 0;

            item.LastedExactlyOneDay =
                item.DurationMinutes == OneDayMinutes;

            item.ConstantDuringEvent =
                Math.Abs(item.StandardDeviationDuring) <= ConstantTolerance;

            item.ReturnedAfterEvent =
                item.ReturnedToPreviousLevel;

            var classification =
                ClassifyBehavior(item);

            item.BehaviorCategory =
                classification.Category;

            item.ClassificationConfidence =
                classification.Confidence;

            item.ClassificationExplanation =
                classification.Explanation;

            item.RecommendedInvestigation =
                classification.RecommendedInvestigation;
        }

        var summary =
            BuildSummary(events);

        var identifier =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);

        var csvPath =
            Path.Combine(
                resultDirectory,
                $"false_positive_structural_analysis_{identifier}.csv");

        var jsonPath =
            Path.Combine(
                resultDirectory,
                $"false_positive_structural_summary_{identifier}.json");

        var htmlPath =
            Path.Combine(
                resultDirectory,
                $"false_positive_structural_report_{identifier}.html");

        await File.WriteAllTextAsync(
            csvPath,
            CreateCsv(events),
            cancellationToken);

        await File.WriteAllTextAsync(
            jsonPath,
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
            htmlPath,
            CreateHtml(summary, events),
            cancellationToken);

        PrintSummary(
            summary,
            csvPath,
            jsonPath,
            htmlPath);
    }

    private static List<FalsePositiveRootCauseEvent>
        ReadEvents(
            string csvPath,
            CancellationToken cancellationToken)
    {
        var result =
            new List<FalsePositiveRootCauseEvent>();

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
        RequireColumn(indexes, "mediaAntes");
        RequireColumn(indexes, "desvioAntes");
        RequireColumn(indexes, "mediaDurante");
        RequireColumn(indexes, "desvioDurante");
        RequireColumn(indexes, "mediaDepois");
        RequireColumn(indexes, "desvioDepois");
        RequireColumn(indexes, "mudancaAbsoluta");
        RequireColumn(indexes, "mudancaPercentual");
        RequireColumn(indexes, "retornouAoNivelAnterior");

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields =
                ParseCsvLine(line);

            result.Add(
                new FalsePositiveRootCauseEvent
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

                    Detectors =
                        GetField(
                                fields,
                                indexes["detectores"])
                            .Split(
                                '|',
                                StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries)
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .ToList(),

                    MeanBefore =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["mediaAntes"])),

                    StandardDeviationBefore =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["desvioAntes"])),

                    MeanDuring =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["mediaDurante"])),

                    StandardDeviationDuring =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["desvioDurante"])),

                    MeanAfter =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["mediaDepois"])),

                    StandardDeviationAfter =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["desvioDepois"])),

                    AbsoluteChange =
                        Math.Abs(
                            ParseDouble(
                                GetField(
                                    fields,
                                    indexes["mudancaAbsoluta"]))),

                    PercentageChange =
                        ParseDouble(
                            GetField(
                                fields,
                                indexes["mudancaPercentual"])),

                    ReturnedToPreviousLevel =
                        ParseBool(
                            GetField(
                                fields,
                                indexes["retornouAoNivelAnterior"]))
                });
        }

        return result;
    }

    private static FalsePositiveBehaviorDecision ClassifyBehavior(
        FalsePositiveRootCauseEvent item)
    {
        /*
         * Importante:
         * esta classificação descreve o COMPORTAMENTO observado.
         * Ela não afirma, sozinha, que houve falha de sensor,
         * comunicação, parada ou qualquer causa operacional real.
         */

        if (LooksDiscreteTag(item.TagName))
        {
            return new FalsePositiveBehaviorDecision
            {
                Category =
                    "Tag discreta analisada como sinal contínuo",

                Confidence =
                    95,

                Explanation =
                    "O nome da tag indica modo, estado, código ou comando. " +
                    "Mudanças normais de estado podem ser interpretadas como " +
                    "anomalias pelos detectores estatísticos contínuos.",

                RecommendedInvestigation =
                    "Separar tags discretas e validar um detector de mudança de estado."
            };
        }

        if (item.StartedAtMinuteZero)
        {
            return new FalsePositiveBehaviorDecision
            {
                Category =
                    "Evento na fronteira inicial do dataset",

                Confidence =
                    98,

                Explanation =
                    "O evento começou no primeiro minuto disponível. " +
                    "Não existe uma janela anterior completa para confirmar " +
                    "se houve transição real ou apenas condição inicial.",

                RecommendedInvestigation =
                    "Excluir ou marcar eventos sem contexto anterior suficiente."
            };
        }

        if (item.ZeroDuringEvent &&
            item.ConstantDuringEvent &&
            item.ReturnedAfterEvent)
        {
            return new FalsePositiveBehaviorDecision
            {
                Category =
                    "Patamar zero temporário com retorno",

                Confidence =
                    97,

                Explanation =
                    $"A média durante o evento foi zero, o sinal permaneceu " +
                    $"constante por {item.DurationMinutes} minuto(s) e depois " +
                    "voltou ao nível anterior.",

                RecommendedInvestigation =
                    "Cruzar com estado de operação, parada programada, qualidade " +
                    "do dado e disponibilidade do equipamento."
            };
        }

        if (item.ZeroDuringEvent &&
            item.ConstantDuringEvent &&
            !item.ReturnedAfterEvent)
        {
            return new FalsePositiveBehaviorDecision
            {
                Category =
                    "Patamar zero sem retorno confirmado",

                Confidence =
                    90,

                Explanation =
                    "O sinal permaneceu constante em zero durante o evento, " +
                    "mas a janela posterior não confirmou retorno ao nível anterior.",

                RecommendedInvestigation =
                    "Verificar fim do dataset, indisponibilidade de dados e estado operacional."
            };
        }

        /*
         * Para evento de um único minuto, o desvio durante sempre tende a zero.
         * Portanto, não devemos interpretar ConstantDuringEvent como sensor travado.
         */
        if (item.DurationMinutes == 1)
        {
            return new FalsePositiveBehaviorDecision
            {
                Category =
                    "Impulso isolado de um minuto",

                Confidence =
                    92,

                Explanation =
                    "O evento possui apenas uma amostra agrupada. " +
                    "O desvio durante igual a zero é consequência matemática " +
                    "da janela de uma única observação, não evidência de travamento.",

                RecommendedInvestigation =
                    "Avaliar debounce, persistência mínima e agrupamento temporal."
            };
        }

        if (item.ReturnedAfterEvent)
        {
            var increased =
                item.MeanDuring >
                item.MeanBefore;

            return new FalsePositiveBehaviorDecision
            {
                Category =
                    increased
                        ? "Elevação temporária com retorno"
                        : "Queda temporária com retorno",

                Confidence =
                    item.DurationMinutes <= 30
                        ? 90
                        : 82,

                Explanation =
                    $"A média mudou de {item.MeanBefore:G6} para " +
                    $"{item.MeanDuring:G6} e retornou para " +
                    $"{item.MeanAfter:G6}.",

                RecommendedInvestigation =
                    item.DurationMinutes <= 30
                        ? "Avaliar debounce ou persistência mínima."
                        : "Cruzar com ciclos, receitas e transições operacionais."
            };
        }

        var distanceAfterToDuring =
            Math.Abs(
                item.MeanAfter -
                item.MeanDuring);

        var distanceAfterToBefore =
            Math.Abs(
                item.MeanAfter -
                item.MeanBefore);

        if (distanceAfterToDuring <
            distanceAfterToBefore)
        {
            return new FalsePositiveBehaviorDecision
            {
                Category =
                    "Mudança sustentada de patamar",

                Confidence =
                    88,

                Explanation =
                    $"Após o evento, a média ({item.MeanAfter:G6}) ficou mais " +
                    $"próxima do nível durante ({item.MeanDuring:G6}) do que " +
                    $"do nível anterior ({item.MeanBefore:G6}).",

                RecommendedInvestigation =
                    "Cruzar com modo operacional e avaliar baseline específico por regime."
            };
        }

        if (item.ConstantDuringEvent)
        {
            return new FalsePositiveBehaviorDecision
            {
                Category =
                    "Patamar constante não conclusivo",

                Confidence =
                    70,

                Explanation =
                    $"O sinal permaneceu praticamente constante por " +
                    $"{item.DurationMinutes} minuto(s), mas não há evidência " +
                    "suficiente para atribuir uma causa operacional.",

                RecommendedInvestigation =
                    "Comparar com precisão do instrumento, estado da máquina e dados correlacionados."
            };
        }

        return new FalsePositiveBehaviorDecision
        {
            Category =
                "Comportamento não conclusivo",

            Confidence =
                50,

            Explanation =
                "As métricas agregadas de antes, durante e depois não permitem " +
                "distinguir com segurança o padrão do evento.",

            RecommendedInvestigation =
                "Analisar a série minuto a minuto e as tags correlacionadas."
        };
    }

    private static bool LooksDiscreteTag(
        string tagName)
    {
        return
            tagName.Contains(
                "STATE",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.Contains(
                "STATUS",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.Contains(
                "MODE",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.Contains(
                "ONLINE",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.Contains(
                "COMMAND",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.EndsWith(
                ".CODE",
                StringComparison.OrdinalIgnoreCase);
    }

    private static FalsePositiveStructuralSummary BuildSummary(
        IReadOnlyCollection<FalsePositiveRootCauseEvent> events)
    {
        var categories =
            events
                .GroupBy(
                    item => item.BehaviorCategory,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        new FalsePositiveBehaviorCategorySummary
                        {
                            Category =
                                group.Key,

                            EventCount =
                                group.LongCount(),

                            TagCount =
                                group
                                    .Select(item => item.TagName)
                                    .Distinct(
                                        StringComparer.OrdinalIgnoreCase)
                                    .LongCount(),

                            AverageConfidence =
                                group.Average(
                                    item =>
                                        item.ClassificationConfidence),

                            AverageDurationMinutes =
                                group.Average(
                                    item =>
                                        item.DurationMinutes)
                        })
                .OrderByDescending(
                    item => item.EventCount)
                .ToList();

        return new FalsePositiveStructuralSummary
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            Categories =
                categories,

            TotalEvents =
                events.Count,

            ZeroDuringEventCount =
                events.LongCount(
                    item => item.ZeroDuringEvent),

            StartedAtMinuteZeroCount =
                events.LongCount(
                    item => item.StartedAtMinuteZero),

            LastedExactlyOneDayCount =
                events.LongCount(
                    item => item.LastedExactlyOneDay),

            ConstantDuringEventCount =
                events.LongCount(
                    item => item.ConstantDuringEvent),

            ReturnedAfterEventCount =
                events.LongCount(
                    item => item.ReturnedAfterEvent),

            ZeroAndConstantCount =
                events.LongCount(
                    item =>
                        item.ZeroDuringEvent &&
                        item.ConstantDuringEvent),

            ZeroOneDayAndReturnedCount =
                events.LongCount(
                    item =>
                        item.ZeroDuringEvent &&
                        item.LastedExactlyOneDay &&
                        item.ReturnedAfterEvent),

            StartedAtZeroAndNoReturnCount =
                events.LongCount(
                    item =>
                        item.StartedAtMinuteZero &&
                        !item.ReturnedAfterEvent),

            Tags =
                events
                    .GroupBy(
                        item => item.TagName,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(
                        group =>
                            new FalsePositiveStructuralTagSummary
                            {
                                TagName =
                                    group.Key,

                                EventCount =
                                    group.LongCount(),

                                ZeroDuringEventCount =
                                    group.LongCount(
                                        item =>
                                            item.ZeroDuringEvent),

                                OneDayEventCount =
                                    group.LongCount(
                                        item =>
                                            item.LastedExactlyOneDay),

                                ConstantDuringEventCount =
                                    group.LongCount(
                                        item =>
                                            item.ConstantDuringEvent),

                                ReturnedAfterEventCount =
                                    group.LongCount(
                                        item =>
                                            item.ReturnedAfterEvent)
                            })
                    .OrderByDescending(
                        item => item.EventCount)
                    .ToList()
        };
    }

    private static string CreateCsv(
        IEnumerable<FalsePositiveRootCauseEvent> events)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "tag,inicioMinuto,fimMinuto,duracaoMinutos,detectores," +
            "mediaAntes,desvioAntes,mediaDurante,desvioDurante," +
            "mediaDepois,desvioDepois,mudancaAbsoluta,mudancaPercentual," +
            "retornouAoNivelAnterior,valorZeradoDurante,comecouNoMinutoZero," +
            "durouExatamenteUmDia,valorConstanteDurante,retornouDepois," +
            "categoriaComportamental,confiancaClassificacao," +
            "explicacaoClassificacao,investigacaoRecomendada");

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
                .Append(
                    Csv(
                        string.Join(
                            " | ",
                            item.Detectors)))
                .Append(',')
                .Append(Number(item.MeanBefore))
                .Append(',')
                .Append(Number(item.StandardDeviationBefore))
                .Append(',')
                .Append(Number(item.MeanDuring))
                .Append(',')
                .Append(Number(item.StandardDeviationDuring))
                .Append(',')
                .Append(Number(item.MeanAfter))
                .Append(',')
                .Append(Number(item.StandardDeviationAfter))
                .Append(',')
                .Append(Number(item.AbsoluteChange))
                .Append(',')
                .Append(Number(item.PercentageChange))
                .Append(',')
                .Append(item.ReturnedToPreviousLevel)
                .Append(',')
                .Append(item.ZeroDuringEvent)
                .Append(',')
                .Append(item.StartedAtMinuteZero)
                .Append(',')
                .Append(item.LastedExactlyOneDay)
                .Append(',')
                .Append(item.ConstantDuringEvent)
                .Append(',')
                .Append(item.ReturnedAfterEvent)
                .Append(',')
                .Append(Csv(item.BehaviorCategory))
                .Append(',')
                .Append(Number(item.ClassificationConfidence))
                .Append(',')
                .Append(Csv(item.ClassificationExplanation))
                .Append(',')
                .AppendLine(Csv(item.RecommendedInvestigation));
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        FalsePositiveStructuralSummary summary,
        IReadOnlyCollection<FalsePositiveRootCauseEvent> events)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Análise estrutural de falsos positivos</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;" +
            "margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1500px;margin:auto}.grid{display:grid;" +
            "grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:14px}");
        html.AppendLine(
            ".card,section{background:white;border-radius:12px;padding:18px;" +
            "margin:16px 0;box-shadow:0 1px 4px #0002}");
        html.AppendLine(
            ".value{font-size:30px;font-weight:800}" +
            "table{width:100%;border-collapse:collapse;font-size:13px}" +
            "th,td{text-align:left;padding:9px;border-bottom:1px solid #e5e7eb}" +
            "th{background:#f9fafb}");
        html.AppendLine("</style></head><body><div class=\"container\">");

        html.AppendLine("<h1>Análise estrutural dos falsos positivos</h1>");
        html.AppendLine("<div class=\"grid\">");

        AppendCard(
            html,
            "Eventos analisados",
            summary.TotalEvents.ToString("N0"));

        AppendCard(
            html,
            "Valor zerado durante",
            summary.ZeroDuringEventCount.ToString("N0"));

        AppendCard(
            html,
            "Começaram no minuto zero",
            summary.StartedAtMinuteZeroCount.ToString("N0"));

        AppendCard(
            html,
            "Duraram exatamente um dia",
            summary.LastedExactlyOneDayCount.ToString("N0"));

        AppendCard(
            html,
            "Constantes durante",
            summary.ConstantDuringEventCount.ToString("N0"));

        AppendCard(
            html,
            "Retornaram depois",
            summary.ReturnedAfterEventCount.ToString("N0"));

        AppendCard(
            html,
            "Zero + constante",
            summary.ZeroAndConstantCount.ToString("N0"));

        AppendCard(
            html,
            "Zero + um dia + retorno",
            summary.ZeroOneDayAndReturnedCount.ToString("N0"));

        html.AppendLine("</div>");


        html.AppendLine(
            "<section><h2>Classificação comportamental</h2>" +
            "<table><thead><tr><th>Categoria</th><th>Eventos</th>" +
            "<th>Tags</th><th>Confiança média</th><th>Duração média</th>" +
            "</tr></thead><tbody>");

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

        html.AppendLine(
            "<section><h2>Resumo por tag</h2>" +
            "<table><thead><tr><th>Tag</th><th>Eventos</th>" +
            "<th>Zerados</th><th>1 dia</th><th>Constantes</th>" +
            "<th>Retornaram</th></tr></thead><tbody>");

        foreach (var item in summary.Tags.Take(100))
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.TagName))
                .Append("</td><td>")
                .Append(item.EventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ZeroDuringEventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.OneDayEventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ConstantDuringEventCount.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ReturnedAfterEventCount.ToString("N0"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");

        html.AppendLine(
            "<section><h2>Amostra de eventos estruturais</h2>" +
            "<table><thead><tr><th>Tag</th><th>Evento</th><th>Duração</th>" +
            "<th>Zerado</th><th>Minuto zero</th><th>1 dia</th>" +
            "<th>Constante</th><th>Retornou</th><th>Categoria</th>" +
            "<th>Confiança</th></tr></thead><tbody>");

        foreach (var item in events
                     .Where(
                         eventItem =>
                             eventItem.ZeroDuringEvent ||
                             eventItem.LastedExactlyOneDay ||
                             eventItem.ConstantDuringEvent ||
                             eventItem.StartedAtMinuteZero)
                     .Take(500))
        {
            html
                .Append("<tr><td>")
                .Append(Encode(item.TagName))
                .Append("</td><td>")
                .Append(item.StartMinute)
                .Append("–")
                .Append(item.EndMinute)
                .Append("</td><td>")
                .Append(item.DurationMinutes)
                .Append(" min</td><td>")
                .Append(item.ZeroDuringEvent ? "Sim" : "Não")
                .Append("</td><td>")
                .Append(item.StartedAtMinuteZero ? "Sim" : "Não")
                .Append("</td><td>")
                .Append(item.LastedExactlyOneDay ? "Sim" : "Não")
                .Append("</td><td>")
                .Append(item.ConstantDuringEvent ? "Sim" : "Não")
                .Append("</td><td>")
                .Append(item.ReturnedAfterEvent ? "Sim" : "Não")
                .Append("</td><td>")
                .Append(Encode(item.BehaviorCategory))
                .Append("</td><td>")
                .Append(item.ClassificationConfidence.ToString("F1"))
                .AppendLine("%</td></tr>");
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

    private static void PrintSummary(
        FalsePositiveStructuralSummary summary,
        string csvPath,
        string jsonPath,
        string htmlPath)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" ANÁLISE ESTRUTURAL FINALIZADA");
        Console.WriteLine("========================================");
        Console.WriteLine($"Eventos analisados: {summary.TotalEvents:N0}");
        Console.WriteLine($"Valor zerado durante: {summary.ZeroDuringEventCount:N0}");
        Console.WriteLine($"Começaram no minuto zero: {summary.StartedAtMinuteZeroCount:N0}");
        Console.WriteLine($"Duraram exatamente um dia: {summary.LastedExactlyOneDayCount:N0}");
        Console.WriteLine($"Constantes durante: {summary.ConstantDuringEventCount:N0}");
        Console.WriteLine($"Retornaram depois: {summary.ReturnedAfterEventCount:N0}");
        Console.WriteLine($"Zero + constante: {summary.ZeroAndConstantCount:N0}");
        Console.WriteLine($"Zero + um dia + retorno: {summary.ZeroOneDayAndReturnedCount:N0}");
        Console.WriteLine();
        Console.WriteLine("Classificação comportamental:");

        foreach (var item in summary.Categories)
        {
            Console.WriteLine(
                $"{item.Category,-48} {item.EventCount,8:N0}");
        }

        Console.WriteLine();
        Console.WriteLine($"CSV:  {csvPath}");
        Console.WriteLine($"JSON: {jsonPath}");
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
}

public sealed class FalsePositiveRootCauseEvent
{
    public string TagName { get; set; } = "";
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public int DurationMinutes { get; set; }
    public List<string> Detectors { get; set; } = [];

    public double MeanBefore { get; set; }
    public double StandardDeviationBefore { get; set; }
    public double MeanDuring { get; set; }
    public double StandardDeviationDuring { get; set; }
    public double MeanAfter { get; set; }
    public double StandardDeviationAfter { get; set; }

    public double AbsoluteChange { get; set; }
    public double PercentageChange { get; set; }
    public bool ReturnedToPreviousLevel { get; set; }

    public bool ZeroDuringEvent { get; set; }
    public bool StartedAtMinuteZero { get; set; }
    public bool LastedExactlyOneDay { get; set; }
    public bool ConstantDuringEvent { get; set; }
    public bool ReturnedAfterEvent { get; set; }

    public string BehaviorCategory { get; set; } = "";
    public double ClassificationConfidence { get; set; }
    public string ClassificationExplanation { get; set; } = "";
    public string RecommendedInvestigation { get; set; } = "";
}

public sealed class FalsePositiveBehaviorDecision
{
    public string Category { get; set; } = "";
    public double Confidence { get; set; }
    public string Explanation { get; set; } = "";
    public string RecommendedInvestigation { get; set; } = "";
}

public sealed class FalsePositiveBehaviorCategorySummary
{
    public string Category { get; set; } = "";
    public long EventCount { get; set; }
    public long TagCount { get; set; }
    public double AverageConfidence { get; set; }
    public double AverageDurationMinutes { get; set; }
}

public sealed class FalsePositiveStructuralTagSummary
{
    public string TagName { get; set; } = "";
    public long EventCount { get; set; }
    public long ZeroDuringEventCount { get; set; }
    public long OneDayEventCount { get; set; }
    public long ConstantDuringEventCount { get; set; }
    public long ReturnedAfterEventCount { get; set; }
}

public sealed class FalsePositiveStructuralSummary
{
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalEvents { get; set; }

    public long ZeroDuringEventCount { get; set; }
    public long StartedAtMinuteZeroCount { get; set; }
    public long LastedExactlyOneDayCount { get; set; }
    public long ConstantDuringEventCount { get; set; }
    public long ReturnedAfterEventCount { get; set; }

    public long ZeroAndConstantCount { get; set; }
    public long ZeroOneDayAndReturnedCount { get; set; }
    public long StartedAtZeroAndNoReturnCount { get; set; }

    public List<FalsePositiveBehaviorCategorySummary> Categories { get; set; } = [];
    public List<FalsePositiveStructuralTagSummary> Tags { get; set; } = [];
}