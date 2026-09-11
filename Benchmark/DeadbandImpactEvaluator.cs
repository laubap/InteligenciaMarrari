using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace IAApi.Benchmark;

/// <summary>
/// Simula o impacto de P90, P95 e P99 como variação mínima relevante,
/// sem alterar nenhum detector, score ou voto.
///
/// Fontes utilizadas:
/// - banco limpo: aprende os percentis por tag;
/// - calibration_all_events_*.csv: avalia os eventos do banco limpo;
/// - benchmark_evaluation_v2_*.json: faz uma triagem de risco no Oracle.
///
/// Importante:
/// a análise do Oracle é uma triagem baseada no início do primeiro evento
/// detectado de cada condição. Ela não substitui a reexecução completa do
/// benchmark depois que o deadband for realmente integrado a um detector.
/// </summary>
public sealed class DeadbandImpactEvaluator
{
    private const double Epsilon = 1e-12;

    public async Task EvaluateAsync(
        BenchmarkOptions options,
        string resultDirectory,
        CancellationToken cancellationToken = default)
    {
        options.Validar();

        if (!Directory.Exists(resultDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Diretório de resultados não encontrado: {resultDirectory}");
        }

        var calibrationCsv =
            FindLatestFile(
                resultDirectory,
                "calibration_all_events_*.csv");

        var oracleEvaluationJson =
            FindLatestFile(
                resultDirectory,
                "benchmark_evaluation_v2_*.json");

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" SIMULAÇÃO DE IMPACTO DO DEADBAND");
        Console.WriteLine("========================================");
        Console.WriteLine($"Banco limpo: {options.CleanDatabasePath}");
        Console.WriteLine($"Eventos limpos: {calibrationCsv}");
        Console.WriteLine($"Avaliação Oracle: {oracleEvaluationJson}");
        Console.WriteLine();

        Console.WriteLine(
            "1/4 Calculando P90, P95 e P99 por tag no banco limpo...");

        var profiles =
            CalculateProfiles(
                options.CleanDatabasePath,
                cancellationToken);

        Console.WriteLine(
            $"Perfis calculados: {profiles.Count:N0}");

        Console.WriteLine(
            "2/4 Avaliando eventos agrupados do banco limpo...");

        var cleanEvents =
            ReadCleanEvents(
                calibrationCsv,
                profiles,
                cancellationToken);

        Console.WriteLine(
            $"Eventos limpos avaliados: {cleanEvents.Count:N0}");

        Console.WriteLine(
            "3/4 Fazendo triagem das condições do Oracle...");

        var oracleRisks =
            ReadOracleRisks(
                oracleEvaluationJson,
                options.AnomalyDatabasePath,
                profiles,
                cancellationToken);

        Console.WriteLine(
            $"Condições Oracle avaliadas: {oracleRisks.Count:N0}");

        Console.WriteLine(
            "4/4 Gerando relatórios...");

        var summary =
            BuildSummary(
                profiles,
                cleanEvents,
                oracleRisks);

        var id =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);

        var jsonPath =
            Path.Combine(
                resultDirectory,
                $"deadband_impact_summary_{id}.json");

        var eventsCsvPath =
            Path.Combine(
                resultDirectory,
                $"deadband_impact_events_{id}.csv");

        var detectorsCsvPath =
            Path.Combine(
                resultDirectory,
                $"deadband_impact_detectors_{id}.csv");

        var oracleCsvPath =
            Path.Combine(
                resultDirectory,
                $"deadband_impact_oracle_{id}.csv");

