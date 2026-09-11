using System.Net;
using System.Text;
using System.Text.Json;

public sealed class BenchmarkEvaluator
{
    private const int DefaultToleranceMinutes = 60;
    private const int DefaultEventGapMinutes = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<BenchmarkEvaluationV2Result> EvaluateAsync(
        string detectionsPath,
        string errorsPath,
        string oraclePath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var oracle = await LoadOracleAsync(oraclePath, cancellationToken);
        ValidateOracle(oracle);

        var options = new BenchmarkEvaluationV2Options
        {
            ToleranceMinutes = DefaultToleranceMinutes,
            EventGapMinutes = DefaultEventGapMinutes
        };

        var windows = oracle.Conditions.ToDictionary(
            c => c.OracleId,
            c => ExpectedWindow.Create(c, oracle.FixtureLengthDays, options.ToleranceMinutes),
            StringComparer.OrdinalIgnoreCase);

        var states = oracle.Conditions.ToDictionary(
            c => c.OracleId,
            c => new ConditionState(c, windows[c.OracleId]),
            StringComparer.OrdinalIgnoreCase);

        var conditionsByTag = BuildTagIndex(oracle.Conditions);
        var openEvents = new Dictionary<string, DetectionEventBuilder>(
            StringComparer.OrdinalIgnoreCase);
        var unmatchedEventsByTag = new Dictionary<string, long>(
            StringComparer.OrdinalIgnoreCase);

        long totalDetectionRecords = 0;
        long invalidDetectionLines = 0;
        long totalDetectionEvents = 0;
        long matchedDetectionEvents = 0;
        long unmatchedDetectionEvents = 0;

        void FinalizeEvent(DetectionEventBuilder builder)
        {
            var detectionEvent = builder.Build();
            totalDetectionEvents++;

            var matchedAny = false;
            if (conditionsByTag.TryGetValue(detectionEvent.NormalizedTag, out var candidates))
            {
                foreach (var condition in candidates)
                {
                    var window = windows[condition.OracleId];
                    if (!window.OverlapsWithTolerance(
                            detectionEvent.StartMinuteIndex,
                            detectionEvent.EndMinuteIndex))
                    {
                        continue;
                    }

                    states[condition.OracleId].RegisterEvent(detectionEvent);
                    matchedAny = true;
                }
            }

            if (matchedAny)
            {
                matchedDetectionEvents++;
            }
            else
            {
                unmatchedDetectionEvents++;
                unmatchedEventsByTag.TryGetValue(
                    detectionEvent.NormalizedTag,
                    out var count);
                unmatchedEventsByTag[detectionEvent.NormalizedTag] = count + 1;
            }
        }

        await foreach (var line in ReadLinesAsync(detectionsPath, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            DetectionRecord? record;
            try
            {
                record = JsonSerializer.Deserialize<DetectionRecord>(line, JsonOptions);
            }
            catch (JsonException)
            {
                invalidDetectionLines++;
                continue;
            }

            if (record is null ||
                string.IsNullOrWhiteSpace(record.TagName) ||
                record.MinuteIndex < 0)
            {
                invalidDetectionLines++;
                continue;
            }

            totalDetectionRecords++;
            var normalizedTag = TagNormalizer.Normalize(record.TagName);

            if (!openEvents.TryGetValue(normalizedTag, out var current))
            {
                openEvents[normalizedTag] = new DetectionEventBuilder(record, normalizedTag);
            }
            else if (record.MinuteIndex - current.EndMinuteIndex <= options.EventGapMinutes)
            {
                current.Add(record);
            }
            else
            {
                FinalizeEvent(current);
                openEvents[normalizedTag] = new DetectionEventBuilder(record, normalizedTag);
            }

            if (totalDetectionRecords % 250_000 == 0)
            {
                Console.WriteLine(
                    $"Registros processados: {totalDetectionRecords:N0} | " +
                    $"Eventos fechados: {totalDetectionEvents:N0}");
            }
        }

        foreach (var builder in openEvents.Values)
            FinalizeEvent(builder);

        long totalErrors = 0;
        long invalidErrorLines = 0;
        long errorsInsideConditions = 0;

        await foreach (var line in ReadLinesAsync(errorsPath, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            ErrorRecord? record;
            try
            {
                record = JsonSerializer.Deserialize<ErrorRecord>(line, JsonOptions);
            }
            catch (JsonException)
            {
                invalidErrorLines++;
                continue;
            }

            if (record is null ||
                string.IsNullOrWhiteSpace(record.TagName) ||
                record.MinuteIndex < 0)
            {
                invalidErrorLines++;
                continue;
            }

            totalErrors++;
            var normalizedTag = TagNormalizer.Normalize(record.TagName);
            var matchedAny = false;

            if (conditionsByTag.TryGetValue(normalizedTag, out var candidates))
            {
                foreach (var condition in candidates)
                {
                    var window = windows[condition.OracleId];
                    if (!window.ContainsWithTolerance(record.MinuteIndex))
                        continue;

                    states[condition.OracleId].RegisterError(record);
                    matchedAny = true;
                }
            }

            if (matchedAny)
                errorsInsideConditions++;
        }

        var evaluations = oracle.Conditions
            .OrderBy(c => c.Number)
            .Select(c => states[c.OracleId].ToEvaluation())
            .ToList();

        var required = evaluations.Where(c => !c.Supplemental).ToList();
        var detected = required.Count(c => c.Status == "Detectada");
        var withErrors = required.Count(c => c.Status == "Erro");
        var notDetected = required.Count(c => c.Status == "Não detectada");

        var detectedWithLatency = required
            .Where(c => c.Status == "Detectada" && c.DetectionLatencyMinutes.HasValue)
            .ToList();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, $"benchmark_evaluation_v2_{timestamp}.json");
        var htmlPath = Path.Combine(outputDirectory, $"benchmark_evaluation_v2_{timestamp}.html");
        var csvPath = Path.Combine(outputDirectory, $"benchmark_conditions_v2_{timestamp}.csv");

        var result = new BenchmarkEvaluationV2Result
        {
            GeneratedAtUtc = DateTime.UtcNow,
            DetectionsFile = detectionsPath,
            ErrorsFile = errorsPath,
            OracleFile = oraclePath,
            Options = options,
            TotalConditions = oracle.Conditions.Count,
            RequiredConditions = required.Count,
            SupplementalConditions = evaluations.Count(c => c.Supplemental),
            DetectedConditions = detected,
            ConditionsWithErrors = withErrors,
            NotDetectedConditions = notDetected,
            ConditionRecall = required.Count == 0 ? 0 : (double)detected / required.Count,
            TotalDetectionRecords = totalDetectionRecords,
            InvalidDetectionLines = invalidDetectionLines,
            TotalDetectionEvents = totalDetectionEvents,
            MatchedDetectionEvents = matchedDetectionEvents,
            UnmatchedDetectionEvents = unmatchedDetectionEvents,
            ApproximateEventPrecision = totalDetectionEvents == 0
                ? 0
                : (double)matchedDetectionEvents / totalDetectionEvents,
            AverageDetectionLatencyMinutes = detectedWithLatency.Count == 0
                ? null
                : detectedWithLatency.Average(c => c.DetectionLatencyMinutes!.Value),
            MedianDetectionLatencyMinutes = Median(
                detectedWithLatency.Select(c => c.DetectionLatencyMinutes!.Value)),
            TotalErrorRecords = totalErrors,
            InvalidErrorLines = invalidErrorLines,
            ErrorsInsideOracleConditions = errorsInsideConditions,
            Conditions = evaluations,
            UnmatchedEventsByTag = unmatchedEventsByTag
                .OrderByDescending(x => x.Value)
                .Take(100)
                .ToDictionary(x => x.Key, x => x.Value),
            ImportantLimitations =
                "A avaliação V2 é orientada a eventos. Detecções consecutivas da mesma tag são agrupadas e cada condição do oráculo conta no máximo uma vez para o recall. " +
                "A tolerância temporal é aplicada antes e depois da janela esperada. Eventos sem correspondência são chamados de eventos não associados, e não de falsos positivos definitivos, " +
                "pois o arquivo de benchmark não contém um rótulo normal/anômalo para cada leitura. Condições de dados ausentes continuam inconclusivas quando nenhuma linha é produzida.",
            JsonReportPath = jsonPath,
            HtmlReportPath = htmlPath,
            CsvReportPath = csvPath
        };

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(result, JsonOptions),
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            HtmlReportBuilderV2.Build(result),
            cancellationToken);

        await File.WriteAllTextAsync(
            csvPath,
            BuildCsv(result),
            cancellationToken);

        return result;
    }

