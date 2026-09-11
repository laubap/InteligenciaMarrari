namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV06.3A — rollout da promoção operacional multivariada.
/// Enabled=false por padrão para separar observabilidade de autoridade real.
/// </summary>
public sealed class MultivariateOperationalPromotionOptions
{
    public const string SectionName =
        "AnomalyDetection:MultivariateOperationalPromotion";

    public bool Enabled { get; init; } = false;

    public bool CanaryOnly { get; init; } = true;

    public string[] AllowedClientIds { get; init; } =
        Array.Empty<string>();

    public int MaxEvidenceAgeMinutes { get; init; } = 2;
}
