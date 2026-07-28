using System.Text.Json;

namespace IAApi.Services.AnomalyDetection;

public class IsolationForestPersistenceService
{
    private readonly IWebHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    public IsolationForestPersistenceService(
        IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string ObterCaminhoModelo(
        string clienteId,
        string tagName)
    {
        var clienteSeguro =
            SanitizarNomeArquivo(clienteId);

        var tagSegura =
            SanitizarNomeArquivo(tagName);

        return Path.Combine(
            _environment.ContentRootPath,
            "Models",
            "IsolationForest",
            clienteSeguro,
            $"{tagSegura}.iforest.json"
        );
    }

    public bool ModeloExiste(
        string clienteId,
        string tagName)
    {
        var caminho =
            ObterCaminhoModelo(
                clienteId,
                tagName
            );

        return File.Exists(caminho);
    }

    public async Task SalvarAsync(
        IsolationForestSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var caminho =
            ObterCaminhoModelo(
                snapshot.ClienteId,
                snapshot.TagName
            );

        var pasta =
            Path.GetDirectoryName(caminho);

        if (string.IsNullOrWhiteSpace(pasta))
        {
            throw new InvalidOperationException(
                "Não foi possível determinar a pasta do modelo."
            );
        }

        Directory.CreateDirectory(pasta);

        var json =
            JsonSerializer.Serialize(
                snapshot,
                JsonOptions
            );

        var caminhoTemporario =
            caminho + ".tmp";

        await File.WriteAllTextAsync(
            caminhoTemporario,
            json,
            cancellationToken
        );

        File.Move(
            caminhoTemporario,
            caminho,
            overwrite: true
        );
    }

    public async Task<IsolationForestSnapshot?> CarregarAsync(
        string clienteId,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        var caminho =
            ObterCaminhoModelo(
                clienteId,
                tagName
            );

        if (!File.Exists(caminho))
        {
            return null;
        }

        var json =
            await File.ReadAllTextAsync(
                caminho,
                cancellationToken
            );

        return JsonSerializer.Deserialize<
            IsolationForestSnapshot
        >(
            json,
            JsonOptions
        );
    }

    public void RemoverModelo(
        string clienteId,
        string tagName)
    {
        var caminho =
            ObterCaminhoModelo(
                clienteId,
                tagName
            );

        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }
    }

    private static string SanitizarNomeArquivo(
        string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return "sem-identificacao";
        }

        var caracteresInvalidos =
            Path.GetInvalidFileNameChars();

        var resultado =
            new string(
                valor
                    .Select(caractere =>
                        caracteresInvalidos.Contains(caractere)
                            ? '_'
                            : caractere
                    )
                    .ToArray()
            );

        return resultado
            .Replace(".", "_")
            .Replace(" ", "_");
    }
}