    private static double? Median(IEnumerable<long> values)
    {
        var ordered = values.OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
            return null;

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    private static async Task<OracleFile> LoadOracleAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var oracle = await JsonSerializer.DeserializeAsync<OracleFile>(
            stream,
            JsonOptions,
            cancellationToken);

        return oracle ?? throw new InvalidOperationException(
            "O arquivo oracle.json não pôde ser desserializado.");
    }

    private static void ValidateOracle(OracleFile oracle)
    {
        if (oracle.Conditions.Count == 0)
            throw new InvalidOperationException("O oráculo não possui condições.");

        if (oracle.FixtureLengthDays <= 0)
            throw new InvalidOperationException("FixtureLengthDays deve ser maior que zero.");

        var duplicateIds = oracle.Conditions
            .GroupBy(c => c.OracleId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
            throw new InvalidOperationException(
                $"Oracle IDs duplicados: {string.Join(", ", duplicateIds)}");

        foreach (var condition in oracle.Conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.OracleId))
                throw new InvalidOperationException(
                    $"A condição nº {condition.Number} não possui OracleId.");

            if (condition.Tags.Count == 0)
                throw new InvalidOperationException(
                    $"A condição {condition.OracleId} não possui tags.");
        }
    }

    private static Dictionary<string, List<OracleCondition>> BuildTagIndex(
        IEnumerable<OracleCondition> conditions)
    {
        var index = new Dictionary<string, List<OracleCondition>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var condition in conditions)
        {
            foreach (var tag in condition.NormalizedTags)
            {
                if (!index.TryGetValue(tag, out var list))
                {
                    list = [];
                    index[tag] = list;
                }

                list.Add(condition);
            }
        }

        return index;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan);

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024 * 1024);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is not null)
                yield return line;
        }
    }

    private static string BuildCsv(BenchmarkEvaluationV2Result result)
    {
        static string Esc(string? value) =>
            "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

        var sb = new StringBuilder();
        sb.AppendLine(
            "numero,oracleId,titulo,categoria,status,missingData,supplemental," +
            "expectedStartMinute,expectedEndMinute,toleranceMinutes,eventCount," +
            "firstEventStartMinute,firstEventEndMinute,latencyMinutes,firstTag," +
            "highestScore,highestScoreTag,errorCount,observation");

        foreach (var c in result.Conditions)
        {
            sb.AppendLine(string.Join(",",
                c.Number,
                Esc(c.OracleId),
                Esc(c.Title),
                Esc(c.Category),
                Esc(c.Status),
                c.MissingDataCondition,
                c.Supplemental,
                c.ExpectedStartMinuteIndex,
                c.ExpectedEndMinuteIndex,
                c.ToleranceMinutes,
                c.MatchingEventCount,
                c.FirstEventStartMinuteIndex?.ToString() ?? "",
                c.FirstEventEndMinuteIndex?.ToString() ?? "",
                c.DetectionLatencyMinutes?.ToString() ?? "",
                Esc(c.FirstDetectionTag),
                c.HighestScore?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ?? "",
                Esc(c.HighestScoreTag),
                c.MatchingErrorCount,
                Esc(c.Observation)));
        }

        return sb.ToString();
    }

    private sealed class DetectionEventBuilder
    {
        public DetectionEventBuilder(DetectionRecord record, string normalizedTag)
        {
            NormalizedTag = normalizedTag;
            OriginalTag = record.TagName;
            StartMinuteIndex = record.MinuteIndex;
            EndMinuteIndex = record.MinuteIndex;
            RecordCount = 1;
            HighestScore = record.Score;
        }

        public string NormalizedTag { get; }
        public string OriginalTag { get; }
        public long StartMinuteIndex { get; }
        public long EndMinuteIndex { get; private set; }
        public long RecordCount { get; private set; }
        public double HighestScore { get; private set; }

        public void Add(DetectionRecord record)
        {
            EndMinuteIndex = Math.Max(EndMinuteIndex, record.MinuteIndex);
            RecordCount++;
            HighestScore = Math.Max(HighestScore, record.Score);
        }

        public DetectionEvent Build() => new()
        {
            NormalizedTag = NormalizedTag,
            OriginalTag = OriginalTag,
            StartMinuteIndex = StartMinuteIndex,
            EndMinuteIndex = EndMinuteIndex,
            RecordCount = RecordCount,
            HighestScore = HighestScore
        };
    }

    private sealed class ConditionState
    {
        private readonly OracleCondition _condition;
        private readonly ExpectedWindow _window;
        private readonly HashSet<string> _errorTypes =
            new(StringComparer.OrdinalIgnoreCase);

        private long _events;
        private long _errors;
        private long? _firstStart;
        private long? _firstEnd;
        private string? _firstTag;
        private double? _highestScore;
        private string? _highestScoreTag;

        public ConditionState(OracleCondition condition, ExpectedWindow window)
        {
            _condition = condition;
            _window = window;
        }

        public void RegisterEvent(DetectionEvent detectionEvent)
        {
            _events++;

            if (_firstStart is null || detectionEvent.StartMinuteIndex < _firstStart)
            {
                _firstStart = detectionEvent.StartMinuteIndex;
                _firstEnd = detectionEvent.EndMinuteIndex;
                _firstTag = detectionEvent.OriginalTag;
            }

            if (_highestScore is null || detectionEvent.HighestScore > _highestScore)
            {
                _highestScore = detectionEvent.HighestScore;
                _highestScoreTag = detectionEvent.OriginalTag;
            }
        }

        public void RegisterError(ErrorRecord record)
        {
            _errors++;
            if (!string.IsNullOrWhiteSpace(record.TipoErro))
                _errorTypes.Add(record.TipoErro);
        }

        public ConditionEvaluationV2 ToEvaluation()
        {
            string status;
            string observation;

            if (_events > 0)
            {
                status = "Detectada";
                observation = _condition.MissingDataCondition
                    ? "Há evento em tag e janela relacionadas, mas isso não comprova sozinho que a ausência de dados foi identificada corretamente."
                    : "Ao menos um evento agrupado coincidiu com uma tag e com a janela temporal tolerada da condição.";
            }
            else if (_errors > 0)
            {
                status = "Erro";
                observation = "Não houve evento coincidente, mas ocorreram erros em tag e janela relacionadas à condição.";
            }
            else
            {
                status = "Não detectada";
                observation = _condition.MissingDataCondition
                    ? "Nenhum evento ou erro coincidiu. Para lacunas de dados, esse resultado permanece inconclusivo quando nenhuma linha é gravada."
                    : "Nenhum evento agrupado coincidiu com as tags e a janela temporal tolerada da condição.";
            }

            if (_condition.Supplemental)
                observation += " Condição suplementar: não entra no recall principal.";

            return new ConditionEvaluationV2
            {
                Number = _condition.Number,
                OracleId = _condition.OracleId,
                Title = _condition.Title,
                Category = _condition.Category,
                Tags = _condition.Tags,
                Status = status,
                MissingDataCondition = _condition.MissingDataCondition,
                Supplemental = _condition.Supplemental,
                ExpectedStartMinuteIndex = _window.StartMinuteIndex,
                ExpectedEndMinuteIndex = _window.EndMinuteIndex,
                ToleranceMinutes = _window.ToleranceMinutes,
                MatchingEventCount = _events,
                MatchingErrorCount = _errors,
                FirstEventStartMinuteIndex = _firstStart,
                FirstEventEndMinuteIndex = _firstEnd,
                DetectionLatencyMinutes = _firstStart.HasValue
                    ? _firstStart.Value - _window.StartMinuteIndex
                    : null,
                FirstDetectionTag = _firstTag,
                HighestScore = _highestScore,
                HighestScoreTag = _highestScoreTag,
                ErrorTypes = _errorTypes.OrderBy(x => x).ToList(),
                Observation = observation
            };
        }
    }

    private sealed class ExpectedWindow
    {
        public long StartMinuteIndex { get; init; }
        public long EndMinuteIndex { get; init; }
        public int ToleranceMinutes { get; init; }

        public bool OverlapsWithTolerance(long eventStart, long eventEnd) =>
            eventEnd >= StartMinuteIndex - ToleranceMinutes &&
            eventStart <= EndMinuteIndex + ToleranceMinutes;

        public bool ContainsWithTolerance(long minuteIndex) =>
            minuteIndex >= StartMinuteIndex - ToleranceMinutes &&
            minuteIndex <= EndMinuteIndex + ToleranceMinutes;

        public static ExpectedWindow Create(
            OracleCondition condition,
            int fixtureLengthDays,
            int toleranceMinutes)
        {
            var matchingDays = Enumerable.Range(0, fixtureLengthDays)
                .Where(day => condition.IsInsideExpectedPeriod(day, fixtureLengthDays))
                .ToArray();

            if (matchingDays.Length == 0)
            {
                throw new InvalidOperationException(
                    $"A condição {condition.OracleId} não possui período válido dentro do fixture.");
            }

            return new ExpectedWindow
            {
                StartMinuteIndex = matchingDays[0] * 1440L,
                EndMinuteIndex = ((matchingDays[^1] + 1L) * 1440L) - 1L,
                ToleranceMinutes = toleranceMinutes
            };
        }
    }
}

