using System.Diagnostics;
using System.Text.Json;
using IAApi.Benchmark;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// Treina os modelos multivariados utilizando somente o banco limpo.
///
/// Para cada grupo configurado:
/// 1. reúne as tags no mesmo minuto;
/// 2. mantém apenas vetores completos;
/// 3. calcula média, desvio, covariância e correlação;
/// 4. salva um modelo JSON por grupo.
///
/// Este serviço não utiliza o oracle.json.
/// </summary>
public sealed class MultivariateTrainingService
{
    private const long ProgressInterval = 1_000_000;

    private readonly GrupoAnaliseService _grupoAnaliseService;
    private readonly MultivariateGroupBufferService _bufferService;
    private readonly BenchmarkOptions _benchmarkOptions;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MultivariateTrainingService(
        GrupoAnaliseService grupoAnaliseService,
        MultivariateGroupBufferService bufferService,
        BenchmarkOptions benchmarkOptions)
    {
        _grupoAnaliseService =
            grupoAnaliseService ??
            throw new ArgumentNullException(
                nameof(grupoAnaliseService));

        _bufferService =
            bufferService ??
            throw new ArgumentNullException(
                nameof(bufferService));

        _benchmarkOptions =
            benchmarkOptions ??
            throw new ArgumentNullException(
                nameof(benchmarkOptions));
    }

    public async Task<MultivariateTrainingSummary> TreinarAsync(
        CancellationToken cancellationToken = default)
    {
        _benchmarkOptions.Validar();

        var clienteId =
            _benchmarkOptions.ClienteId;

        ArgumentException.ThrowIfNullOrWhiteSpace(
            clienteId);

        var cleanDatabasePath =
            Path.GetFullPath(
                _benchmarkOptions.CleanDatabasePath);

        if (!File.Exists(cleanDatabasePath))
        {
            throw new FileNotFoundException(
                "O banco limpo não foi encontrado.",
                cleanDatabasePath);
        }

        _bufferService.LimparCliente(clienteId);

        var grupos =
            await _grupoAnaliseService
                .ObterGruposAsync(clienteId);

        if (grupos.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nenhum grupo multivariado foi configurado " +
                $"para o cliente '{clienteId}'.");
        }

        var tags = grupos
            .SelectMany(grupo => grupo.Tags)
            .Where(tag =>
                !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tags.Length == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma tag multivariada válida foi encontrada.");
        }

        var acumuladores = grupos.ToDictionary(
            grupo => grupo.Nome,
            grupo => new OnlineMultivariateAccumulator(
                grupo.Nome,
                grupo.Tags),
            StringComparer.OrdinalIgnoreCase);

        var reader =
            new SqliteDatasetReader(
                cleanDatabasePath);

        var totalEsperado =
            reader.GetTotalSamples(tags);

        var summary =
            new MultivariateTrainingSummary
            {
                ClienteId = clienteId,
                CleanDatabasePath = cleanDatabasePath,
                InicioUtc = DateTime.UtcNow,
                TotalGruposConfigurados = grupos.Count,
                TotalTagsConfiguradas = tags.Length,
                TotalLeiturasEsperadas = totalEsperado
            };

        ExibirCabecalho(
            summary,
            cleanDatabasePath);

        var stopwatch =
            Stopwatch.StartNew();

        foreach (var sample in
                 reader.GetSamplesChronological(tags))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            summary.TotalLeiturasProcessadas++;

            if (!double.IsFinite(sample.Value))
            {
                summary.TotalLeiturasInvalidas++;
                continue;
            }

            var finalizados =
                await _bufferService
                    .AdicionarLeituraAsync(
                        clienteId,
                        sample.TagName,
                        sample.MinuteIndex,
                        sample.Value,
                        cancellationToken);

            ProcessarVetoresFinalizados(
                finalizados,
                acumuladores,
                summary);

