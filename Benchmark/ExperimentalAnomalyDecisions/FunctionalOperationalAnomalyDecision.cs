namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

/// <summary>
/// B3.16 — contrato da decisão operacional funcional.
///
/// Mantém dois canais independentes:
///
/// 1. EmitAnomaly
///    Indica se esta leitura deve ser tratada como uma nova anomalia
///    no fluxo operacional.
///
/// 2. EmitNewEvidence
///    Indica se existe nova evidência relevante que precisa continuar
///    visível, mesmo quando não representa uma nova anomalia.
///
/// Exemplo importante:
///
/// CandidateMismatch:
/// - EmitAnomaly = false
/// - EmitNewEvidence = true
///
/// Assim uma transição B -> C não é perdida apenas porque não deve ser
/// contada automaticamente como uma nova anomalia independente.
///
/// Este contrato, sozinho, NÃO altera DetectorResult, FinalAnomaly,
/// arquivo Legacy, notificações ou baseline.
/// </summary>
public sealed record FunctionalOperationalAnomalyDecision(
    bool LegacyFinalAnomaly,
    bool EmitAnomaly,
    bool EmitNewEvidence,
    ExperimentalAnomalyDecisionKind SourceDecision,
    bool UsedLegacyFallback,
    IReadOnlyList<string> Reasons);