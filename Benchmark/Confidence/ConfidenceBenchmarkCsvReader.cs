using System.Globalization;
using System.Text;

namespace IAApi.Benchmark;

public static class ConfidenceBenchmarkCsvReader
{
    public static List<ConfidenceBenchmarkEvent> Read(
        string csvPath,
        CancellationToken cancellationToken = default)
    {
        var result =
            new List<ConfidenceBenchmarkEvent>();

        using var reader =
            new StreamReader(csvPath);

        var headerLine =
            reader.ReadLine();

        if (headerLine is null)
        {
            return result;
        }

        var headers =
            ParseCsvLine(headerLine);

        var indexes =
            headers
                .Select(
                    (name, index) =>
                        new
                        {
                            Name = name.Trim(),
                            Index = index
                        })
                .ToDictionary(
                    item => item.Name,
                    item => item.Index,
                    StringComparer.OrdinalIgnoreCase);

        RequireColumn(indexes, "tag");
        RequireColumn(indexes, "inicioMinuto");
        RequireColumn(indexes, "fimMinuto");
        RequireColumn(indexes, "duracaoMinutos");
        RequireColumn(indexes, "detectores");
        RequireColumn(indexes, "categoriaComportamental");

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields =
                ParseCsvLine(line);

            var detectors =
                GetField(
                        fields,
                        indexes["detectores"])
                    .Split(
                        '|',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Where(
                        item =>
                            !string.IsNullOrWhiteSpace(item))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        item => item,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var confidence =
                indexes.TryGetValue(
                    "confiancaClassificacao",
                    out var confidenceIndex)
                    ? ParseDouble(
                        GetField(
                            fields,
                            confidenceIndex))
                    : 0;

            result.Add(
                new ConfidenceBenchmarkEvent
                {
                    TagName =
                        GetField(
                            fields,
                            indexes["tag"]),

                    StartMinute =
                        ParseInt(
                            GetField(
                                fields,
                                indexes["inicioMinuto"])),

                    EndMinute =
                        ParseInt(
                            GetField(
                                fields,
                                indexes["fimMinuto"])),

                    DurationMinutes =
                        ParseInt(
                            GetField(
                                fields,
                                indexes["duracaoMinutos"])),

                    Category =
                        GetField(
                            fields,
                            indexes["categoriaComportamental"]),

                    StructuralClassificationConfidence =
                        confidence,

                    Detectors =
                        detectors
                });
        }

        return result;
    }

    private static List<string> ParseCsvLine(
        string line)
    {
        var fields =
            new List<string>();

        var current =
            new StringBuilder();

        var insideQuotes =
            false;

        for (var index = 0;
             index < line.Length;
             index++)
        {
            var character =
                line[index];

            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                insideQuotes =
                    !insideQuotes;

                continue;
            }

            if (character == ',' &&
                !insideQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        fields.Add(current.ToString());

        return fields;
    }

    private static void RequireColumn(
        IReadOnlyDictionary<string, int> indexes,
        string columnName)
    {
        if (!indexes.ContainsKey(columnName))
        {
            throw new InvalidDataException(
                $"A coluna obrigatória '{columnName}' não existe no CSV.");
        }
    }

    private static string GetField(
        IReadOnlyList<string> fields,
        int index)
    {
        return index >= 0 &&
               index < fields.Count
            ? fields[index]
            : "";
    }

    private static int ParseInt(
        string value)
    {
        return int.TryParse(
                   value,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out var result)
            ? result
            : 0;
    }

    private static double ParseDouble(
        string value)
    {
        return double.TryParse(
                   value,
                   NumberStyles.Float |
                   NumberStyles.AllowThousands,
                   CultureInfo.InvariantCulture,
                   out var result)
            ? result
            : 0;
    }
}
