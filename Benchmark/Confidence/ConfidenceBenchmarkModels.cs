namespace IAApi.Benchmark;

public sealed class ConfidenceBenchmarkEvent
{
    public string TagName { get; init; } = "";
    public int StartMinute { get; init; }
    public int EndMinute { get; init; }
    public int DurationMinutes { get; init; }
    public string Category { get; init; } = "";
    public double StructuralClassificationConfidence { get; init; }

    public IReadOnlyList<string> Detectors { get; init; } =
        Array.Empty<string>();
}

public sealed class ConfidenceBenchmarkLevelSummary
{
    public string Level { get; init; } = "";
    public long EventCount { get; init; }
    public double EventRate { get; init; }
}

public sealed class ConfidenceBenchmarkCategorySummary
{
    public string Category { get; init; } = "";
    public long InputEvents { get; init; }
    public long SuspiciousEvents { get; init; }
    public long ProbableEvents { get; init; }
    public long ConfirmedEvents { get; init; }
    public double AverageScore { get; init; }
    public double AverageFamilyCount { get; init; }
}

public sealed class ConfidenceBenchmarkIsolatedDetectorSummary
{
    public string Detector { get; init; } = "";
    public long InputEvents { get; init; }
    public long SuspiciousEvents { get; init; }
    public long ProbableEvents { get; init; }
    public long ConfirmedEvents { get; init; }
    public string PrincipalCategory { get; init; } = "";
}

public sealed class ConfidenceBenchmarkFamilyCountSummary
{
    public int FamilyCount { get; init; }
    public long InputEvents { get; init; }
    public long SuspiciousEvents { get; init; }
    public long ProbableEvents { get; init; }
    public long ConfirmedEvents { get; init; }
    public double AverageScore { get; init; }
}

public sealed class ConfidenceBenchmarkReport
{
    public DateTime GeneratedAtUtc { get; init; }
    public string SourceFile { get; init; } = "";
    public string EvaluationMode { get; init; } =
        "TopologyProxyFromStructuralEvents";

    public int TotalEvents { get; init; }
    public int TagCount { get; init; }
    public int CategoryCount { get; init; }

    public long SuspiciousEvents { get; init; }
    public long ProbableEvents { get; init; }
    public long ConfirmedEvents { get; init; }

    public double SuspiciousRate { get; init; }
    public double ProbableRate { get; init; }
    public double ConfirmedRate { get; init; }

    public string ImportantWarning { get; init; } = "";

    public IReadOnlyList<ConfidenceBenchmarkLevelSummary> Levels { get; init; } =
        Array.Empty<ConfidenceBenchmarkLevelSummary>();

    public IReadOnlyList<ConfidenceBenchmarkCategorySummary> ByCategory
        { get; init; } =
        Array.Empty<ConfidenceBenchmarkCategorySummary>();

    public IReadOnlyList<ConfidenceBenchmarkIsolatedDetectorSummary>
        IsolatedDetectors { get; init; } =
        Array.Empty<ConfidenceBenchmarkIsolatedDetectorSummary>();

    public IReadOnlyList<ConfidenceBenchmarkFamilyCountSummary> ByFamilyCount
        { get; init; } =
        Array.Empty<ConfidenceBenchmarkFamilyCountSummary>();
}

public sealed class ConfidenceBenchmarkEvaluatedEvent
{
    public ConfidenceBenchmarkEvent Source { get; init; } =
        new();

    public string Level { get; init; } = "";
    public double Score { get; init; }
    public int PositiveFamilyCount { get; init; }
    public bool HasTemporalConfirmation { get; init; }
    public bool HasMultivariateEvidence { get; init; }
    public string Explanation { get; init; } = "";
}
