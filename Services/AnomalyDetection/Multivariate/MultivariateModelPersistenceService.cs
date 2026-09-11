using System.Collections.Concurrent;
using System.Text.Json;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// Carrega e mantém em cache os modelos multivariados treinados.
/// </summary>
public sealed class MultivariateModelPersistenceService
{
    private readonly IWebHostEnvironment _environment;

    private readonly ConcurrentDictionary<
        string,
        MultivariateGroupModel?> _cache =
            new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MultivariateModelPersistenceService(
        IWebHostEnvironment environment)
    {
        _environment = environment
            ?? throw new ArgumentNullException(
                nameof(environment));
    }

    public async Task<MultivariateGroupModel?> CarregarAsync(
        string clienteId,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        var cacheKey = CriarCacheKey(
            clienteId,
            groupName);

        if (_cache.TryGetValue(
                cacheKey,
                out var cachedModel))
        {
            return cachedModel;
        }

        var modelPath = ObterCaminhoModelo(
            clienteId,
            groupName);

        if (!File.Exists(modelPath))
        {
            _cache[cacheKey] = null;
            return null;
        }

        await using var stream = File.OpenRead(
            modelPath);

        var model =
            await JsonSerializer.DeserializeAsync<
                MultivariateGroupModel>(
                stream,
                JsonOptions,
                cancellationToken);

        if (model is null)
        {
            throw new InvalidOperationException(
                $"O modelo multivariado não pôde ser " +
                $"desserializado: {modelPath}");
        }

        ValidarModelo(
            model,
            modelPath);

        _cache[cacheKey] = model;

        return model;
    }

    public bool ExisteModelo(
        string clienteId,
        string groupName)
    {
        return File.Exists(
            ObterCaminhoModelo(
                clienteId,
                groupName));
    }

    public string ObterCaminhoModelo(
        string clienteId,
        string groupName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        return Path.Combine(
            _environment.ContentRootPath,
            "ModelsML",
            "Multivariate",
            CriarNomeArquivoSeguro(clienteId),
            $"{CriarNomeArquivoSeguro(groupName)}.json");
    }

    public void InvalidarCache(
        string? clienteId = null,
        string? groupName = null)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            _cache.Clear();
            return;
        }

        if (!string.IsNullOrWhiteSpace(groupName))
        {
            _cache.TryRemove(
                CriarCacheKey(
                    clienteId,
                    groupName),
                out _);

            return;
        }

        var prefix = $"{clienteId.Trim()}::";

        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                _cache.TryRemove(
                    key,
                    out _);
            }
        }
    }

    private static void ValidarModelo(
        MultivariateGroupModel model,
        string modelPath)
    {
        if (model.Tags.Count < 2)
        {
            throw new InvalidOperationException(
                $"O modelo '{modelPath}' possui menos " +
                "de duas tags.");
        }

        var dimension = model.Tags.Count;

        if (model.Means.Count != dimension ||
            model.StandardDeviations.Count != dimension ||
            model.CovarianceMatrix.Count != dimension)
        {
            throw new InvalidOperationException(
                $"O modelo '{modelPath}' possui dimensões inválidas.");
        }

        if (model.CovarianceMatrix.Any(
                row => row.Count != dimension))
        {
            throw new InvalidOperationException(
                $"A matriz de covariância do modelo " +
                $"'{modelPath}' é inválida.");
        }

        if (model.SampleCount < 2)
        {
            throw new InvalidOperationException(
                $"O modelo '{modelPath}' possui SampleCount inválido.");
        }

        if (model.MinimumAvailableTags < 2 ||
            model.MinimumAvailableTags > dimension)
        {
            throw new InvalidOperationException(
                $"O modelo '{modelPath}' possui MinimumAvailableTags inválido.");
        }

        if (model.Means.Any(value => !double.IsFinite(value)) ||
            model.StandardDeviations.Any(value =>
                !double.IsFinite(value) || value < 0) ||
            model.CovarianceMatrix.SelectMany(row => row)
                .Any(value => !double.IsFinite(value)))
        {
            throw new InvalidOperationException(
                $"O modelo '{modelPath}' contém valores numéricos inválidos.");
        }

        const double symmetryTolerance = 1e-8;

        for (var row = 0; row < dimension; row++)
        {
            if (model.CovarianceMatrix[row][row] < -symmetryTolerance)
            {
                throw new InvalidOperationException(
                    $"O modelo '{modelPath}' possui variância negativa.");
            }

            for (var column = row + 1; column < dimension; column++)
            {
                var a = model.CovarianceMatrix[row][column];
                var b = model.CovarianceMatrix[column][row];
                var scale = Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));

                if (Math.Abs(a - b) > symmetryTolerance * scale)
                {
                    throw new InvalidOperationException(
                        $"A matriz de covariância do modelo '{modelPath}' não é simétrica.");
                }
            }
        }
    }

    private static string CriarCacheKey(
        string clienteId,
        string groupName)
    {
        return $"{clienteId.Trim()}::{groupName.Trim()}";
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
}