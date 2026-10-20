namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Representação padronizada de uma evidência produzida por um detector.
///
/// Evidence não representa a decisão final de anomalia.
/// Ela descreve apenas:
/// - qual detector observou o comportamento;
/// - qual família de evidência ele representa;
/// - qual foi a força da evidência;
/// - quão confiável foi o cálculo;
/// - qual foi a severidade estimada;
/// - se o threshold interno foi ultrapassado.
/// </summary>
public sealed class Evidence
{
    public string Detector { get; init; } = "";

    public DetectorFamily Family { get; init; } =
        DetectorFamily.Unknown;

    /// <summary>
    /// Indica se o detector possuía condições técnicas para produzir
    /// uma evidência válida.
    /// </summary>
    public bool Applicable { get; init; }

    /// <summary>
    /// Indica se o detector ultrapassou seu threshold interno.
    /// Isso não representa a decisão final do sistema.
    /// </summary>
    public bool PositiveVote { get; init; }

    /// <summary>
    /// Força contínua da evidência entre 0 e 1.
    /// </summary>
    public double Strength { get; init; }

    /// <summary>
    /// Confiança na qualidade do cálculo entre 0 e 1.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Severidade estatística ou física estimada entre 0 e 1.
    /// </summary>
    public double Severity { get; init; }

    /// <summary>
    /// Peso base configurado para o detector entre 0 e 1.
    /// </summary>
    public double BaseWeight { get; init; }

    public EvidenceStrength StrengthLevel =>
        EvidenceStrengthClassifier.Classify(
            Strength);

    public string Explanation { get; init; } = "";

    public string? NotApplicableReason { get; init; }

    public LimitesDetector? Limits { get; init; }

    public IReadOnlyDictionary<string, double> Metrics { get; init; } =
        new Dictionary<string, double>();

    /// <summary>
    /// Produto simples usado apenas como medida auxiliar de qualidade.
    /// Não deve ser tratado sozinho como decisão final.
    /// </summary>
    public double EffectiveStrength =>
        Normalize01(
            Strength *
            Confidence);

    public IReadOnlyList<string> Validate()
    {
        var errors =
            new List<string>();

        if (string.IsNullOrWhiteSpace(Detector))
        {
            errors.Add(
                "O nome do detector é obrigatório.");
        }

        if (Family == DetectorFamily.Unknown)
        {
            errors.Add(
                "A família da evidência não foi informada.");
        }

        ValidateRange(
            Strength,
            nameof(Strength),
            errors);

        ValidateRange(
            Confidence,
            nameof(Confidence),
            errors);

        ValidateRange(
            Severity,
            nameof(Severity),
            errors);

        ValidateRange(
            BaseWeight,
            nameof(BaseWeight),
            errors);

        if (!Applicable)
        {
            if (PositiveVote)
            {
                errors.Add(
                    "Uma evidência não aplicável não pode possuir voto positivo.");
            }

            if (Strength != 0 ||
                Confidence != 0 ||
                Severity != 0)
            {
                errors.Add(
                    "Uma evidência não aplicável deve possuir força, " +
                    "confiança e severidade iguais a zero.");
            }

            if (string.IsNullOrWhiteSpace(
                    NotApplicableReason))
            {
                errors.Add(
                    "O motivo de não aplicabilidade é obrigatório.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(
                     NotApplicableReason))
        {
            errors.Add(
                "Uma evidência aplicável não deve possuir " +
                "NotApplicableReason.");
        }

        return errors;
    }

    private static void ValidateRange(
        double value,
        string fieldName,
        ICollection<string> errors)
    {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value > 1)
        {
            errors.Add(
                $"{fieldName} deve estar entre 0 e 1.");
        }
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
