namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// Feature flag da integração funcional no runtime real.
///
/// Enabled = false:
/// comportamento Legacy exatamente como antes da B3.18.
///
/// Enabled = true + CanaryOnly = true:
/// somente clientes explicitamente permitidos usam a decisão funcional.
///
/// Enabled = true + CanaryOnly = false:
/// rollout global da decisão funcional.
///
/// Em qualquer configuração ambígua/incompleta do canário,
/// o comportamento é conservador: Legacy.
/// </summary>
public sealed class RuntimeAnomalyDecisionOptions
{
    public const string SectionName =
        "AnomalyDetection:RuntimeDecision";

    public bool Enabled { get; init; }

    public bool CanaryOnly { get; init; }

    public string[] AllowedClientIds { get; init; } =
        Array.Empty<string>();
}