            ExibirProgressoSeNecessario(
                summary,
                stopwatch);
        }

        var pendentes =
            _bufferService
                .FinalizarPendentes(clienteId);

        ProcessarVetoresFinalizados(
            pendentes,
            acumuladores,
            summary);

        stopwatch.Stop();

        var outputDirectory =
            ObterDiretorioModelos(clienteId);

        Directory.CreateDirectory(
            outputDirectory);

        foreach (var grupo in grupos)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (!acumuladores.TryGetValue(
                    grupo.Nome,
                    out var acumulador))
            {
                continue;
            }

            if (acumulador.SampleCount < 2)
            {
                summary.GruposIgnorados.Add(
                    new MultivariateIgnoredGroup
                    {
                        GroupName = grupo.Nome,
                        Reason =
                            "O grupo não possui pelo menos dois " +
                            "vetores completos para calcular covariância.",
                        CompleteVectors =
                            acumulador.SampleCount
                    });

                continue;
            }

            var model =
                acumulador.CriarModelo(
                    clienteId,
                    grupo.MinimumAvailableTags,
                    grupo.MaxLagMinutes);

            var fileName =
                CriarNomeArquivoSeguro(
                    grupo.Nome) + ".json";

            var modelPath =
                Path.Combine(
                    outputDirectory,
                    fileName);

            var json =
                JsonSerializer.Serialize(
                    model,
                    _jsonOptions);

            await File.WriteAllTextAsync(
                modelPath,
                json,
                cancellationToken);

            summary.ModelosGerados.Add(
                new MultivariateGeneratedModel
                {
                    GroupName = grupo.Nome,
                    ModelPath = modelPath,
                    CompleteVectors =
                        model.SampleCount,
                    TagCount =
                        model.Tags.Count
                });
        }

        summary.FimUtc = DateTime.UtcNow;
        summary.DuracaoSegundos =
            stopwatch.Elapsed.TotalSeconds;

        summary.TotalModelosGerados =
            summary.ModelosGerados.Count;

        var summaryPath =
            Path.Combine(
                outputDirectory,
                "training_summary.json");

        await File.WriteAllTextAsync(
            summaryPath,
            JsonSerializer.Serialize(
                summary,
                _jsonOptions),
            cancellationToken);

        _bufferService.LimparCliente(
            clienteId);

        ExibirResumo(
            summary,
            outputDirectory,
            summaryPath);

        return summary;
    }

    private static void ProcessarVetoresFinalizados(
        IReadOnlyList<BufferedGroupReading> finalizados,
        IReadOnlyDictionary<
            string,
            OnlineMultivariateAccumulator> acumuladores,
        MultivariateTrainingSummary summary)
    {
        foreach (var leitura in finalizados)
        {
            summary.TotalVetoresFinalizados++;

            /*
             * Para calcular uma matriz com dimensão fixa,
             * o treinamento utiliza somente grupos completos.
             *
             * Vetores parciais continuam úteis em inferência futura,
             * mas não entram no modelo-base desta primeira versão.
             */
            if (!leitura.EstaCompleto())
            {
                summary.TotalVetoresIncompletos++;
                continue;
            }

            if (!acumuladores.TryGetValue(
                    leitura.GroupName,
                    out var acumulador))
            {
                summary.TotalVetoresSemGrupo++;
                continue;
            }

            if (!acumulador.TryAdd(
                    leitura.Valores))
            {
                summary.TotalVetoresInvalidos++;
                continue;
            }

            summary.TotalVetoresTreinamento++;
        }
    }

    private string ObterDiretorioModelos(
        string clienteId)
    {
        return Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "ModelsML",
                "Multivariate",
                CriarNomeArquivoSeguro(clienteId)));
    }

    private static string CriarNomeArquivoSeguro(
        string value)
    {
        var invalidChars =
            Path.GetInvalidFileNameChars();

        var sanitized = new string(
            value
                .Trim()
                .Select(character =>
                    invalidChars.Contains(character)
                        ? '_'
                        : character)
                .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "unnamed"
            : sanitized;
    }

    private static void ExibirCabecalho(
        MultivariateTrainingSummary summary,
        string databasePath)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "   TREINAMENTO MULTIVARIADO");
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"Cliente: {summary.ClienteId}");

        Console.WriteLine(
            $"Banco limpo: {databasePath}");

        Console.WriteLine(
            $"Grupos: {summary.TotalGruposConfigurados}");

        Console.WriteLine(
            $"Tags distintas: {summary.TotalTagsConfiguradas}");

        Console.WriteLine(
            $"Leituras previstas: " +
            $"{summary.TotalLeiturasEsperadas:N0}");

        Console.WriteLine();
    }

    private static void ExibirProgressoSeNecessario(
        MultivariateTrainingSummary summary,
        Stopwatch stopwatch)
    {
        if (summary.TotalLeiturasProcessadas %
            ProgressInterval != 0)
        {
            return;
        }

        var segundos =
            stopwatch.Elapsed.TotalSeconds;

        var velocidade =
            segundos > 0
                ? summary.TotalLeiturasProcessadas /
                  segundos
                : 0;

        var percentual =
            summary.TotalLeiturasEsperadas > 0
                ? summary.TotalLeiturasProcessadas *
                  100.0 /
                  summary.TotalLeiturasEsperadas
                : 0;

        var restantes =
            Math.Max(
                0,
                summary.TotalLeiturasEsperadas -
                summary.TotalLeiturasProcessadas);

        var eta =
            velocidade > 0
                ? TimeSpan.FromSeconds(
                    restantes / velocidade)
                : TimeSpan.Zero;

        Console.WriteLine(
            $"Treinamento: {percentual:F2}% | " +
            $"{summary.TotalLeiturasProcessadas:N0}/" +
            $"{summary.TotalLeiturasEsperadas:N0} | " +
            $"{velocidade:N0} leituras/s | " +
            $"ETA {eta:hh\\:mm\\:ss} | " +
            $"Vetores {summary.TotalVetoresTreinamento:N0}");
    }

    private static void ExibirResumo(
        MultivariateTrainingSummary summary,
        string outputDirectory,
        string summaryPath)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            " TREINAMENTO MULTIVARIADO CONCLUÍDO");
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"Leituras processadas: " +
            $"{summary.TotalLeiturasProcessadas:N0}");

        Console.WriteLine(
            $"Vetores finalizados: " +
            $"{summary.TotalVetoresFinalizados:N0}");

        Console.WriteLine(
            $"Vetores usados: " +
            $"{summary.TotalVetoresTreinamento:N0}");

        Console.WriteLine(
            $"Vetores incompletos: " +
            $"{summary.TotalVetoresIncompletos:N0}");

        Console.WriteLine(
            $"Modelos gerados: " +
            $"{summary.TotalModelosGerados}");

        Console.WriteLine(
            $"Duração: " +
            $"{summary.DuracaoSegundos:N2} segundos");

        Console.WriteLine(
            $"Diretório: {outputDirectory}");

        Console.WriteLine(
            $"Resumo: {summaryPath}");
    }
}

