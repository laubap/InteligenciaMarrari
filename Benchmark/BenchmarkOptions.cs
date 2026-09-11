namespace IAApi.Benchmark;

public class BenchmarkOptions
{
    public const string SectionName = "Benchmark";

    public string ClienteId { get; set; } =
        "BENCHMARK_STRESS_V1";

    public string CleanDatabasePath { get; set; } =
        string.Empty;

    public string AnomalyDatabasePath { get; set; } =
        string.Empty;

    public string ResultDirectory { get; set; } =
        "Results";

    public DateTime StartDateUtc { get; set; } =
        new(
            2025,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc
        );

    public int HistoryWindowSize { get; set; } =
        1440;

    public int MinimumHistoryPoints { get; set; } =
        50;

    public bool EnableLlm { get; set; }

    /// <summary>
    /// Quantidade aproximada de leituras desejada no benchmark rápido.
    /// O plano usa a densidade real do banco para transformar esse alvo
    /// em minutos distribuídos ao longo do dataset.
    /// </summary>
    public long FastTargetSamples { get; set; } =
        1_500_000;

    /// <summary>
    /// Quantidade de janelas temporais distribuídas pelo dataset.
    /// </summary>
    public int FastWindowCount { get; set; } =
        6;

    /// <summary>
    /// Evita janelas curtas demais para aquecimento e fenômenos temporais.
    /// </summary>
    public int FastMinimumWindowMinutes { get; set; } =
        1440;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(ClienteId))
        {
            throw new InvalidOperationException(
                "Benchmark:ClienteId não foi informado."
            );
        }

        ValidarBanco(
            CleanDatabasePath,
            "Benchmark:CleanDatabasePath"
        );

        ValidarBanco(
            AnomalyDatabasePath,
            "Benchmark:AnomalyDatabasePath"
        );

        if (string.IsNullOrWhiteSpace(ResultDirectory))
        {
            throw new InvalidOperationException(
                "Benchmark:ResultDirectory não foi informado."
            );
        }

        if (HistoryWindowSize < 10)
        {
            throw new InvalidOperationException(
                "Benchmark:HistoryWindowSize deve ser maior ou igual a 10."
            );
        }

        if (MinimumHistoryPoints < 10)
        {
            throw new InvalidOperationException(
                "Benchmark:MinimumHistoryPoints deve ser maior ou igual a 10."
            );
        }

        if (MinimumHistoryPoints > HistoryWindowSize)
        {
            throw new InvalidOperationException(
                "Benchmark:MinimumHistoryPoints não pode ser maior " +
                "que Benchmark:HistoryWindowSize."
            );
        }

        if (FastTargetSamples < 10_000)
        {
            throw new InvalidOperationException(
                "Benchmark:FastTargetSamples deve ser maior ou igual a 10.000."
            );
        }

        if (FastWindowCount < 1 ||
            FastWindowCount > 24)
        {
            throw new InvalidOperationException(
                "Benchmark:FastWindowCount deve estar entre 1 e 24."
            );
        }

        if (FastMinimumWindowMinutes < MinimumHistoryPoints)
        {
            throw new InvalidOperationException(
                "Benchmark:FastMinimumWindowMinutes deve ser maior ou igual " +
                "a Benchmark:MinimumHistoryPoints."
            );
        }
    }

    private static void ValidarBanco(
        string databasePath,
        string configurationName)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new InvalidOperationException(
                $"{configurationName} não foi informado."
            );
        }

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                $"O banco configurado em {configurationName} " +
                "não foi encontrado.",
                databasePath
            );
        }
    }
}