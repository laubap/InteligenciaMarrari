namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Resultado da inferência comportamental de uma tag.
/// </summary>
public sealed class TagBehaviorProfile
{
    public TagBehaviorType Type { get; init; } =
        TagBehaviorType.Unknown;

    public double Confidence { get; init; }

    public string Reason { get; init; } = "";

    public int SampleCount { get; init; }

    public int DistinctValueCount { get; init; }

    public double DistinctValueRatio { get; init; }

    public double IntegerLikeRatio { get; init; }

    public double NonDecreasingRatio { get; init; }

    public double ChangeRatio { get; init; }

    public bool IsHighConfidence(
        double minimumConfidence = 0.85)
    {
        return Type != TagBehaviorType.Unknown &&
               Confidence >= minimumConfidence;
    }

    public static TagBehaviorProfile Unknown(
        string reason,
        int sampleCount = 0)
    {
        return new TagBehaviorProfile
        {
            Type =
                TagBehaviorType.Unknown,

            Confidence =
                0,

            Reason =
                reason,

            SampleCount =
                sampleCount
        };
    }
}
