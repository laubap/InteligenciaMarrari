namespace IAApi.RegimePolicy;

public sealed record RegimeEvidenceSnapshot
{
    public RegimeEvidenceSnapshot(
        bool hasMaterialTransitionEdge = false,
        bool hasLevelShiftConfirmation = false,
        bool hasSlowDriftEvidence = false,
        bool isOutsideActiveRegime = false,
        bool hasReturnedToActiveRegime = false,
        bool hasPersistentDeviation = false,
        bool candidateFormationConfirmed = false,
        bool candidateInvalidated = false,
        string? proposedCandidateRegimeId = null,
        CandidateStability? candidateStability = null,
        LegitimacyEvidence? legitimacy = null,
        IEnumerable<string>? sourceEvidenceIds = null,
        bool hasIncreasingNoiseEvidence = false)
    {
        HasMaterialTransitionEdge = hasMaterialTransitionEdge;
        HasLevelShiftConfirmation = hasLevelShiftConfirmation;
        HasSlowDriftEvidence = hasSlowDriftEvidence;
        IsOutsideActiveRegime = isOutsideActiveRegime;
        HasReturnedToActiveRegime = hasReturnedToActiveRegime;
        HasPersistentDeviation = hasPersistentDeviation;
        HasIncreasingNoiseEvidence = hasIncreasingNoiseEvidence;
        CandidateFormationConfirmed = candidateFormationConfirmed;
        CandidateInvalidated = candidateInvalidated;
        ProposedCandidateRegimeId = string.IsNullOrWhiteSpace(proposedCandidateRegimeId)
            ? null
            : proposedCandidateRegimeId.Trim();
        CandidateStability = candidateStability ?? CandidateStability.Insufficient;
        Legitimacy = legitimacy ?? LegitimacyEvidence.None;
        SourceEvidenceIds = Array.AsReadOnly(
            (sourceEvidenceIds ?? []).ToArray());
    }

    public bool HasMaterialTransitionEdge { get; }
    public bool HasLevelShiftConfirmation { get; }
    public bool HasSlowDriftEvidence { get; }
    public bool IsOutsideActiveRegime { get; }
    public bool HasReturnedToActiveRegime { get; }
    public bool HasPersistentDeviation { get; }
    public bool HasIncreasingNoiseEvidence { get; }
    public bool CandidateFormationConfirmed { get; }
    public bool CandidateInvalidated { get; }
    public string? ProposedCandidateRegimeId { get; }
    public CandidateStability CandidateStability { get; }
    public LegitimacyEvidence Legitimacy { get; }
    public IReadOnlyList<string> SourceEvidenceIds { get; }

    public static RegimeEvidenceSnapshot None { get; } = new();
}
