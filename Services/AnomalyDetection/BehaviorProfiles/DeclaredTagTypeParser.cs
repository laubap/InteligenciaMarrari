namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

/// <summary>
/// Normaliza o valor externo de TipoRepresentacao sem executar inferencia e
/// sem participar ainda de qualquer decisao do pipeline.
/// </summary>
public sealed class DeclaredTagTypeParser
{
    public const string LegacyMissingSentinel = "perfil-comportamental";

    public DeclaredTagType Parse(
        string? rawValue,
        string source)
    {
        if (rawValue is null)
        {
            return Unknown(
                rawValue,
                source,
                DeclaredTagTypeStatus.Missing);
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Unknown(
                rawValue,
                source,
                DeclaredTagTypeStatus.EmptyOrWhitespace);
        }

        var normalized = rawValue.Trim().ToLowerInvariant();

        if (normalized == LegacyMissingSentinel)
        {
            return Unknown(
                rawValue,
                source,
                DeclaredTagTypeStatus.LegacyMissing);
        }

        var kind = normalized switch
        {
            "continuous" or
            "continuo" or
            "analog" or
            "analogico" or
            "float" or
            "double" or
            "real" => TagBehaviorType.Continuous,

            "discrete" or
            "discreto" or
            "boolean" or
            "bool" or
            "digital" or
            "binary" or
            "binario" or
            "state" or
            "estado" => TagBehaviorType.Discrete,

            "counter" or
            "contador" or
            "totalizer" or
            "totalizador" or
            "accumulator" or
            "acumulador" => TagBehaviorType.Counter,

            "setpoint" or
            "set point" or
            "sp" => TagBehaviorType.Setpoint,

            _ => TagBehaviorType.Unknown
        };

        return kind == TagBehaviorType.Unknown
            ? Unknown(
                rawValue,
                source,
                DeclaredTagTypeStatus.InvalidOrUnrecognized)
            : new DeclaredTagType
            {
                Kind = kind,
                RawValue = rawValue,
                Source = source,
                Status = DeclaredTagTypeStatus.Recognized
            };
    }

    private static DeclaredTagType Unknown(
        string? rawValue,
        string source,
        DeclaredTagTypeStatus status)
    {
        return new DeclaredTagType
        {
            Kind = TagBehaviorType.Unknown,
            RawValue = rawValue,
            Source = source,
            Status = status
        };
    }
}
