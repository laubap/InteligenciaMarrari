namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

/// <summary>
/// B3.17 — gate determinístico de escopo canário.
///
/// Não decide se há anomalia.
/// Apenas decide se aquela leitura pode usar o rollout funcional
/// ou se deve permanecer no Legacy por estar fora do escopo.
/// </summary>
public sealed class FunctionalOperationalCanarySelector
{
    public bool IsInCanaryScope(
        FunctionalOperationalCanaryOptions options,
        int fastWindow,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        /*
         * Canary desabilitado:
         * não restringe o rollout funcional.
         *
         * Isso preserva o comportamento atual da B3.16/B3.17.
         */
        if (!options.Enabled)
            return true;

        /*
         * Fail-closed:
         * canário ligado sem configuração explícita não libera nada.
         */
        if (options.AllowedTags.Count == 0 ||
            options.AllowedFastWindows.Count == 0)
        {
            return false;
        }

        return
            options.AllowedTags.Contains(tag) &&
            options.AllowedFastWindows.Contains(fastWindow);
    }
}