using System.Text.Json;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Coletor opcional para salvar uma amostra das simulações em JSONL.
///
/// Ele não é usado automaticamente pela API. O chamador decide quando
/// registrar uma simulação, permitindo uso controlado em benchmark.
/// </summary>
public sealed class EnsembleSimulationCollector : IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public EnsembleSimulationCollector(
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException(
                "O caminho de saída é obrigatório.",
                nameof(outputPath));
        }

        var directory =
            Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer =
            new StreamWriter(
                outputPath,
                append: false);
    }

    public void Registrar(
        EnsembleSimulationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_sync)
        {
            ThrowIfDisposed();

            var json =
                JsonSerializer.Serialize(
                    result);

            _writer.WriteLine(
                json);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _writer.Flush();
            _writer.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
