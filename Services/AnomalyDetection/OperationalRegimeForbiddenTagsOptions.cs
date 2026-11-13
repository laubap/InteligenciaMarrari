namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Lista, por cliente, de tags que nunca podem ser tag de regime
/// (backlog item 11 / RC-7). Exemplo: no CP300/PK2, TAG60–TAG81 são
/// rótulos (gabarito) do simulador; usá-las como regime vazaria a
/// resposta para a decisão.
///
/// Seção: <c>AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente</c>.
/// Vazia por padrão: sem cadastro, o provider se comporta exatamente
/// como antes. A regra é por cliente para não bloquear tags legítimas
/// de outros clientes.
///
/// Comparação de cliente e tag: sem diferenciar maiúsculas/minúsculas,
/// ignorando espaços nas pontas. A tag tem de bater por inteiro
/// (não há faixa nem prefixo); cadastrar cada tag da lista.
/// </summary>
public sealed class OperationalRegimeForbiddenTagsOptions
{
    public const string SectionName =
        "AnomalyDetection:RegimeOperacional";

    public Dictionary<string, List<string>> TagsProibidasPorCliente { get; set; } =
        new();

    /// <summary>
    /// Verdadeiro se <paramref name="tagName"/> estiver na lista de tags
    /// proibidas como regime do cliente <paramref name="clienteId"/>.
    /// Usada para a TagRegime atual e, na RC-8, para cada componente.
    /// </summary>
    public bool IsForbidden(
        string? clienteId,
        string? tagName)
    {
        if (TagsProibidasPorCliente is null ||
            TagsProibidasPorCliente.Count == 0 ||
            string.IsNullOrWhiteSpace(clienteId) ||
            string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var cliente = clienteId.Trim();
        var tag = tagName.Trim();

        foreach (var (key, tags) in TagsProibidasPorCliente)
        {
            if (tags is null ||
                key is null ||
                !string.Equals(
                    key.Trim(),
                    cliente,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var forbidden in tags)
            {
                if (forbidden is not null &&
                    string.Equals(
                        forbidden.Trim(),
                        tag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
