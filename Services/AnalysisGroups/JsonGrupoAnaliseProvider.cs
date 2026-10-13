using System.Text.Json;

public sealed class JsonGrupoAnaliseProvider
    : IGrupoAnaliseProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<GrupoAnaliseDto>>
        CarregarGruposAsync(
            string clienteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            clienteId);

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Benchmark",
            "TestData",
            "MultivariateGroups.json"
        );

        Console.WriteLine(
            $"Carregando grupos de: {path}");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "O arquivo de grupos multivariados não foi encontrado.",
                path
            );
        }

        var json = await File.ReadAllTextAsync(path);

        var root =
            JsonSerializer.Deserialize<MultivariateRoot>(
                json,
                JsonOptions
            );

        if (root is null)
        {
            throw new InvalidOperationException(
                "Não foi possível desserializar o arquivo MultivariateGroups.json."
            );
        }

        if (root.MultivariateGroups.Count == 0)
        {
            throw new InvalidOperationException(
                "O arquivo foi carregado, mas nenhum grupo foi encontrado em multivariateGroups."
            );
        }

        return root.MultivariateGroups
            .Where(grupo => grupo.Enabled)
            .Select(grupo => new GrupoAnaliseDto
            {
                Id = 0,
                Nome = grupo.Name,
                Descricao = null,

                // Valores temporários para o teste.
                Algoritmo = "Correlation",
                Sensibilidade = 0.5,
                PesoEnsemble = 1.0,

                MinimumAvailableTags =
                    grupo.MinimumAvailableTags,

                MaxLagMinutes =
                    grupo.MaxLagMinutes,

                Tags = grupo.Tags
                    .Where(tag =>
                        !string.IsNullOrWhiteSpace(tag))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();
    }
}