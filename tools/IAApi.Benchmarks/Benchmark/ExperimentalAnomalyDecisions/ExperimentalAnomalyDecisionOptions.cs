namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

public sealed record ExperimentalAnomalyDecisionOptions
{
    public bool Enabled { get; init; }

    public bool FailOpen { get; init; } = true;

    public bool ObserveOnly { get; init; } = true;

    public static ExperimentalAnomalyDecisionOptions Parse(string[] args)
    {
        var enabled = args.Any(x =>
            x.Equals(
                "--experimental-anomaly-decision",
                StringComparison.OrdinalIgnoreCase));

        return new ExperimentalAnomalyDecisionOptions
        {
            Enabled = enabled
        };
    }
}