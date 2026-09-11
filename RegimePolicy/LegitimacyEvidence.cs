namespace IAApi.RegimePolicy;

[Flags]
public enum LegitimacyEvidenceKind
{
    None = 0,
    KnownRegimeMatch = 1 << 0,
    ValidatedOperationalContext = 1 << 1,
    ExplicitModeOrSetpointChange = 1 << 2,
    HumanApproval = 1 << 3,
    MultivariateProcessCoherence = 1 << 4
}

public enum NewRegimePromotionAuthority
{
    None = 0,
    ValidatedOperationalContext = 1,
    HumanApproval = 2
}

public sealed record LegitimacyEvidence
{
    public LegitimacyEvidence(
        double confidence = 0,
        LegitimacyEvidenceKind kinds = LegitimacyEvidenceKind.None,
        string? matchedKnownRegimeId = null,
        NewRegimePromotionAuthority newRegimePromotionAuthority =
            NewRegimePromotionAuthority.None,
        bool hasSafetyViolation = false,
        bool hasContradictoryEvidence = false,
        IEnumerable<string>? reasons = null)
    {
        Confidence = double.IsFinite(confidence)
            ? Math.Clamp(confidence, 0, 1)
            : 0;
        Kinds = kinds;
        MatchedKnownRegimeId = Normalize(matchedKnownRegimeId);
        NewRegimePromotionAuthority = newRegimePromotionAuthority;
        HasSafetyViolation = hasSafetyViolation;
        HasContradictoryEvidence = hasContradictoryEvidence;
        Reasons = Array.AsReadOnly((reasons ?? []).ToArray());
    }

    public double Confidence { get; }
    public LegitimacyEvidenceKind Kinds { get; }
    public string? MatchedKnownRegimeId { get; }
    public NewRegimePromotionAuthority NewRegimePromotionAuthority { get; }
    public bool HasSafetyViolation { get; }
    public bool HasContradictoryEvidence { get; }
    public IReadOnlyList<string> Reasons { get; }

    public bool MatchesKnownRegime =>
        !string.IsNullOrWhiteSpace(MatchedKnownRegimeId) &&
        Kinds.HasFlag(LegitimacyEvidenceKind.KnownRegimeMatch);

    public bool AuthorizesNewRegime =>
        NewRegimePromotionAuthority != NewRegimePromotionAuthority.None;

    public static LegitimacyEvidence None { get; } = new();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