public sealed class BenchmarkEvaluationV2Options
{
    public int ToleranceMinutes { get; set; }
    public int EventGapMinutes { get; set; }
}

public sealed class BenchmarkEvaluationV2Result
{
    public DateTime GeneratedAtUtc { get; set; }
    public string DetectionsFile { get; set; } = "";
    public string ErrorsFile { get; set; } = "";
    public string OracleFile { get; set; } = "";
    public BenchmarkEvaluationV2Options Options { get; set; } = new();
    public int TotalConditions { get; set; }
    public int RequiredConditions { get; set; }
    public int SupplementalConditions { get; set; }
    public int DetectedConditions { get; set; }
    public int ConditionsWithErrors { get; set; }
    public int NotDetectedConditions { get; set; }
    public double ConditionRecall { get; set; }
    public long TotalDetectionRecords { get; set; }
    public long InvalidDetectionLines { get; set; }
    public long TotalDetectionEvents { get; set; }
    public long MatchedDetectionEvents { get; set; }
    public long UnmatchedDetectionEvents { get; set; }
    public double ApproximateEventPrecision { get; set; }
    public double? AverageDetectionLatencyMinutes { get; set; }
    public double? MedianDetectionLatencyMinutes { get; set; }
    public long TotalErrorRecords { get; set; }
    public long InvalidErrorLines { get; set; }
    public long ErrorsInsideOracleConditions { get; set; }
    public List<ConditionEvaluationV2> Conditions { get; set; } = [];
    public Dictionary<string, long> UnmatchedEventsByTag { get; set; } = new();
    public string ImportantLimitations { get; set; } = "";
    public string JsonReportPath { get; set; } = "";
    public string HtmlReportPath { get; set; } = "";
    public string CsvReportPath { get; set; } = "";
}

