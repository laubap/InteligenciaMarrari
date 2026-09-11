using System.Text.Json.Serialization;

public sealed class OracleFile
{
    public string Name { get; set; } = "";
    public int FixtureLengthDays { get; set; } = 365;
    public List<OracleCondition> Conditions { get; set; } = [];
}

public sealed class OracleCondition
{
    public int Number { get; set; }
    public string OracleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public int? StartDayInclusive { get; set; }
    public int? EndDayExclusive { get; set; }
    public List<int> OccurrenceDays { get; set; } = [];
    public bool MissingDataCondition { get; set; }
    public bool Supplemental { get; set; }
    public string ExpectedInterpretation { get; set; } = "";

    [JsonIgnore]
    public HashSet<string> NormalizedTags =>
        Tags.Select(TagNormalizer.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsInsideExpectedPeriod(int fixtureDay, int fixtureLengthDays)
    {
        if (OccurrenceDays.Count > 0)
            return OccurrenceDays.Contains(fixtureDay);

        var start = StartDayInclusive ?? 0;
        var end = EndDayExclusive ?? fixtureLengthDays;
        return fixtureDay >= start && fixtureDay < end;
    }
}

public sealed class DetectionRecord
{
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";
    public long MinuteIndex { get; set; }
    public DateTimeOffset? DataHora { get; set; }
    public double Valor { get; set; }
    public bool EhAnomalia { get; set; }
    public double Score { get; set; }
    public string? Mensagem { get; set; }
    public List<DetectorRecord> Detectores { get; set; } = [];
}

public sealed class DetectorRecord
{
    public string Nome { get; set; } = "";
    public bool Aplicavel { get; set; }
    public bool VotouAnomalia { get; set; }
    public double ScoreNormalizado { get; set; }
    public double Peso { get; set; }
    public string? Explicacao { get; set; }
}

public sealed class ErrorRecord
{
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";
    public long MinuteIndex { get; set; }
    public DateTimeOffset? DataHora { get; set; }
    public double Valor { get; set; }
    public string TipoErro { get; set; } = "";
    public string Mensagem { get; set; } = "";
}

public sealed class ConditionEvaluation
{
    public int Number { get; set; }
    public string OracleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string Status { get; set; } = "";
    public bool MissingDataCondition { get; set; }
    public bool Supplemental { get; set; }
    public long MatchingDetectionCount { get; set; }
    public long MatchingErrorCount { get; set; }
    public int? FirstDetectionDay { get; set; }
    public long? FirstDetectionMinuteIndex { get; set; }
    public string? FirstDetectionTag { get; set; }
    public double? HighestScore { get; set; }
    public string? HighestScoreTag { get; set; }
    public List<string> ErrorTypes { get; set; } = [];
    public string Observation { get; set; } = "";
}

public sealed class BenchmarkEvaluationResult
{
    public DateTime GeneratedAtUtc { get; set; }
    public string DetectionsFile { get; set; } = "";
    public string ErrorsFile { get; set; } = "";
    public string OracleFile { get; set; } = "";
    public int TotalConditions { get; set; }
    public int DetectedConditions { get; set; }
    public int ConditionsWithErrors { get; set; }
    public int NotDetectedConditions { get; set; }
    public int SupplementalConditions { get; set; }
    public long TotalDetectionRecords { get; set; }
    public long MatchedDetectionRecords { get; set; }
    public long PossibleFalsePositiveRecords { get; set; }
    public long InvalidDetectionLines { get; set; }
    public long TotalErrorRecords { get; set; }
    public long ErrorsInsideOracleConditions { get; set; }
    public long InvalidErrorLines { get; set; }
    public double ConditionRecall { get; set; }
    public double ApproximateRecordPrecision { get; set; }
    public List<ConditionEvaluation> Conditions { get; set; } = [];
    public Dictionary<string, long> PossibleFalsePositivesByTag { get; set; } = [];
    public string ImportantLimitations { get; set; } = "";
    public string JsonReportPath { get; set; } = "";
    public string HtmlReportPath { get; set; } = "";
    public string CsvReportPath { get; set; } = "";
}

public static class TagNormalizer
{
    public static string Normalize(string? tag) =>
        (tag ?? string.Empty).Trim().ToUpperInvariant();
}
