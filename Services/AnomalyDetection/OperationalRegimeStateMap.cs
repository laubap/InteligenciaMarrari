using System.Globalization;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Mapa de estados de um componente do regime combinado
/// (PLANO_REGIME_COMBINADO.md §5.5, §6.2 / RC-4).
///
/// Sintaxe ASCII: entradas <c>chave:valor</c> separadas por <c>;</c>.
/// A chave é um número ou <c>*</c> (curinga: qualquer valor que não casou
/// com uma entrada numérica). O valor é sempre um número.
/// Números em <c>InvariantCulture</c> (ponto decimal). Espaços em volta
/// dos itens são ignorados.
///
/// Exemplos (configuração alvo das bombas, §6.2):
/// <c>2:2;*:0</c> (estado da bomba: 2 = ligada, qualquer outro = 0) e
/// <c>380:380;*:450</c> (TAG46).
///
/// Erro de parse: texto vazio, entrada vazia, entrada sem <c>:</c> ou com
/// mais de um, número inválido, valor não finito, valor <c>*</c>, chave
/// numérica repetida (numericamente igual, ex.: <c>2</c> e <c>2.0</c>)
/// e mais de um <c>*</c>.
///
/// Decisões de §6.2 (fechadas na RC-4):
/// - comparação: a leitura casa com uma entrada numérica se
///   |leitura − chave| ≤ ToleranciaEstado; com várias entradas que casam,
///   vale a primeira na ordem escrita; depois de mapeado, o resolver
///   compara valores exatos;
/// - valor fora do mapa e sem <c>*</c>: passa sem mudança
///   (<see cref="OperationalRegimeStateMapStatus.OutOfMap"/>), para o
///   relatório de cobertura do replay listar esses valores.
///
/// Na RC-4 nada usa esta classe; o uso entra na RC-6/RC-8.
/// </summary>
public sealed class OperationalRegimeStateMap
{
    private readonly OperationalRegimeStateMapEntry[] _entries;

    private OperationalRegimeStateMap(
        OperationalRegimeStateMapEntry[] entries,
        double? wildcardValue)
    {
        _entries = entries;
        WildcardValue = wildcardValue;
    }

    /// <summary>Entradas numéricas, na ordem escrita.</summary>
    public IReadOnlyList<OperationalRegimeStateMapEntry> Entries =>
        _entries;

    /// <summary>Valor do curinga <c>*</c>, ou nulo se não houver.</summary>
    public double? WildcardValue { get; }

    public static OperationalRegimeStateMap Parse(
        string? text)
    {
        if (!TryParse(text, out var map, out var error))
        {
            throw new FormatException(error);
        }

        return map!;
    }

    public static bool TryParse(
        string? text,
        out OperationalRegimeStateMap? map,
        out string? error)
    {
        map = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Mapa de estados vazio.";
            return false;
        }

        var entries = new List<OperationalRegimeStateMapEntry>();
        double? wildcard = null;

        var parts = text.Split(';');

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();

            if (part.Length == 0)
            {
                error = $"Mapa de estados '{text}': entrada {i + 1} vazia.";
                return false;
            }

            var pair = part.Split(':');

            if (pair.Length != 2)
            {
                error = $"Mapa de estados '{text}': entrada '{part}' deve ter a forma chave:valor.";
                return false;
            }

            var keyText = pair[0].Trim();
            var valueText = pair[1].Trim();

            if (!TryParseNumber(valueText, out var mapped))
            {
                error = $"Mapa de estados '{text}': valor '{valueText}' da entrada '{part}' não é um número finito.";
                return false;
            }

            if (keyText == "*")
            {
                if (wildcard is not null)
                {
                    error = $"Mapa de estados '{text}': curinga '*' repetido.";
                    return false;
                }

                wildcard = mapped;
                continue;
            }

            if (!TryParseNumber(keyText, out var key))
            {
                error = $"Mapa de estados '{text}': chave '{keyText}' da entrada '{part}' não é um número finito nem '*'.";
                return false;
            }

            if (entries.Any(e => e.Key == key))
            {
                error = $"Mapa de estados '{text}': chave '{keyText}' repetida.";
                return false;
            }

            entries.Add(new OperationalRegimeStateMapEntry(key, mapped));
        }

        map = new OperationalRegimeStateMap(entries.ToArray(), wildcard);
        return true;
    }

    /// <summary>
    /// Aplica o mapa a uma leitura do componente.
    /// <paramref name="tolerance"/> é a ToleranciaEstado da configuração.
    /// Leitura não finita: passa sem mudança, como fora do mapa
    /// (o resolver já descarta esses pontos antes).
    /// </summary>
    public OperationalRegimeStateMapping Apply(
        double value,
        double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tolerance),
                tolerance,
                "A tolerância deve ser um número finito maior ou igual a zero.");
        }

        if (!double.IsFinite(value))
        {
            return new OperationalRegimeStateMapping(
                value,
                value,
                OperationalRegimeStateMapStatus.OutOfMap);
        }

        foreach (var entry in _entries)
        {
            if (Math.Abs(value - entry.Key) <= tolerance)
            {
                return new OperationalRegimeStateMapping(
                    value,
                    entry.Value,
                    OperationalRegimeStateMapStatus.Entry);
            }
        }

        if (WildcardValue is double wildcard)
        {
            return new OperationalRegimeStateMapping(
                value,
                wildcard,
                OperationalRegimeStateMapStatus.Wildcard);
        }

        return new OperationalRegimeStateMapping(
            value,
            value,
            OperationalRegimeStateMapStatus.OutOfMap);
    }

    /// <summary>Forma canônica (InvariantCulture), curinga por último.</summary>
    public override string ToString()
    {
        var items =
            _entries
                .Select(e => $"{Format(e.Key)}:{Format(e.Value)}")
                .ToList();

        if (WildcardValue is double wildcard)
        {
            items.Add($"*:{Format(wildcard)}");
        }

        return string.Join(";", items);
    }

    private static string Format(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static bool TryParseNumber(
        string text,
        out double value)
    {
        // NumberStyles.Float: sinal, ponto decimal e expoente; sem
        // separador de milhar (evita "2,5" virar 25).
        return double.TryParse(
                   text,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out value) &&
               double.IsFinite(value);
    }
}

public readonly record struct OperationalRegimeStateMapEntry(
    double Key,
    double Value);

public enum OperationalRegimeStateMapStatus
{
    /// <summary>Casou com uma entrada numérica.</summary>
    Entry,

    /// <summary>Não casou com entrada numérica; mapeado pelo curinga.</summary>
    Wildcard,

    /// <summary>Sem entrada e sem curinga: passa sem mudança.</summary>
    OutOfMap
}

public readonly record struct OperationalRegimeStateMapping(
    double OriginalValue,
    double MappedValue,
    OperationalRegimeStateMapStatus Status);
