namespace IAApi.Benchmark.BehaviorProfiles;

/// <summary>Opcoes exclusivamente observacionais da etapa A2.1.4.</summary>
public sealed record BenchmarkBehaviorProfileOptions
{
    public bool Enabled { get; init; }
    public bool ApplicabilityCounterfactualEnabled { get; init; }

    public IReadOnlySet<string> Tags { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<BehaviorProfileMinuteRange> Ranges { get; init; } = [];

    public static BenchmarkBehaviorProfileOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var enabled = args.Any(argument => argument.Equals(
            "--tag-profile-observability",
            StringComparison.OrdinalIgnoreCase));

        var counterfactual = args.Any(argument => argument.Equals(
            "--detector-applicability-counterfactual",
            StringComparison.OrdinalIgnoreCase));

        if (counterfactual && !enabled)
            throw new InvalidOperationException(
                "--detector-applicability-counterfactual requer --tag-profile-observability.");

        if (!enabled)
            return new BenchmarkBehaviorProfileOptions();

        var rawTags = Read(args, "--tag-profile-tags");
        if (string.IsNullOrWhiteSpace(rawTags))
        {
            throw new InvalidOperationException(
                "--tag-profile-tags e obrigatorio quando " +
                "--tag-profile-observability esta ativo.");
        }

        var tags = new HashSet<string>(
            rawTags.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        var ranges = new List<BehaviorProfileMinuteRange>();
        var rawRanges = Read(args, "--tag-profile-ranges");
        if (!string.IsNullOrWhiteSpace(rawRanges))
        {
            foreach (var rawRange in rawRanges.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
            {
                ranges.Add(ParseRange(rawRange));
            }
        }

        return new BenchmarkBehaviorProfileOptions
        {
            Enabled = true,
            ApplicabilityCounterfactualEnabled = counterfactual,
            Tags = tags,
            Ranges = ranges
        };
    }

    public bool ShouldTrace(string tag, int minuteIndex) =>
        Enabled &&
        Tags.Contains(tag) &&
        (Ranges.Count == 0 || Ranges.Any(range => range.Contains(minuteIndex)));

    private static string? Read(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var prefix = name + "=";
            if (args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return args[index][prefix.Length..];

            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Count)
                return args[index + 1];
        }

        return null;
    }

    private static BehaviorProfileMinuteRange ParseRange(string value)
    {
        var separator = value.IndexOf('-');
        if (separator <= 0 ||
            separator >= value.Length - 1 ||
            !int.TryParse(value[..separator], out var start) ||
            !int.TryParse(value[(separator + 1)..], out var end) ||
            start < 0 ||
            end < start)
        {
            throw new InvalidOperationException(
                $"Intervalo de perfil invalido: '{value}'. Use inicio-fim.");
        }

        return new BehaviorProfileMinuteRange(start, end);
    }
}

public readonly record struct BehaviorProfileMinuteRange(
    int StartInclusive,
    int EndInclusive)
{
    public bool Contains(int minuteIndex) =>
        minuteIndex >= StartInclusive && minuteIndex <= EndInclusive;
}
