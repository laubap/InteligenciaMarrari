namespace IAApi.Benchmark.PersistenceCounterfactual;

public sealed record PersistenceCounterfactualOptions
{
    public bool Enabled { get; init; }

    public static PersistenceCounterfactualOptions Parse(string[] args) => new()
    {
        Enabled = args.Any(x => x.Equals(
            "--persistence-counterfactual", StringComparison.OrdinalIgnoreCase))
    };
}
