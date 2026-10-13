namespace IAApi.Benchmark.BehaviorProfiles.Diagnostics;

public sealed record BehaviorProfileClosureEpisode
{
    public string Tag { get; init; } = "";
    public int FastWindow { get; init; }
    public int StartMinute { get; init; }
    public int EndMinute { get; init; }
    public int Duration { get; init; }
    public string DeclaredType { get; init; } = "";
    public string DeclaredStatus { get; init; } = "";
    public string DeclaredRawValue { get; init; } = "";
    public string DeclaredSource { get; init; } = "";
    public string RawProfile { get; init; } = "";
    public string StableProfile { get; init; } = "";
    public IReadOnlyList<string> ValidClasses { get; init; } = [];
    public string PreferredClass { get; init; } = "";
    public string ResolutionStatus { get; init; } = "";
    public string DeclaredRelation { get; init; } = "";
    public string Readiness { get; init; } = "";
    public string ObservedStructure { get; init; } = "";
    public string Reason { get; init; } = "";
    public object? StartEvidence { get; init; }
    public object? EndEvidence { get; init; }
}
