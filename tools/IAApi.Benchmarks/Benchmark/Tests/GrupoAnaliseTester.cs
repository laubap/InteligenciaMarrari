namespace IAApi.Benchmark.Tests;

public sealed class GrupoAnaliseTester
{
    private readonly GrupoAnaliseService _grupoAnaliseService;

    public GrupoAnaliseTester(
        GrupoAnaliseService grupoAnaliseService)
    {
        _grupoAnaliseService = grupoAnaliseService;
    }

    public async Task ExecutarAsync()
    {
        const string clienteId =
            "BENCHMARK_STRESS_V1";

        var grupos =
            await _grupoAnaliseService
                .ObterGruposAsync(clienteId);

        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "TESTE DE CARREGAMENTO DOS GRUPOS");
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            $"Cliente: {clienteId}");
        Console.WriteLine(
            $"Grupos carregados: {grupos.Count}");

        if (grupos.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Nenhum grupo foi carregado.");
            Console.WriteLine(
                "Verifique o arquivo:");
            Console.WriteLine(
                "Benchmark/TestData/MultivariateGroups.json");

            return;
        }

        foreach (var grupo in grupos)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Grupo: {grupo.Nome}");

            Console.WriteLine(
                $"Algoritmo: " +
                $"{(string.IsNullOrWhiteSpace(grupo.Algoritmo)
                    ? "não definido"
                    : grupo.Algoritmo)}");

            Console.WriteLine(
                $"Mínimo de tags: " +
                $"{grupo.MinimumAvailableTags}");

            Console.WriteLine(
                $"Atraso máximo: " +
                $"{grupo.MaxLagMinutes} minuto(s)");

            Console.WriteLine(
                $"Quantidade de tags: " +
                $"{grupo.Tags.Count}");

            foreach (var tag in grupo.Tags)
            {
                Console.WriteLine(
                    $"  - {tag}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "CARREGAMENTO CONCLUÍDO");
        Console.WriteLine(
            "========================================");
    }
}