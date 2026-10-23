namespace IAApi.IntelligenceLab.Observability;

/// <summary>
/// Origem declarada pela futura instrumentação para uma métrica já produzida.
/// Este contrato não infere a origem; a coleta precisará obtê-la do pipeline real.
/// </summary>
public enum DiagnosticCalculationOrigin
{
    Reading = 1,
    HistoricalBaseline = 2,
    HistoryAnalysis = 3,
    DetectorCalculation = 4,
    ModelInference = 5,
    OperationalContext = 6
}

/// <summary>
/// Metadados descritivos de uma métrica capturada no DetectorDiagnostic.Metrics.
/// MetricKey aponta para o valor original, sem duplicá-lo ou recalculá-lo.
/// FormulaDescription é explicativa, não executável nem validada matematicamente.
/// </summary>
public sealed record DiagnosticCalculationReference
{
    public DiagnosticCalculationReference(
        string metricKey,
        string label,
        string meaning,
        DiagnosticCalculationOrigin origin,
        string? unit = null,
        string? formulaDescription = null)
    {
        MetricKey = DiagnosticContractGuard.Required(metricKey, nameof(metricKey));
        Label = DiagnosticContractGuard.Required(label, nameof(label));
        Meaning = DiagnosticContractGuard.Required(meaning, nameof(meaning));

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        Origin = origin;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        FormulaDescription = string.IsNullOrWhiteSpace(formulaDescription)
            ? null
            : formulaDescription.Trim();
    }

    public string MetricKey { get; }
    public string Label { get; }
    public string Meaning { get; }
    public DiagnosticCalculationOrigin Origin { get; }
    public string? Unit { get; }
    public string? FormulaDescription { get; }
}
