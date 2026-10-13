using Microsoft.Data.Sqlite;

namespace IAApi.Benchmark;

public class SqliteDatasetReader
{
    private readonly string _connectionString;

    public SqliteDatasetReader(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException(
                "O caminho do banco SQLite não pode estar vazio.",
                nameof(databasePath));
        }

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                "O banco SQLite não foi encontrado.",
                databasePath);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
    }

    public List<TagInfo> GetTags()
    {
        var tags = new List<TagInfo>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                TagId,
                TagName,
                Unit
            FROM Tags
            ORDER BY TagId;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            tags.Add(new TagInfo
            {
                TagId = reader.GetInt32(0),
                TagName = reader.GetString(1),
                Unit = reader.IsDBNull(2)
                    ? string.Empty
                    : reader.GetString(2)
            });
        }

        return tags;
    }

    public IEnumerable<SamplePoint> GetSamples(int tagId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                MinuteIndex,
                Value,
                Quality
            FROM MinuteSamples
            WHERE TagId = $tagId
            ORDER BY MinuteIndex;
            """;

        command.Parameters.AddWithValue("$tagId", tagId);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            /*
             * No dataset de benchmark, as leituras com Quality = 2
             * possuem Value = NULL.
             *
             * Elas são ignoradas pelo adaptador do benchmark, sem alterar
             * nenhuma regra dos detectores ou do projeto principal.
             */
            if (reader.IsDBNull(1))
            {
                continue;
            }

            yield return new SamplePoint
            {
                MinuteIndex = reader.GetInt32(0),
                Value = reader.GetDouble(1),
                Quality = reader.IsDBNull(2)
                    ? 0
                    : reader.GetInt32(2)
            };
        }
    }

public IEnumerable<DatasetSample>
    GetSamplesChronological(
        IEnumerable<string> tagNames,
        int? startMinuteInclusive = null,
        int? endMinuteInclusive = null)
{
    var tags = tagNames
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (tags.Count == 0)
    {
        yield break;
    }

    using var connection =
        new SqliteConnection(_connectionString);

    connection.Open();

    using var command =
        connection.CreateCommand();

    var parametros = new List<string>();

    for (var i = 0; i < tags.Count; i++)
    {
        var nomeParametro = $"$tag{i}";

        parametros.Add(nomeParametro);

        command.Parameters.AddWithValue(
            nomeParametro,
            tags[i]
        );
    }

    command.Parameters.AddWithValue(
        "$startMinute",
        startMinuteInclusive.HasValue
            ? startMinuteInclusive.Value
            : DBNull.Value);

    command.Parameters.AddWithValue(
        "$endMinute",
        endMinuteInclusive.HasValue
            ? endMinuteInclusive.Value
            : DBNull.Value);

    command.CommandText = $"""
        SELECT
            ms.TagId,
            t.TagName,
            ms.MinuteIndex,
            ms.Value,
            ms.Quality
        FROM MinuteSamples AS ms
        INNER JOIN Tags AS t
            ON t.TagId = ms.TagId
        WHERE
            ms.Value IS NOT NULL
            AND COALESCE(ms.Quality, 0) <> 2
            AND t.TagName IN ({string.Join(", ", parametros)})
            AND ($startMinute IS NULL OR ms.MinuteIndex >= $startMinute)
            AND ($endMinute IS NULL OR ms.MinuteIndex <= $endMinute)
        ORDER BY
            ms.MinuteIndex,
            ms.TagId;
        """;

    using var reader =
        command.ExecuteReader();

    while (reader.Read())
    {
        yield return new DatasetSample
        {
            TagId =
                reader.GetInt32(0),

            TagName =
                reader.GetString(1),

            MinuteIndex =
                reader.GetInt32(2),

            Value =
                reader.GetDouble(3),

            Quality =
                reader.IsDBNull(4)
                    ? 0
                    : reader.GetInt32(4)
        };
    }
}

public long GetTotalSamples(
    IEnumerable<string> tagNames,
    int? startMinuteInclusive = null,
    int? endMinuteInclusive = null)
{
    var tags = tagNames
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (tags.Count == 0)
    {
        return 0;
    }

    using var connection =
        new SqliteConnection(_connectionString);

    connection.Open();

    using var command =
        connection.CreateCommand();

    var parametros = new List<string>();

    for (var i = 0; i < tags.Count; i++)
    {
        var nomeParametro = $"$tag{i}";

        parametros.Add(nomeParametro);

        command.Parameters.AddWithValue(
            nomeParametro,
            tags[i]
        );
    }

    command.Parameters.AddWithValue(
        "$startMinute",
        startMinuteInclusive.HasValue
            ? startMinuteInclusive.Value
            : DBNull.Value);

    command.Parameters.AddWithValue(
        "$endMinute",
        endMinuteInclusive.HasValue
            ? endMinuteInclusive.Value
            : DBNull.Value);

    command.CommandText = $"""
        SELECT COUNT(*)
        FROM MinuteSamples AS ms
        INNER JOIN Tags AS t
            ON t.TagId = ms.TagId
        WHERE
            ms.Value IS NOT NULL
            AND COALESCE(ms.Quality, 0) <> 2
            AND t.TagName IN ({string.Join(", ", parametros)})
            AND ($startMinute IS NULL OR ms.MinuteIndex >= $startMinute)
            AND ($endMinute IS NULL OR ms.MinuteIndex <= $endMinute);
        """;

    return Convert.ToInt64(
        command.ExecuteScalar()
    );
}


public (int MinMinute, int MaxMinute)? GetMinuteBounds(
    IEnumerable<string> tagNames)
{
    var tags = tagNames
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (tags.Count == 0)
        return null;

    using var connection =
        new SqliteConnection(_connectionString);

    connection.Open();

    using var command =
        connection.CreateCommand();

    var parametros =
        new List<string>();

    for (var i = 0; i < tags.Count; i++)
    {
        var nomeParametro =
            $"$tag{i}";

        parametros.Add(
            nomeParametro);

        command.Parameters.AddWithValue(
            nomeParametro,
            tags[i]);
    }

    command.CommandText = $"""
        SELECT
            MIN(ms.MinuteIndex),
            MAX(ms.MinuteIndex)
        FROM MinuteSamples AS ms
        INNER JOIN Tags AS t
            ON t.TagId = ms.TagId
        WHERE
            ms.Value IS NOT NULL
            AND COALESCE(ms.Quality, 0) <> 2
            AND t.TagName IN ({string.Join(", ", parametros)});
        """;

    using var reader =
        command.ExecuteReader();

    if (!reader.Read() ||
        reader.IsDBNull(0) ||
        reader.IsDBNull(1))
    {
        return null;
    }

    return (
        reader.GetInt32(0),
        reader.GetInt32(1));
}

}

public class TagInfo
{
    public int TagId { get; set; }

    public string TagName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;
}

public class SamplePoint
{
    public int MinuteIndex { get; set; }

    public double Value { get; set; }

    public int Quality { get; set; }
}

public class DatasetSample
{
    public int TagId { get; set; }

    public string TagName { get; set; } =
        string.Empty;

    public int MinuteIndex { get; set; }

    public double Value { get; set; }

    public int Quality { get; set; }
}