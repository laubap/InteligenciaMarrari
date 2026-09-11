namespace IAApi.Benchmark.ExperimentalIncidents.Api;

public sealed record ExperimentalIncidentApiOptions
{
    public const string SectionName = "ExperimentalIncidentApi";
    public bool Enabled { get; init; }
    public int DefaultPageSize { get; init; } = 20;
    public int MaximumPageSize { get; init; } = 100;
    public string? SnapshotPath { get; init; }
}
