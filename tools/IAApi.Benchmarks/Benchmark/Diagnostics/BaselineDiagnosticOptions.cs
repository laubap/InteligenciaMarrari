namespace IAApi.Benchmark.Diagnostics;

public sealed class BaselineDiagnosticOptions
{
    private readonly HashSet<string> _tags =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<MinuteRange> _ranges = [];

    public bool Enabled { get; private init; }

    public IReadOnlyCollection<string> Tags => _tags;

    public IReadOnlyList<MinuteRange> Ranges => _ranges;

    public static BaselineDiagnosticOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var enabled = args.Any(argument =>
            string.Equals(
                argument,
                "--b2-baseline-diagnostics",
                StringComparison.OrdinalIgnoreCase));

        var options = new BaselineDiagnosticOptions
        {
            Enabled = enabled
        };

        if (!enabled)
        {
            return options;
        }

        var tagsValue = ReadValue(args, "--b2-tags");

        if (string.IsNullOrWhiteSpace(tagsValue))
        {
            throw new InvalidOperationException(
                "--b2-tags é obrigatório quando " +
                "--b2-baseline-diagnostics está ativo.");
        }

        foreach (var tag in tagsValue.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            options._tags.Add(tag);
        }

        if (options._tags.Count == 0)
        {
            throw new InvalidOperationException(
                "--b2-tags deve conter ao menos uma tag.");
        }

        var rangesValue = ReadValue(args, "--b2-ranges");

        if (!string.IsNullOrWhiteSpace(rangesValue))
        {
            foreach (var rangeText in rangesValue.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
            {
                options._ranges.Add(ParseRange(rangeText));
            }
        }

        return options;
    }

    public bool ShouldTrace(string tagName, int minuteIndex)
    {
        if (!Enabled || !_tags.Contains(tagName))
        {
            return false;
        }

        return _ranges.Count == 0 ||
               _ranges.Any(range => range.Contains(minuteIndex));
    }

    private static string? ReadValue(
        IReadOnlyList<string> args,
        string optionName)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            var prefix = optionName + "=";

            if (argument.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return argument[prefix.Length..];
            }

            if (string.Equals(
                    argument,
                    optionName,
                    StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Count)
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static MinuteRange ParseRange(string value)
    {
        var separator = value.IndexOf('-');

        if (separator <= 0 || separator >= value.Length - 1 ||
            !int.TryParse(value[..separator], out var start) ||
            !int.TryParse(value[(separator + 1)..], out var end) ||
            start < 0 || end < start)
        {
            throw new InvalidOperationException(
                $"Intervalo B2 inválido: '{value}'. Use início-fim.");
        }

        return new MinuteRange(start, end);
    }
}

public readonly record struct MinuteRange(
    int StartInclusive,
    int EndInclusive)
{
    public bool Contains(int minuteIndex) =>
        minuteIndex >= StartInclusive &&
        minuteIndex <= EndInclusive;
}
