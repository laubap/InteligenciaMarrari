using System.Collections.Concurrent;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 — mantém o estado temporal necessário para montar vetores
/// multivariados entre diferentes scopes/requests HTTP.
///
/// Este store não carrega configuração de grupos e não depende de serviços
/// scoped. Ele guarda somente estado operacional compartilhável.
/// </summary>
public sealed class MultivariateRuntimeStateStore
{
    internal ConcurrentDictionary<string, BufferedGroupReading> Buffers { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    internal ConcurrentDictionary<string, long> UltimoMinutoPorCliente { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int ObterQuantidadeBuffers()
    {
        return Buffers.Count;
    }

    public void LimparCliente(string clienteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);

        var clienteNormalizado = clienteId.Trim();

        foreach (var item in Buffers.ToArray())
        {
            if (!string.Equals(
                    item.Value.ClienteId,
                    clienteNormalizado,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Buffers.TryRemove(
                item.Key,
                out _);
        }

        UltimoMinutoPorCliente.TryRemove(
            clienteNormalizado,
            out _);
    }

    public void LimparTudo()
    {
        Buffers.Clear();
        UltimoMinutoPorCliente.Clear();
    }
}