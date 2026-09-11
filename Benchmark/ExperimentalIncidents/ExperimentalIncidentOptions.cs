namespace IAApi.Benchmark.ExperimentalIncidents;

public enum ExperimentalIncidentMode { ObserveOnly }

public sealed record ExperimentalIncidentOptions
{
    public bool Enabled { get; init; }
    public ExperimentalIncidentMode Mode { get; init; } = ExperimentalIncidentMode.ObserveOnly;
    public bool FailOpen { get; init; } = true;
    public bool ReadModelEnabled { get; init; } = true;

    public static ExperimentalIncidentOptions Parse(string[] args)
    {
        var enabled = args.Any(x => x.Equals(
            "--experimental-incident-layer", StringComparison.OrdinalIgnoreCase));
        return new ExperimentalIncidentOptions { Enabled = enabled };
    }
}
