using IAApi.Services.AnomalyDetection.Multivariate;

namespace IAApi.Benchmark.Tests;

public sealed class MultivariateDatasetTester
{
    private readonly GrupoAnaliseService _grupoAnaliseService;
    private readonly BenchmarkOptions _options;

    public MultivariateDatasetTester(
        GrupoAnaliseService grupoAnaliseService,
        BenchmarkOptions options)
    {
        _grupoAnaliseService =
            grupoAnaliseService ??
            throw new ArgumentNullException(
                nameof(grupoAnaliseService));

        _options =
            options ??
            throw new ArgumentNullException(
                nameof(options));
    }

    public async Task ExecutarAsync()
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "TESTE DAS TAGS MULTIVARIADAS NOS BANCOS");
        Console.WriteLine(
            "========================================");

        var grupos =
            await _grupoAnaliseService
                .ObterGruposAsync(
                    _options.ClienteId);

        var tagsConfiguradas = grupos
            .SelectMany(grupo => grupo.Tags)
            .Where(tag =>
                !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag)
            .ToList();

        Console.WriteLine(
            $"Cliente: {_options.ClienteId}");

        Console.WriteLine(
            $"Grupos configurados: {grupos.Count}");

        Console.WriteLine(
            $"Tags multivariadas distintas: " +
            $"{tagsConfiguradas.Count}");

        Console.WriteLine();

        var resultadoLimpo =
            AnalisarBanco(
                "BANCO LIMPO",
                _options.CleanDatabasePath,
                tagsConfiguradas);

        var resultadoAnomalo =
            AnalisarBanco(
                "BANCO COM ANOMALIAS",
                _options.AnomalyDatabasePath,
                tagsConfiguradas);

        var ausentesEmAmbos = tagsConfiguradas
            .Where(tag =>
                !resultadoLimpo.TagsEncontradas.Contains(tag) &&
                !resultadoAnomalo.TagsEncontradas.Contains(tag))
            .ToList();

        var somenteNoLimpo = tagsConfiguradas
            .Where(tag =>
                resultadoLimpo.TagsEncontradas.Contains(tag) &&
                !resultadoAnomalo.TagsEncontradas.Contains(tag))
            .ToList();

        var somenteNoAnomalo = tagsConfiguradas
            .Where(tag =>
                !resultadoLimpo.TagsEncontradas.Contains(tag) &&
                resultadoAnomalo.TagsEncontradas.Contains(tag))
            .ToList();

        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "RESUMO DA COMPARAÇÃO");
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"Configuradas: {tagsConfiguradas.Count}");

        Console.WriteLine(
            $"Encontradas no banco limpo: " +
            $"{resultadoLimpo.TagsEncontradas.Count}");

        Console.WriteLine(
            $"Encontradas no banco anômalo: " +
            $"{resultadoAnomalo.TagsEncontradas.Count}");

        Console.WriteLine(
            $"Ausentes nos dois bancos: " +
            $"{ausentesEmAmbos.Count}");

        Console.WriteLine(
            $"Somente no banco limpo: " +
            $"{somenteNoLimpo.Count}");

        Console.WriteLine(
            $"Somente no banco anômalo: " +
            $"{somenteNoAnomalo.Count}");

        ExibirLista(
            "TAGS AUSENTES NOS DOIS BANCOS",
            ausentesEmAmbos);

        ExibirLista(
            "TAGS SOMENTE NO BANCO LIMPO",
            somenteNoLimpo);

        ExibirLista(
            "TAGS SOMENTE NO BANCO ANÔMALO",
            somenteNoAnomalo);

        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "TESTE DO DATASET CONCLUÍDO");
        Console.WriteLine(
            "========================================");
    }

    private static ResultadoBanco
        AnalisarBanco(
            string nome,
            string caminhoBanco,
            IReadOnlyCollection<string> tagsConfiguradas)
    {
        Console.WriteLine(
            $"Analisando {nome}:");

        Console.WriteLine(
            $"Caminho: {Path.GetFullPath(caminhoBanco)}");

        var reader =
            new SqliteDatasetReader(
                caminhoBanco);

        var tagsDoBanco = reader
            .GetTags()
            .Select(tag => tag.TagName.Trim())
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var encontradas = tagsConfiguradas
            .Where(tagsDoBanco.Contains)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var ausentes = tagsConfiguradas
            .Where(tag =>
                !tagsDoBanco.Contains(tag))
            .OrderBy(tag => tag)
            .ToList();

        var totalAmostras =
            reader.GetTotalSamples(
                encontradas);

        Console.WriteLine(
            $"Tags totais existentes no banco: " +
            $"{tagsDoBanco.Count}");

        Console.WriteLine(
            $"Tags configuradas encontradas: " +
            $"{encontradas.Count}");

        Console.WriteLine(
            $"Tags configuradas ausentes: " +
            $"{ausentes.Count}");

        Console.WriteLine(
            $"Amostras válidas dessas tags: " +
            $"{totalAmostras:N0}");

        if (ausentes.Count > 0)
        {
            Console.WriteLine(
                "Tags ausentes:");

            foreach (var tag in ausentes)
            {
                Console.WriteLine(
                    $"  - {tag}");
            }
        }

        Console.WriteLine();

        return new ResultadoBanco
        {
            TagsEncontradas = encontradas,
            TagsAusentes = ausentes,
            TotalAmostras = totalAmostras
        };
    }

    private static void ExibirLista(
        string titulo,
        IReadOnlyCollection<string> itens)
    {
        if (itens.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(titulo);

        foreach (var item in itens)
        {
            Console.WriteLine(
                $"  - {item}");
        }
    }

    private sealed class ResultadoBanco
    {
        public HashSet<string> TagsEncontradas { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> TagsAusentes { get; init; } = [];

        public long TotalAmostras { get; init; }
    }
}