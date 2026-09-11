namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Classificação textual da força de uma evidência.
///
/// A classificação é derivada do valor numérico de Strength.
/// O Confidence Engine deve continuar usando o valor numérico entre 0 e 1.
/// </summary>
public enum EvidenceStrength
{
    None = 0,
    Weak = 1,
    Moderate = 2,
    Strong = 3,
    VeryStrong = 4
}

public static class EvidenceStrengthClassifier
{
    public static EvidenceStrength Classify(
        double strength)
    {
        var normalized =
            Normalize01(strength);

        return normalized switch
        {
            < 0.20 => EvidenceStrength.None,
            < 0.45 => EvidenceStrength.Weak,
            < 0.65 => EvidenceStrength.Moderate,
            < 0.85 => EvidenceStrength.Strong,
            _ => EvidenceStrength.VeryStrong
        };
    }

    private static double Normalize01(
        double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Clamp(
            value,
            0,
            1);
    }
}
