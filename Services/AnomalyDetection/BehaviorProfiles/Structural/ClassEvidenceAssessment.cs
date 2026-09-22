using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Structural;

public sealed record ClassEvidenceAssessment
{
    public TagBehaviorType Class { get; init; }

    public StructuralEvidenceStatus StructuralStatus { get; init; }

    public double Score { get; init; }

    public double Margin { get; init; }

    public IReadOnlyList<string> RequiredEvidence { get; init; } = [];

    public IReadOnlyList<string> SupportingEvidence { get; init; } = [];

    public IReadOnlyList<string> ContradictoryEvidence { get; init; } = [];

    public ObservationCoverage ObservationCoverage { get; init; } = new();

    public string Reason { get; init; } = "";
}