public sealed class ConditionEvaluationV2
{
    public int Number { get; set; }
    public string OracleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string Status { get; set; } = "";
    public bool MissingDataCondition { get; set; }
    public bool Supplemental { get; set; }
    public long ExpectedStartMinuteIndex { get; set; }
    public long ExpectedEndMinuteIndex { get; set; }
    public int ToleranceMinutes { get; set; }
    public long MatchingEventCount { get; set; }
    public long MatchingErrorCount { get; set; }
    public long? FirstEventStartMinuteIndex { get; set; }
    public long? FirstEventEndMinuteIndex { get; set; }
    public long? DetectionLatencyMinutes { get; set; }
    public string? FirstDetectionTag { get; set; }
    public double? HighestScore { get; set; }
    public string? HighestScoreTag { get; set; }
    public List<string> ErrorTypes { get; set; } = [];
    public string Observation { get; set; } = "";
}

public sealed class DetectionEvent
{
    public string NormalizedTag { get; set; } = "";
    public string OriginalTag { get; set; } = "";
    public long StartMinuteIndex { get; set; }
    public long EndMinuteIndex { get; set; }
    public long RecordCount { get; set; }
    public double HighestScore { get; set; }
}

public static class HtmlReportBuilderV2
{
    public static string Build(BenchmarkEvaluationV2Result r)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        static string StatusClass(string status) => status switch
        {
            "Detectada" => "detected",
            "Erro" => "error",
            _ => "missing"
        };
        static string Minutes(long? value) => value.HasValue ? $"{value.Value:N0} min" : "—";

