namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.18 â€” decisÃ£o operacional usada pelo runtime real.
///
/// O resultado Legacy do ensemble permanece imutÃ¡vel.
/// Esta classe apenas informa quais efeitos externos podem ocorrer.
/// </summary>
public sealed record RuntimeAnomalyDecision(
    bool LegacyFinalAnomaly,
    bool EmitAnomaly,
    bool EmitNewEvidence,
    bool UsedLegacyFallback,
    string Source,
    IReadOnlyList<string> Reasons)
{
    public bool RequiresNewInterpretation =>
        EmitAnomaly &&
        EmitNewEvidence;

    public bool IsAnomalyContinuation =>
        EmitAnomaly &&
        !EmitNewEvidence;
}

/// <summary>
/// Candidato funcional opcional.
///
/// Nesta primeira etapa da B3.18 ele ainda nÃ£o Ã© produzido pelo runtime.
/// O contrato existe para permitir conectar a decisÃ£o funcional depois,
/// sem alterar DetectorResult nem o EnsembleDetectionService.
/// </summary>
public sealed record RuntimeFunctionalDecisionCandidate(
    bool EmitAnomaly,
    bool EmitNewEvidence,
    string Source,
    IReadOnlyList<string> Reasons);
