using System.Globalization;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Chave textual do regime combinado (PLANO_REGIME_COMBINADO.md §6.2 / RC-4).
///
/// Formato: <c>TAG42=2|TAG43=0|TAG46=380</c>, com os valores já
/// <b>mapeados</b>, na ordem dos componentes (componente 1 primeiro),
/// números em <c>InvariantCulture</c> (forma mais curta que volta ao
/// mesmo double; <c>-0</c> vira <c>0</c>). O nome da tag é usado como
/// veio, sem espaços nas pontas.
///
/// É a futura chave dos perfis por regime (PROP §4-A). Na RC-4 nada usa
/// esta classe; o uso entra na RC-6.
/// </summary>
public static class OperationalRegimeKey
{
    public const char ComponentSeparator = '|';

    public const char ValueSeparator = '=';

    public static string Build(
        IReadOnlyList<OperationalRegimeKeyComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        if (components.Count == 0)
        {
            throw new ArgumentException(
                "A chave de regime precisa de pelo menos um componente.",
                nameof(components));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = new string[components.Count];

        for (var i = 0; i < components.Count; i++)
        {
            var tag = components[i].TagName?.Trim();

            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentException(
                    $"Componente {i + 1} da chave de regime sem nome de tag.",
                    nameof(components));
            }

            if (tag.Contains(ComponentSeparator) ||
                tag.Contains(ValueSeparator))
            {
                throw new ArgumentException(
                    $"Tag '{tag}' contém '{ComponentSeparator}' ou '{ValueSeparator}', " +
                    "que são separadores da chave de regime.",
                    nameof(components));
            }

            if (!seen.Add(tag))
            {
                throw new ArgumentException(
                    $"Tag '{tag}' repetida na chave de regime.",
                    nameof(components));
            }

            parts[i] =
                tag +
                ValueSeparator +
                FormatValue(components[i].MappedValue);
        }

        return string.Join(ComponentSeparator, parts);
    }

    public static string FormatValue(
        double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Valor de componente da chave de regime deve ser finito.");
        }

        if (value == 0)
        {
            // -0 e 0 são o mesmo estado.
            value = 0;
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}

public readonly record struct OperationalRegimeKeyComponent(
    string TagName,
    double MappedValue);
