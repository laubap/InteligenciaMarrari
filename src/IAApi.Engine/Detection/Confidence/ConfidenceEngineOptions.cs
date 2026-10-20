namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Configuração inicial do Confidence Engine.
///
/// Esta primeira versão usa defaults conservadores e permanece isolada
/// do EnsembleDetectionService atual.
/// </summary>
public sealed class ConfidenceEngineOptions
{
    /// <summary>
    /// Score mínimo para classificar um evento como provável.
    /// </summary>
    public double ProbableThreshold { get; set; } = 0.48;

    /// <summary>
    /// Score mínimo para classificar um evento como confirmado.
    /// </summary>
    public double ConfirmedThreshold { get; set; } = 0.68;

    /// <summary>
    /// Número mínimo de famílias positivas para um evento provável.
    /// </summary>
    public int MinimumFamiliesForProbable { get; set; } = 2;

    /// <summary>
    /// Número mínimo de famílias positivas para um evento confirmado.
    /// </summary>
    public int MinimumFamiliesForConfirmed { get; set; } = 3;

    /// <summary>
    /// Limite máximo do score de um evento sustentado por uma família só.
    /// Impede que um detector/família isolado seja confirmado automaticamente.
    /// </summary>
    public double SingleFamilyScoreCap { get; set; } = 0.44;

    /// <summary>
    /// Limite máximo do score de um evento sustentado por duas famílias.
    /// </summary>
    public double TwoFamilyScoreCap { get; set; } = 0.67;

    /// <summary>
    /// Peso da diversidade de famílias no score final.
    /// </summary>
    public double FamilyDiversityWeight { get; set; } = 0.42;

    /// <summary>
    /// Peso da evidência consolidada no score final.
    /// </summary>
    public double EvidenceWeight { get; set; } = 0.33;

    /// <summary>
    /// Peso da confiança dos detectores no score final.
    /// </summary>
    public double DetectorConfidenceWeight { get; set; } = 0.15;

    /// <summary>
    /// Peso da confirmação temporal no score final.
    /// </summary>
    public double TemporalConfirmationWeight { get; set; } = 0.10;

    /// <summary>
    /// Exige confirmação temporal para elevar um evento de duas famílias
    /// ao nível confirmado.
    /// </summary>
    public bool RequireTemporalFamilyForTwoFamilyConfirmation { get; set; } =
        true;

    public void Validate()
    {
        ValidateRange(
            ProbableThreshold,
            nameof(ProbableThreshold));

        ValidateRange(
            ConfirmedThreshold,
            nameof(ConfirmedThreshold));

        ValidateRange(
            SingleFamilyScoreCap,
            nameof(SingleFamilyScoreCap));

        ValidateRange(
            TwoFamilyScoreCap,
            nameof(TwoFamilyScoreCap));

        ValidateRange(
            FamilyDiversityWeight,
            nameof(FamilyDiversityWeight));

        ValidateRange(
            EvidenceWeight,
            nameof(EvidenceWeight));

        ValidateRange(
            DetectorConfidenceWeight,
            nameof(DetectorConfidenceWeight));

        ValidateRange(
            TemporalConfirmationWeight,
            nameof(TemporalConfirmationWeight));

        if (ConfirmedThreshold <= ProbableThreshold)
        {
            throw new InvalidOperationException(
                "ConfirmedThreshold deve ser maior que ProbableThreshold.");
        }

        if (MinimumFamiliesForProbable < 1)
        {
            throw new InvalidOperationException(
                "MinimumFamiliesForProbable deve ser maior ou igual a 1.");
        }

        if (MinimumFamiliesForConfirmed <
            MinimumFamiliesForProbable)
        {
            throw new InvalidOperationException(
                "MinimumFamiliesForConfirmed não pode ser menor que " +
                "MinimumFamiliesForProbable.");
        }

        var totalWeight =
            FamilyDiversityWeight +
            EvidenceWeight +
            DetectorConfidenceWeight +
            TemporalConfirmationWeight;

        if (Math.Abs(totalWeight - 1.0) > 0.000001)
        {
            throw new InvalidOperationException(
                "A soma dos pesos do Confidence Engine deve ser igual a 1.");
        }
    }

    private static void ValidateRange(
        double value,
        string propertyName)
    {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value > 1)
        {
            throw new InvalidOperationException(
                $"{propertyName} deve estar entre 0 e 1.");
        }
    }
}
