namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// Snapshot imutável das métricas do runtime decision.
/// B3.20.6 acrescenta a janela temporal de observação sem quebrar
/// os construtores existentes: os timestamps são opcionais.
/// </summary>
public sealed record RuntimeDecisionMetricsSnapshot(
    long TotalDecisions,
    long EmitAnomalyTrue,
    long EmitAnomalyFalse,
    long EmitNewEvidenceTrue,
    long EmitNewEvidenceFalse,
    long UsedLegacyFallbackTrue,
    long UsedLegacyFallbackFalse,
    IReadOnlyDictionary<string, long> BySource,
    DateTimeOffset? FirstDecisionAtUtc = null,
    DateTimeOffset? LastDecisionAtUtc = null);