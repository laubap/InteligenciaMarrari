// Planeja a coleta em lote sem misturar tags de clientes diferentes.
// Nomes de tag são deduplicados na aquisição, mas cada configuração ativa
// continua sendo analisada individualmente, preservando o fluxo anterior.
public sealed record MonitoramentoTagsLote(
    string ClienteId,
    IReadOnlyList<TagIaConfig> Tags,
    IReadOnlyList<string> NomesTags);

public static class MonitoramentoTagsBatchPlanner
{
    public static IReadOnlyList<MonitoramentoTagsLote> CriarLotes(
        IEnumerable<TagIaConfig> tagsAtivas)
    {
        ArgumentNullException.ThrowIfNull(tagsAtivas);

        return tagsAtivas
            .GroupBy(tag => tag.ClienteId, StringComparer.Ordinal)
            .Select(grupo =>
            {
                var tags = grupo.ToList();
                var nomesTags = tags
                    .Select(tag => tag.TagName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new MonitoramentoTagsLote(
                    grupo.Key,
                    tags,
                    nomesTags);
            })
            .ToList();
    }
}
