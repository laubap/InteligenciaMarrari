namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV06 — opções da fusão multivariada.
///
/// Nesta etapa a fusão é SOMENTE COUNTERFACTUAL:
/// mesmo quando Enabled=true, este componente não altera RuntimeAnomalyDecision.
/// </summary>
public sealed class MultivariateDecisionFusionOptions
{
    public const string SectionName =
        "AnomalyDetection:MultivariateDecisionFusion";

    /// <summary>
    /// Habilita apenas a avaliação counterfactual.
    /// Não concede autoridade operacional ao multivariado.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Quando true, somente clientes explicitamente permitidos
    /// participam da avaliação counterfactual.
    /// </summary>
    public bool CanaryOnly { get; init; } = true;

    public string[] AllowedClientIds { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Idade máxima, em minutos, entre o vetor multivariado finalizado
    /// e a leitura atual que provocou sua finalização.
    ///
    /// O valor padrão 2 é conservador e compatível com o MaxLag usado
    /// no primeiro grupo operacional validado.
    /// </summary>
    public int MaxEvidenceAgeMinutes { get; init; } = 2;
}