        var htmlPath =
            Path.Combine(
                resultDirectory,
                $"deadband_impact_report_{id}.html");

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
            eventsCsvPath,
            CreateEventsCsv(cleanEvents),
            cancellationToken);

        await File.WriteAllTextAsync(
            detectorsCsvPath,
            CreateDetectorsCsv(summary.Detectors),
            cancellationToken);

        await File.WriteAllTextAsync(
            oracleCsvPath,
            CreateOracleCsv(oracleRisks),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            CreateHtml(summary),
            cancellationToken);

        PrintSummary(
            summary,
            jsonPath,
            eventsCsvPath,
            detectorsCsvPath,
            oracleCsvPath,
            htmlPath);
    }

    private static string FindLatestFile(
        string directory,
        string pattern)
    {
        var file =
            new DirectoryInfo(directory)
                .GetFiles(pattern)
                .OrderByDescending(
                    item => item.LastWriteTimeUtc)
                .FirstOrDefault();

        return file?.FullName
            ?? throw new FileNotFoundException(
                $"Nenhum arquivo '{pattern}' foi encontrado em {directory}.");
    }

    private static Dictionary<string, DeadbandTagProfile>
        CalculateProfiles(
            string databasePath,
            CancellationToken cancellationToken)
    {
        var reader =
            new SqliteDatasetReader(databasePath);

        var result =
            new Dictionary<string, DeadbandTagProfile>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var tag in reader.GetTags())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var samples =
                reader.GetSamples(tag.TagId)
                    .Select(item => item.Value)
                    .Where(double.IsFinite)
                    .ToList();

            if (samples.Count < 10)
            {
                result[tag.TagName] =
                    new DeadbandTagProfile
                    {
                        TagName = tag.TagName,
                        Unit = tag.Unit,
                        SampleCount = samples.Count,
                        Applicable = false,
                        Reason =
                            "Histórico insuficiente."
                    };

                continue;
            }

            var variations =
                new List<double>(
                    Math.Max(
                        0,
                        samples.Count - 1));

            for (var index = 1;
                 index < samples.Count;
                 index++)
            {
                variations.Add(
                    Math.Abs(
                        samples[index] -
                        samples[index - 1]));
            }

            var distinctValues =
                samples
                    .Select(
                        value =>
                            Math.Round(
                                value,
                                9))
                    .Distinct()
                    .Take(21)
                    .Count();

            var looksDiscrete =
                LooksDiscrete(
                    tag.TagName,
                    distinctValues,
                    samples);

            var p90 =
                Percentile(
                    variations,
                    0.90);

            var p95 =
                Percentile(
                    variations,
                    0.95);

            var p99 =
                Percentile(
                    variations,
                    0.99);

            result[tag.TagName] =
                new DeadbandTagProfile
                {
                    TagName =
                        tag.TagName,

                    Unit =
                        tag.Unit,

                    SampleCount =
                        samples.Count,

                    VariationCount =
                        variations.Count,

                    DistinctValueCountCapped =
                        distinctValues,

                    Mean =
                        samples.Average(),

                    Minimum =
                        samples.Min(),

                    Maximum =
                        samples.Max(),

                    P90 =
                        p90,

                    P95 =
                        p95,

                    P99 =
                        p99,

                    Applicable =
                        !looksDiscrete &&
                        p95 > Epsilon,

                    LooksDiscrete =
                        looksDiscrete,

                    Reason =
                        looksDiscrete
                            ? "Tag com comportamento discreto/estado."
                            : p95 <= Epsilon
                                ? "Série sem variação suficiente."
                                : "Aplicável para simulação."
                };
        }

        return result;
    }

    private static bool LooksDiscrete(
        string tagName,
        int distinctValues,
        IReadOnlyCollection<double> samples)
    {
        var nameSuggestsDiscrete =
            tagName.Contains(
                "STATE",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.Contains(
                "MODE",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.Contains(
                "ONLINE",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.EndsWith(
                ".CODE",
                StringComparison.OrdinalIgnoreCase) ||
            tagName.Contains(
                "STATUS",
                StringComparison.OrdinalIgnoreCase);

        if (nameSuggestsDiscrete)
        {
            return true;
        }

        if (distinctValues <= 5)
        {
            return true;
        }

        var binaryRate =
            samples.Count(
                value =>
                    Math.Abs(value) <= Epsilon ||
                    Math.Abs(value - 1.0) <= Epsilon) /
            (double)samples.Count;

        return binaryRate >= 0.98;
    }

    private static double Percentile(
        IReadOnlyCollection<double> values,
        double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted =
            values
                .Where(double.IsFinite)
                .OrderBy(value => value)
                .ToArray();

        if (sorted.Length == 0)
        {
            return 0;
        }

        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        var position =
            Math.Clamp(
                percentile,
                0,
                1) *
            (sorted.Length - 1);

        var lower =
            (int)Math.Floor(position);

        var upper =
            (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return sorted[lower];
        }

        var fraction =
            position - lower;

        return sorted[lower] +
               ((sorted[upper] -
                 sorted[lower]) *
                fraction);
    }

    private static List<DeadbandCleanEventImpact>
        ReadCleanEvents(
            string csvPath,
            IReadOnlyDictionary<string, DeadbandTagProfile> profiles,
            CancellationToken cancellationToken)
    {
        var result =
            new List<DeadbandCleanEventImpact>();

        using var reader =
            new StreamReader(csvPath);

        var header =
            reader.ReadLine();

        if (header is null)
        {
            return result;
        }

        var columns =
            ParseCsvLine(header);

        var indexes =
            columns
                .Select(
                    (name, index) =>
                        new
                        {
                            Name =
                                name.Trim(),
                            Index =
                                index
                        })
                .ToDictionary(
                    item => item.Name,
                    item => item.Index,
                    StringComparer.OrdinalIgnoreCase);

        RequireColumn(
            indexes,
            "tag");

        RequireColumn(
            indexes,
            "inicioMinuto");

        RequireColumn(
            indexes,
            "fimMinuto");

        RequireColumn(
            indexes,
            "detectores");

        RequireColumn(
            indexes,
            "mudancaAbsoluta");

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields =
                ParseCsvLine(line);

            var tag =
                GetField(
                    fields,
                    indexes["tag"]);

            var detectors =
                GetField(
                    fields,
                    indexes["detectores"])
                    .Split(
                        '|',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var absoluteChange =
                Math.Abs(
                    ParseDouble(
                        GetField(
                            fields,
                            indexes["mudancaAbsoluta"])));

            profiles.TryGetValue(
                tag,
                out var profile);

            result.Add(
                new DeadbandCleanEventImpact
                {
                    TagName =
                        tag,

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

                    Detectors =
                        detectors,

                    AbsoluteChange =
                        absoluteChange,

                    P90 =
                        profile?.P90 ?? 0,

                    P95 =
                        profile?.P95 ?? 0,

                    P99 =
                        profile?.P99 ?? 0,

                    Applicable =
                        profile?.Applicable == true,

                    BelowP90 =
                        profile?.Applicable == true &&
                        absoluteChange <
                        profile.P90,

                    BelowP95 =
                        profile?.Applicable == true &&
                        absoluteChange <
                        profile.P95,

                    BelowP99 =
                        profile?.Applicable == true &&
                        absoluteChange <
                        profile.P99,

                    NonApplicableReason =
                        profile?.Reason ??
                        "Tag não encontrada no banco limpo."
                });
        }

        return result;
    }

    private static List<DeadbandOracleRisk>
        ReadOracleRisks(
            string evaluationJsonPath,
            string anomalyDatabasePath,
            IReadOnlyDictionary<string, DeadbandTagProfile> profiles,
            CancellationToken cancellationToken)
    {
        using var document =
            JsonDocument.Parse(
                File.ReadAllText(
                    evaluationJsonPath));

        if (!document.RootElement.TryGetProperty(
                "conditions",
                out var conditionsElement) ||
            conditionsElement.ValueKind !=
                JsonValueKind.Array)
        {
            return [];
        }

        var risks =
            new List<DeadbandOracleRisk>();

        using var connection =
            new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource =
                        anomalyDatabasePath,
                    Mode =
                        SqliteOpenMode.ReadOnly
                }
                .ToString());

        connection.Open();

        foreach (var condition in
                 conditionsElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var oracleId =
                GetJsonString(
                    condition,
                    "oracleId");

            var title =
                GetJsonString(
                    condition,
                    "title");

            var status =
                GetJsonString(
                    condition,
                    "status");

            var firstTag =
                GetJsonString(
                    condition,
                    "firstDetectionTag");

            var firstMinute =
                GetJsonNullableInt(
                    condition,
                    "firstEventStartMinuteIndex");

            if (!string.Equals(
                    status,
                    "Detectada",
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(firstTag) ||
                !firstMinute.HasValue)
            {
                risks.Add(
                    new DeadbandOracleRisk
                    {
                        OracleId =
                            oracleId,

                        Title =
                            title,

                        Status =
                            status,

                        FirstDetectionTag =
                            firstTag,

                        FirstEventStartMinute =
                            firstMinute,

                        ScreeningConclusion =
                            "Não foi possível simular o primeiro evento detectado."
                    });

                continue;
            }

            profiles.TryGetValue(
                firstTag,
                out var profile);

            var delta =
                ReadOnsetDelta(
                    connection,
                    firstTag,
                    firstMinute.Value);

            var applicable =
                profile?.Applicable == true &&
                delta.HasValue;

            risks.Add(
                new DeadbandOracleRisk
                {
                    OracleId =
                        oracleId,

                    Title =
                        title,

                    Status =
                        status,

                    FirstDetectionTag =
                        firstTag,

                    FirstEventStartMinute =
                        firstMinute,

                    OnsetAbsoluteDelta =
                        delta,

                    P90 =
                        profile?.P90 ?? 0,

                    P95 =
                        profile?.P95 ?? 0,

                    P99 =
                        profile?.P99 ?? 0,

                    Applicable =
                        applicable,

                    FirstOnsetBelowP90 =
                        applicable &&
                        delta!.Value <
                        profile!.P90,

                    FirstOnsetBelowP95 =
                        applicable &&
                        delta!.Value <
                        profile!.P95,

                    FirstOnsetBelowP99 =
                        applicable &&
                        delta!.Value <
                        profile!.P99,

                    ScreeningConclusion =
                        !applicable
                            ? profile?.Reason ??
                              "Sem perfil ou delta de início."
                            : delta!.Value < profile!.P95
                                ? "ATENÇÃO: o primeiro início detectado ficou abaixo do P95."
                                : "O primeiro início detectado ficou acima do P95."
                });
        }

        return risks;
    }

    private static double? ReadOnsetDelta(
        SqliteConnection connection,
        string tagName,
        int minuteIndex)
    {
        using var command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT
                atual.Value,
                anterior.Value
            FROM MinuteSamples AS atual
            INNER JOIN Tags AS t
                ON t.TagId = atual.TagId
            LEFT JOIN MinuteSamples AS anterior
                ON anterior.TagId = atual.TagId
                AND anterior.MinuteIndex = atual.MinuteIndex - 1
            WHERE
                t.TagName = $tagName
                AND atual.MinuteIndex = $minuteIndex
                AND atual.Value IS NOT NULL
            LIMIT 1;
            """;

        command.Parameters.AddWithValue(
            "$tagName",
            tagName);

        command.Parameters.AddWithValue(
            "$minuteIndex",
            minuteIndex);

        using var reader =
            command.ExecuteReader();

        if (!reader.Read() ||
            reader.IsDBNull(0) ||
            reader.IsDBNull(1))
        {
            return null;
        }

        return Math.Abs(
            reader.GetDouble(0) -
            reader.GetDouble(1));
    }

    private static DeadbandImpactSummary BuildSummary(
        IReadOnlyDictionary<string, DeadbandTagProfile> profiles,
        IReadOnlyCollection<DeadbandCleanEventImpact> cleanEvents,
        IReadOnlyCollection<DeadbandOracleRisk> oracleRisks)
    {
        var applicableEvents =
            cleanEvents
                .Where(item => item.Applicable)
                .ToList();

        var detectorSummaries =
            cleanEvents
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
                        new DeadbandDetectorImpact
                        {
                            Detector =
                                group.Key,

                            TotalEvents =
                                group.LongCount(),

                            ApplicableEvents =
                                group.LongCount(
                                    item =>
                                        item.Event.Applicable),

                            BelowP90 =
                                group.LongCount(
                                    item =>
                                        item.Event.BelowP90),

                            BelowP95 =
                                group.LongCount(
                                    item =>
                                        item.Event.BelowP95),

                            BelowP99 =
                                group.LongCount(
                                    item =>
                                        item.Event.BelowP99)
                        })
                .OrderByDescending(
                    item => item.TotalEvents)
                .ToList();

        return new DeadbandImpactSummary
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            TotalTags =
                profiles.Count,

            ApplicableTags =
                profiles.Values.LongCount(
                    item => item.Applicable),

            DiscreteOrNonApplicableTags =
                profiles.Values.LongCount(
                    item => !item.Applicable),

            TotalCleanEvents =
                cleanEvents.Count,

            ApplicableCleanEvents =
                applicableEvents.Count,

            CleanEventsBelowP90 =
                applicableEvents.LongCount(
                    item => item.BelowP90),

            CleanEventsBelowP95 =
                applicableEvents.LongCount(
                    item => item.BelowP95),

            CleanEventsBelowP99 =
                applicableEvents.LongCount(
                    item => item.BelowP99),

            OracleConditions =
                oracleRisks.Count,

            OracleDetectedConditions =
                oracleRisks.LongCount(
                    item =>
                        string.Equals(
                            item.Status,
                            "Detectada",
                            StringComparison.OrdinalIgnoreCase)),

            OracleFirstOnsetBelowP90 =
                oracleRisks.LongCount(
                    item => item.FirstOnsetBelowP90),

            OracleFirstOnsetBelowP95 =
                oracleRisks.LongCount(
                    item => item.FirstOnsetBelowP95),

            OracleFirstOnsetBelowP99 =
                oracleRisks.LongCount(
                    item => item.FirstOnsetBelowP99),

            Tags =
                profiles.Values
                    .OrderBy(
                        item => item.TagName)
                    .ToList(),

            Detectors =
                detectorSummaries,

            OracleRisks =
                oracleRisks
                    .OrderBy(
                        item => item.OracleId)
                    .ToList()
        };
    }

    private static string CreateEventsCsv(
        IEnumerable<DeadbandCleanEventImpact> events)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "tag,inicioMinuto,fimMinuto,detectores,mudancaAbsoluta," +
            "p90,p95,p99,aplicavel,abaixoP90,abaixoP95,abaixoP99,motivo");

        foreach (var item in events)
        {
            builder.Append(
                    Csv(item.TagName))
                .Append(',')
                .Append(item.StartMinute)
                .Append(',')
                .Append(item.EndMinute)
                .Append(',')
                .Append(
                    Csv(
                        string.Join(
                            " | ",
                            item.Detectors)))
                .Append(',')
                .Append(
                    Number(item.AbsoluteChange))
                .Append(',')
                .Append(
                    Number(item.P90))
                .Append(',')
                .Append(
                    Number(item.P95))
                .Append(',')
                .Append(
                    Number(item.P99))
                .Append(',')
                .Append(item.Applicable)
                .Append(',')
                .Append(item.BelowP90)
                .Append(',')
                .Append(item.BelowP95)
                .Append(',')
                .Append(item.BelowP99)
                .Append(',')
                .AppendLine(
                    Csv(item.NonApplicableReason));
        }

        return builder.ToString();
    }

    private static string CreateDetectorsCsv(
        IEnumerable<DeadbandDetectorImpact> detectors)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "detector,eventosTotais,eventosAplicaveis," +
            "abaixoP90,percentualP90,abaixoP95,percentualP95," +
            "abaixoP99,percentualP99");

        foreach (var item in detectors)
        {
            builder.Append(
                    Csv(item.Detector))
                .Append(',')
                .Append(item.TotalEvents)
                .Append(',')
                .Append(item.ApplicableEvents)
                .Append(',')
                .Append(item.BelowP90)
                .Append(',')
                .Append(
                    Number(item.P90Rate))
                .Append(',')
                .Append(item.BelowP95)
                .Append(',')
                .Append(
                    Number(item.P95Rate))
                .Append(',')
                .Append(item.BelowP99)
                .Append(',')
                .AppendLine(
                    Number(item.P99Rate));
        }

        return builder.ToString();
    }

    private static string CreateOracleCsv(
        IEnumerable<DeadbandOracleRisk> risks)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "oracleId,titulo,status,primeiraTag,primeiroMinuto," +
            "deltaInicio,p90,p95,p99,aplicavel," +
            "inicioAbaixoP90,inicioAbaixoP95,inicioAbaixoP99,conclusao");

        foreach (var item in risks)
        {
            builder.Append(
                    Csv(item.OracleId))
                .Append(',')
                .Append(
                    Csv(item.Title))
                .Append(',')
                .Append(
                    Csv(item.Status))
                .Append(',')
                .Append(
                    Csv(item.FirstDetectionTag))
                .Append(',')
                .Append(
                    item.FirstEventStartMinute?.ToString(
                        CultureInfo.InvariantCulture) ?? "")
                .Append(',')
                .Append(
                    item.OnsetAbsoluteDelta.HasValue
                        ? Number(
                            item.OnsetAbsoluteDelta.Value)
                        : "")
                .Append(',')
                .Append(
                    Number(item.P90))
                .Append(',')
                .Append(
                    Number(item.P95))
                .Append(',')
                .Append(
                    Number(item.P99))
                .Append(',')
                .Append(item.Applicable)
                .Append(',')
                .Append(item.FirstOnsetBelowP90)
                .Append(',')
                .Append(item.FirstOnsetBelowP95)
                .Append(',')
                .Append(item.FirstOnsetBelowP99)
                .Append(',')
                .AppendLine(
                    Csv(item.ScreeningConclusion));
        }

        return builder.ToString();
    }

    private static string CreateHtml(
        DeadbandImpactSummary summary)
    {
        var html =
            new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"pt-BR\"><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine("<title>Impacto do Deadband</title>");
        html.AppendLine("<style>");
        html.AppendLine(
            "body{font-family:Segoe UI,Arial,sans-serif;background:#f3f4f6;" +
            "margin:0;padding:24px;color:#111827}");
        html.AppendLine(
            ".container{max-width:1500px;margin:auto}.grid{display:grid;" +
            "grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px}");
        html.AppendLine(
            ".card,section{background:#fff;border-radius:12px;padding:18px;" +
            "margin:16px 0;box-shadow:0 1px 4px #0002}");
        html.AppendLine(
            ".value{font-size:28px;font-weight:800}.warning{border-left:5px solid #f59e0b;" +
            "background:#fffbeb;padding:14px;border-radius:8px}");
        html.AppendLine(
            "table{width:100%;border-collapse:collapse;font-size:13px}" +
            "th,td{text-align:left;padding:9px;border-bottom:1px solid #e5e7eb}" +
            "th{background:#f9fafb}");
        html.AppendLine("</style></head><body><div class=\"container\">");
        html.AppendLine("<h1>Simulação de impacto do deadband</h1>");
        html.AppendLine("<div class=\"grid\">");

        AppendCard(
            html,
            "Eventos limpos",
            summary.TotalCleanEvents.ToString("N0"));

        AppendCard(
            html,
            "Abaixo do P90",
            summary.CleanEventsBelowP90.ToString("N0"));

        AppendCard(
            html,
            "Abaixo do P95",
            summary.CleanEventsBelowP95.ToString("N0"));

        AppendCard(
            html,
            "Abaixo do P99",
            summary.CleanEventsBelowP99.ToString("N0"));

        AppendCard(
            html,
            "Oracle: início abaixo do P95",
            summary.OracleFirstOnsetBelowP95.ToString("N0"));

        html.AppendLine("</div>");
        html.AppendLine(
            "<div class=\"warning\"><strong>Limitação:</strong> " +
            "a parte Oracle avalia somente a variação no início do primeiro evento " +
            "detectado. O resultado indica risco para investigação, não prova perda de recall.</div>");

        html.AppendLine(
            "<section><h2>Impacto potencial por detector</h2>" +
            "<table><thead><tr><th>Detector</th><th>Eventos</th>" +
            "<th>Aplicáveis</th><th>Abaixo P90</th><th>Abaixo P95</th>" +
            "<th>Abaixo P99</th></tr></thead><tbody>");

        foreach (var item in summary.Detectors)
        {
            html.Append("<tr><td>")
                .Append(Encode(item.Detector))
                .Append("</td><td>")
                .Append(item.TotalEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.ApplicableEvents.ToString("N0"))
                .Append("</td><td>")
                .Append(item.P90Rate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.P95Rate.ToString("P1"))
                .Append("</td><td>")
                .Append(item.P99Rate.ToString("P1"))
                .AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
        html.AppendLine(
            "<section><h2>Triagem de risco no Oracle</h2>" +
            "<table><thead><tr><th>Condição</th><th>Status</th>" +
            "<th>Primeira tag</th><th>Delta inicial</th><th>P95</th>" +
            "<th>Abaixo P95?</th><th>Conclusão</th></tr></thead><tbody>");

        foreach (var item in summary.OracleRisks)
        {
            html.Append("<tr><td>")
                .Append(Encode(item.OracleId))
                .Append("</td><td>")
                .Append(Encode(item.Status))
                .Append("</td><td>")
                .Append(Encode(item.FirstDetectionTag))
                .Append("</td><td>")
                .Append(
                    item.OnsetAbsoluteDelta?.ToString("G6") ?? "")
                .Append("</td><td>")
                .Append(item.P95.ToString("G6"))
                .Append("</td><td>")
                .Append(item.FirstOnsetBelowP95 ? "Sim" : "Não")
                .Append("</td><td>")
                .Append(Encode(item.ScreeningConclusion))
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
        html.Append("<div class=\"card\"><div>")
            .Append(Encode(title))
            .Append("</div><div class=\"value\">")
            .Append(Encode(value))
            .AppendLine("</div></div>");
    }

    private static void PrintSummary(
        DeadbandImpactSummary summary,
        string jsonPath,
        string eventsCsvPath,
        string detectorsCsvPath,
        string oracleCsvPath,
        string htmlPath)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(" SIMULAÇÃO FINALIZADA");
        Console.WriteLine("========================================");
        Console.WriteLine(
            $"Tags: {summary.TotalTags:N0}");
        Console.WriteLine(
            $"Tags aplicáveis: {summary.ApplicableTags:N0}");
        Console.WriteLine(
            $"Eventos limpos: {summary.TotalCleanEvents:N0}");
        Console.WriteLine(
            $"Eventos aplicáveis: {summary.ApplicableCleanEvents:N0}");
        Console.WriteLine(
            $"Abaixo do P90: {summary.CleanEventsBelowP90:N0}");
        Console.WriteLine(
            $"Abaixo do P95: {summary.CleanEventsBelowP95:N0}");
        Console.WriteLine(
            $"Abaixo do P99: {summary.CleanEventsBelowP99:N0}");
        Console.WriteLine(
            $"Oracle - primeiro início abaixo do P95: " +
            $"{summary.OracleFirstOnsetBelowP95:N0}");
        Console.WriteLine();
        Console.WriteLine($"JSON: {jsonPath}");
        Console.WriteLine($"Eventos: {eventsCsvPath}");
        Console.WriteLine($"Detectores: {detectorsCsvPath}");
        Console.WriteLine($"Oracle: {oracleCsvPath}");
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

        var quoted =
            false;

        for (var index = 0;
             index < line.Length;
             index++)
        {
            var character =
                line[index];

            if (character == '"')
            {
                if (quoted &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted =
                        !quoted;
                }

                continue;
            }

            if (character == ',' &&
                !quoted)
            {
                fields.Add(
                    current.ToString());

                current.Clear();

                continue;
            }

            current.Append(character);
        }

        fields.Add(
            current.ToString());

        return fields;
    }

    private static void RequireColumn(
        IReadOnlyDictionary<string, int> indexes,
        string name)
    {
        if (!indexes.ContainsKey(name))
        {
            throw new InvalidDataException(
                $"A coluna obrigatória '{name}' não existe no CSV.");
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

    private static string GetJsonString(
        JsonElement element,
        string name)
    {
        return element.TryGetProperty(
                   name,
                   out var property) &&
               property.ValueKind ==
                   JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

private static int? GetJsonNullableInt(
    JsonElement element,
    string name)
{
    if (!element.TryGetProperty(
            name,
            out var property))
    {
        return null;
    }

    if (property.ValueKind ==
        JsonValueKind.Null)
    {
        return null;
    }

    if (property.ValueKind !=
        JsonValueKind.Number)
    {
        return null;
    }

    return property.TryGetInt32(
        out var value)
            ? value
            : null;
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
        return System.Net.WebUtility
            .HtmlEncode(value);
    }
}

public sealed class DeadbandTagProfile
{
    public string TagName { get; set; } = "";
    public string Unit { get; set; } = "";
    public int SampleCount { get; set; }
    public int VariationCount { get; set; }
    public int DistinctValueCountCapped { get; set; }
    public double Mean { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double P90 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public bool Applicable { get; set; }
    public bool LooksDiscrete { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class DeadbandCleanEventImpact
{
    public string TagName { get; set; } = "";
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public List<string> Detectors { get; set; } = [];
    public double AbsoluteChange { get; set; }
    public double P90 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public bool Applicable { get; set; }
    public bool BelowP90 { get; set; }
    public bool BelowP95 { get; set; }
    public bool BelowP99 { get; set; }
    public string NonApplicableReason { get; set; } = "";
}

public sealed class DeadbandDetectorImpact
{
    public string Detector { get; set; } = "";
    public long TotalEvents { get; set; }
    public long ApplicableEvents { get; set; }
    public long BelowP90 { get; set; }
    public long BelowP95 { get; set; }
    public long BelowP99 { get; set; }

    public double P90Rate =>
        ApplicableEvents > 0
            ? BelowP90 /
              (double)ApplicableEvents
            : 0;

    public double P95Rate =>
        ApplicableEvents > 0
            ? BelowP95 /
              (double)ApplicableEvents
            : 0;

    public double P99Rate =>
        ApplicableEvents > 0
            ? BelowP99 /
              (double)ApplicableEvents
            : 0;
}

public sealed class DeadbandOracleRisk
{
    public string OracleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string FirstDetectionTag { get; set; } = "";
    public int? FirstEventStartMinute { get; set; }
    public double? OnsetAbsoluteDelta { get; set; }
    public double P90 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public bool Applicable { get; set; }
    public bool FirstOnsetBelowP90 { get; set; }
    public bool FirstOnsetBelowP95 { get; set; }
    public bool FirstOnsetBelowP99 { get; set; }
    public string ScreeningConclusion { get; set; } = "";
}

public sealed class DeadbandImpactSummary
{
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalTags { get; set; }
    public long ApplicableTags { get; set; }
    public long DiscreteOrNonApplicableTags { get; set; }
    public int TotalCleanEvents { get; set; }
    public int ApplicableCleanEvents { get; set; }
    public long CleanEventsBelowP90 { get; set; }
    public long CleanEventsBelowP95 { get; set; }
    public long CleanEventsBelowP99 { get; set; }
    public int OracleConditions { get; set; }
    public long OracleDetectedConditions { get; set; }
    public long OracleFirstOnsetBelowP90 { get; set; }
    public long OracleFirstOnsetBelowP95 { get; set; }
    public long OracleFirstOnsetBelowP99 { get; set; }
    public List<DeadbandTagProfile> Tags { get; set; } = [];
    public List<DeadbandDetectorImpact> Detectors { get; set; } = [];
    public List<DeadbandOracleRisk> OracleRisks { get; set; } = [];
}