        var rows = new StringBuilder();
        foreach (var c in r.Conditions)
        {
            rows.Append($"""
<tr>
  <td>{c.Number}</td>
  <td><code>{E(c.OracleId)}</code></td>
  <td>{E(c.Title)}</td>
  <td>{E(c.Category)}</td>
  <td><span class="status {StatusClass(c.Status)}">{E(c.Status)}</span></td>
  <td>{c.MatchingEventCount:N0}</td>
  <td>{c.ExpectedStartMinuteIndex:N0}</td>
  <td>{c.FirstEventStartMinuteIndex?.ToString("N0") ?? "—"}</td>
  <td>{Minutes(c.DetectionLatencyMinutes)}</td>
  <td>{c.HighestScore?.ToString("F2") ?? "—"}</td>
  <td>{E(c.Observation)}</td>
</tr>
""");
        }

        var unmatchedRows = new StringBuilder();
        foreach (var item in r.UnmatchedEventsByTag.Take(30))
        {
            unmatchedRows.Append(
                $"<tr><td><code>{E(item.Key)}</code></td><td>{item.Value:N0}</td></tr>");
        }

        return $$"""
<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Avaliação do Benchmark V2</title>
<style>
:root{font-family:Inter,Segoe UI,Arial,sans-serif;color:#111827;background:#f3f4f6}
body{margin:0;padding:24px}.container{max-width:1600px;margin:auto}
h1{margin:0 0 6px}.muted{color:#6b7280}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:14px;margin:22px 0}
.card{background:white;border-radius:12px;padding:18px;box-shadow:0 1px 4px #0002}.value{font-size:28px;font-weight:800;margin-top:6px}
section{background:white;border-radius:12px;padding:18px;margin:18px 0;box-shadow:0 1px 4px #0002;overflow:auto}
table{width:100%;border-collapse:collapse;font-size:13px}th,td{text-align:left;padding:10px;border-bottom:1px solid #e5e7eb;vertical-align:top}th{position:sticky;top:0;background:#f9fafb}
.status{display:inline-block;padding:4px 8px;border-radius:999px;font-weight:700}.detected{background:#dcfce7;color:#166534}.error{background:#fef3c7;color:#92400e}.missing{background:#fee2e2;color:#991b1b}
.warning{border-left:5px solid #f59e0b;background:#fffbeb;padding:14px;border-radius:8px}code{font-size:12px}
</style>
</head>
<body>
<div class="container">
<h1>Avaliação do Benchmark V2 — orientada a eventos</h1>
<div class="muted">Gerado em {{r.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}} UTC · tolerância {{r.Options.ToleranceMinutes}} min · intervalo máximo entre detecções do mesmo evento {{r.Options.EventGapMinutes}} min</div>

<div class="grid">
<div class="card"><div>Condições obrigatórias</div><div class="value">{{r.RequiredConditions}}</div></div>
<div class="card"><div>Detectadas</div><div class="value">{{r.DetectedConditions}}</div></div>
<div class="card"><div>Não detectadas</div><div class="value">{{r.NotDetectedConditions}}</div></div>
<div class="card"><div>Recall por condição</div><div class="value">{{r.ConditionRecall:P1}}</div></div>
<div class="card"><div>Eventos detectados</div><div class="value">{{r.TotalDetectionEvents:N0}}</div></div>
<div class="card"><div>Eventos associados</div><div class="value">{{r.MatchedDetectionEvents:N0}}</div></div>
<div class="card"><div>Precisão aproximada por evento</div><div class="value">{{r.ApproximateEventPrecision:P1}}</div></div>
<div class="card"><div>Latência mediana</div><div class="value">{{(r.MedianDetectionLatencyMinutes?.ToString("N0") ?? "—")}} min</div></div>
</div>

<div class="warning"><strong>Limitação:</strong> {{E(r.ImportantLimitations)}}</div>

<section>
<h2>Resultado por condição</h2>
<table>
<thead><tr><th>#</th><th>Oracle ID</th><th>Condição</th><th>Categoria</th><th>Status</th><th>Eventos</th><th>Início esperado</th><th>1º evento</th><th>Latência</th><th>Maior score</th><th>Observação</th></tr></thead>
<tbody>{{rows}}</tbody>
</table>
</section>

<section>
<h2>Eventos não associados por tag</h2>
<p class="muted">Não são falsos positivos confirmados; são eventos que não coincidiram com nenhuma condição do oráculo.</p>
<table><thead><tr><th>Tag</th><th>Eventos</th></tr></thead><tbody>{{unmatchedRows}}</tbody></table>
</section>

<section>
<h2>Arquivos usados</h2>
<p><strong>Detecções:</strong> {{E(r.DetectionsFile)}}</p>
<p><strong>Erros:</strong> {{E(r.ErrorsFile)}}</p>
<p><strong>Oráculo:</strong> {{E(r.OracleFile)}}</p>
</section>
</div>
</body>
</html>
""";
    }
}