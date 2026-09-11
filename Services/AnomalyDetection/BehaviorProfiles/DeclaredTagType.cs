namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

/// <summary>
/// Informacao de tipo fornecida externamente. Preserva o valor bruto e sua
/// procedencia; nao executa inferencia e nao substitui TipoTag nesta fase.
/// </summary>
public sealed record DeclaredTagType
{
    public TagBehaviorType Kind { get; init; } = TagBehaviorType.Unknown;

    public string? RawValue { get; init; }

    public string Source { get; init; } = "";

    public DeclaredTagTypeStatus Status { get; init; } =
        DeclaredTagTypeStatus.Missing;

    public bool IsRecognized =>
        Status == DeclaredTagTypeStatus.Recognized;

    public BehaviorProfileUnknownCause UnknownCause =>
        Status switch
        {
            DeclaredTagTypeStatus.Missing =>
                BehaviorProfileUnknownCause.MissingDeclaration,

            DeclaredTagTypeStatus.EmptyOrWhitespace =>
                BehaviorProfileUnknownCause.MissingDeclaration,

            DeclaredTagTypeStatus.LegacyMissing =>
                BehaviorProfileUnknownCause.MissingDeclaration,

            DeclaredTagTypeStatus.InvalidOrUnrecognized =>
                BehaviorProfileUnknownCause.InvalidOrUnrecognizedDeclaration,

            _ => BehaviorProfileUnknownCause.None
        };
}
