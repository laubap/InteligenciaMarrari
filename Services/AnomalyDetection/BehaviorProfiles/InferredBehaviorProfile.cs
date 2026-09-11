namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

/// <summary>
/// Resultado exclusivamente estatistico. Nao representa metadado declarado
/// nem a decisao final usada pela IA.
/// </summary>
public sealed record InferredBehaviorProfile
{
    public TagBehaviorType Type { get; init; } = TagBehaviorType.Unknown;

    public double Confidence { get; init; }

    public string Reason { get; init; } = "";

    public IReadOnlyList<string> Evidence { get; init; } = [];

    public int SampleCount { get; init; }

    public TimeSpan ObservedDuration { get; init; }

    public DateTimeOffset? WindowStart { get; init; }

    public DateTimeOffset? WindowEnd { get; init; }

    public string AlgorithmVersion { get; init; } = "";

    public BehaviorProfileUnknownCause UnknownCause =>
        Type == TagBehaviorType.Unknown
            ? BehaviorProfileUnknownCause.InferenceInconclusive
            : BehaviorProfileUnknownCause.None;
}
