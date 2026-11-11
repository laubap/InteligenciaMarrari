namespace IAApi.IntelligenceLab.Observability;

/// <summary>
/// Eventos observacionais do ciclo de vida de um vetor multivariado. Estes
/// eventos nunca concedem autoridade operacional ao resultado shadow.
/// </summary>
public enum MultivariateShadowEventKind
{
    ReadingAccepted = 1,
    VectorFinalized = 2,
    VectorDiscarded = 3,
    MahalanobisExecuted = 4,
    MahalanobisNotApplicable = 5,
    MahalanobisFailed = 6
}

public sealed record MultivariateShadowTagReading(
    string TagName,
    DateTime? SourceTimestamp,
    double? Value,
    string? SourceAnalysisCorrelationId);

/// <summary>
/// Registro temporário, local e limitado de uma leitura/vetor shadow. A
/// correlação aponta para diagnósticos univariados, mas não presume que o
/// vetor tenha sido concluído durante a mesma requisição HTTP.
/// </summary>
public sealed record MultivariateShadowDiagnostic(
    Guid DiagnosticId,
    Guid VectorId,
    MultivariateShadowEventKind EventKind,
    string ClienteId,
    string GroupName,
    long MinuteIndex,
    IReadOnlyList<string> ExpectedTags,
    IReadOnlyList<MultivariateShadowTagReading> Readings,
    int MinimumAvailableTags,
    int MaxLagMinutes,
    bool VectorComplete,
    string? ReasonCode,
    bool MahalanobisExecuted,
    bool? Anomaly,
    double? NormalizedScore,
    double? Threshold,
    double? MahalanobisSquared,
    DateTimeOffset RecordedAtUtc,
    string CounterfactualStatus = MultivariateShadowDiagnostic.CounterfactualNotAttached,
    string OperationalDecisionStatus = MultivariateShadowDiagnostic.OperationalDecisionUnaffected)
{
    // Estes campos descrevem somente este registro observacional. O eventual
    // avaliador contrafactual é um passo posterior e independente; este
    // coletor não o aciona nem altera a decisão operacional.
    public const string CounterfactualNotAttached = "NotEvaluatedByShadowCollector";
    public const string OperationalDecisionUnaffected = "NotMutatedByShadowCollector";
}
