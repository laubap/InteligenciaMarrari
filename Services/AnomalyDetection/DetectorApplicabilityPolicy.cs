namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Decide se um detector pode participar do ensemble para o perfil
/// comportamental inferido.
///
/// Nesta A2 somente classificações de alta confiança restringem detectores.
/// Unknown/baixa confiança preservam o comportamento legado.
/// </summary>
public sealed class DetectorApplicabilityPolicy
{
    public const double MinimumRoutingConfidence = 0.85;

    public DetectorApplicabilityDecision Evaluate(
        IAnomalyDetector detector,
        TagBehaviorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.IsHighConfidence(
                MinimumRoutingConfidence))
        {
            return DetectorApplicabilityDecision.Allow(
                "Perfil desconhecido ou de baixa confiança; " +
                "compatibilidade com o ensemble legado.");
        }

        return profile.Type switch
        {
            TagBehaviorType.Continuous =>
                DetectorApplicabilityDecision.Allow(
                    "Detector permitido para sinal contínuo."),

            TagBehaviorType.Discrete =>
                DetectorApplicabilityDecision.Block(
                    "Detector contínuo bloqueado para tag discreta " +
                    "classificada com alta confiança."),

            TagBehaviorType.Counter =>
                DetectorApplicabilityDecision.Block(
                    "Detector contínuo bloqueado para contador " +
                    "classificado com alta confiança."),

            TagBehaviorType.Setpoint =>
                DetectorApplicabilityDecision.Block(
                    "Detector contínuo bloqueado para setpoint " +
                    "classificado com alta confiança."),

            _ =>
                DetectorApplicabilityDecision.Allow(
                    "Tipo comportamental desconhecido; comportamento legado.")
        };
    }
}

public sealed record DetectorApplicabilityDecision(
    bool IsAllowed,
    string Reason)
{
    public static DetectorApplicabilityDecision Allow(
        string reason)
    {
        return new DetectorApplicabilityDecision(
            true,
            reason);
    }

    public static DetectorApplicabilityDecision Block(
        string reason)
    {
        return new DetectorApplicabilityDecision(
            false,
            reason);
    }
}