/// <summary>
/// Calcula média e covariância incrementalmente,
/// sem manter todos os vetores na memória.
/// </summary>



public sealed class MultivariateTrainingSummary
{
    public string ClienteId { get; set; } = "";

    public string CleanDatabasePath { get; set; } = "";

    public DateTime InicioUtc { get; set; }

    public DateTime FimUtc { get; set; }

    public double DuracaoSegundos { get; set; }

    public int TotalGruposConfigurados { get; set; }

    public int TotalTagsConfiguradas { get; set; }

    public long TotalLeiturasEsperadas { get; set; }

    public long TotalLeiturasProcessadas { get; set; }

    public long TotalLeiturasInvalidas { get; set; }

    public long TotalVetoresFinalizados { get; set; }

    public long TotalVetoresTreinamento { get; set; }

    public long TotalVetoresIncompletos { get; set; }

    public long TotalVetoresInvalidos { get; set; }

    public long TotalVetoresSemGrupo { get; set; }

    public int TotalModelosGerados { get; set; }

    public List<MultivariateGeneratedModel>
        ModelosGerados { get; set; } = [];

    public List<MultivariateIgnoredGroup>
        GruposIgnorados { get; set; } = [];
}

public sealed class MultivariateGeneratedModel
{
    public string GroupName { get; set; } = "";

    public string ModelPath { get; set; } = "";

    public long CompleteVectors { get; set; }

    public int TagCount { get; set; }
}

public sealed class MultivariateIgnoredGroup
{
    public string GroupName { get; set; } = "";

    public string Reason { get; set; } = "";

    public long CompleteVectors { get; set; }
}
