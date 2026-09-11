namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.19 — snapshot persistível da decisão funcional do runtime.
///
/// Mantém separadas:
/// - decisão Legacy original do ensemble;
/// - decisão operacional emitida;
/// - nova evidência funcional;
/// - contexto de regime/incidente usado na decisão.
///
/// Este contrato não altera DetectorResult nem a decisão operacional.
/// Ele existe exclusivamente para auditoria/persistência.
/// </summary>
public sealed record RuntimeDecisionAuditData(
    bool LegacyFinalAnomaly,
    bool EmitAnomaly,
    bool EmitNewEvidence,
    string Source,
    bool UsedLegacyFallback,
    IReadOnlyList<string> Reasons,
    string? IncidentId,
    string? RegimeState,
    bool? CandidateBaselineReady)
{
    public static RuntimeDecisionAuditData From(
        RuntimeAnomalyDecision decision,
        RuntimeFunctionalDecisionContext? context)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new RuntimeDecisionAuditData(
            LegacyFinalAnomaly:
                decision.LegacyFinalAnomaly,
            EmitAnomaly:
                decision.EmitAnomaly,
            EmitNewEvidence:
                decision.EmitNewEvidence,
            Source:
                decision.Source,
            UsedLegacyFallback:
                decision.UsedLegacyFallback,
            Reasons:
                decision.Reasons.ToArray(),
            IncidentId:
                context?.IncidentId,
            RegimeState:
                context?.RegimeState,
            CandidateBaselineReady:
                context?.CandidateBaselineReady);
    }
}