namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

/// <summary>
/// B3.17 — configuração de escopo canário.
///
/// Quando Enabled = true, a decisão funcional só pode ser usada
/// para tags/janelas explicitamente permitidas.
///
/// Listas vazias são fail-closed:
/// nenhum item entra no canário por acidente.
/// </summary>
public sealed class FunctionalOperationalCanaryOptions
{
    public bool Enabled { get; init; }

    public IReadOnlySet<string> AllowedTags { get; init; } =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<int> AllowedFastWindows { get; init; } =
        new HashSet<int>();
}