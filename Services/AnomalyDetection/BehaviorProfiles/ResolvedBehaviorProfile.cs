namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

/// <summary>
/// Decisao comportamental resolvida, com rastreabilidade para a declaracao e
/// para a inferencia. Ainda nao e consumida pelo pipeline principal.
/// </summary>
public sealed record ResolvedBehaviorProfile
{
    public TagBehaviorType Type { get; init; } = TagBehaviorType.Unknown;

    public double Confidence { get; init; }

    public BehaviorProfileOrigin Origin { get; init; } =
        BehaviorProfileOrigin.Unknown;

    public string Reason { get; init; } = "";

    public BehaviorProfileUnknownCause UnknownCause { get; init; }

    public bool HasConflict { get; init; }

    public bool IsStable { get; init; }

    public DeclaredTagType? Declaration { get; init; }

    public InferredBehaviorProfile? Inference { get; init; }
}
