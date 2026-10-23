using IAApi.IntelligenceLab.Observability;

namespace IAApi.Tests.IntelligenceLab;

public sealed class DiagnosticCalculationReferenceTests
{
    private static DiagnosticCalculationReference ZScoreReference() => new(
        metricKey: "zScoreEfetivo",
        label: "Z-Score calculado",
        meaning: "Distância da leitura em relação à escala efetiva do baseline.",
        origin: DiagnosticCalculationOrigin.DetectorCalculation,
        unit: "desvios",
        formulaDescription: "|valor atual - média| / escala efetiva, fora do deadband");

    private static DetectorDiagnostic Executed(
        IReadOnlyDictionary<string, double>? metrics,
        IEnumerable<DiagnosticCalculationReference>? references) => new(
        "Z-Score",
        DetectorDiagnosticStatus.Executed,
        votedAnomaly: true,
        weight: 0.15,
        originalScore: 4,
        normalizedScore: 80,
        threshold: 3,
        justification: "Desvio observado.",
        metrics: metrics,
        thresholdScale: DetectorThresholdScale.OriginalScore,
        calculationReferences: references);

    [Fact]
    public void Reference_UsesOriginalMetricWithoutRecalculation()
    {
        var originalMetrics = new Dictionary<string, double> { ["zScoreEfetivo"] = 4 };
        var result = Executed(originalMetrics, [ZScoreReference()]);

        originalMetrics["zScoreEfetivo"] = 99;

        var reference = Assert.Single(result.CalculationReferences);
        Assert.Equal(4, result.Metrics[reference.MetricKey]);
        Assert.Equal("Z-Score calculado", reference.Label);
        Assert.Equal(DiagnosticCalculationOrigin.DetectorCalculation, reference.Origin);
        Assert.Equal("desvios", reference.Unit);
        Assert.NotNull(reference.FormulaDescription);
    }

    [Fact]
    public void Reference_RejectsAbsentMetric()
    {
        Assert.Throws<ArgumentException>(() =>
            Executed(new Dictionary<string, double>(), [ZScoreReference()]));
    }

    [Fact]
    public void Reference_RejectsDuplicateMetricKeysIgnoringCase()
    {
        var duplicate = new DiagnosticCalculationReference(
            "ZSCOREEFETIVO", "Mesmo cálculo", "Não repetir.",
            DiagnosticCalculationOrigin.DetectorCalculation);

        Assert.Throws<ArgumentException>(() =>
            Executed(new Dictionary<string, double> { ["zScoreEfetivo"] = 4 },
                [ZScoreReference(), duplicate]));
    }

    [Fact]
    public void Reference_RejectsNonExecutedDetector()
    {
        Assert.Throws<ArgumentException>(() => new DetectorDiagnostic(
            "Z-Score", DetectorDiagnosticStatus.NotApplicable,
            votedAnomaly: false, weight: 0.15,
            originalScore: null, normalizedScore: null, threshold: null,
            justification: "Sem histórico suficiente.",
            metrics: new Dictionary<string, double> { ["zScoreEfetivo"] = 4 },
            calculationReferences: [ZScoreReference()]));
    }

    [Fact]
    public void Reference_ValidatesMetadataAndDefensiveCollection()
    {
        Assert.Throws<ArgumentException>(() => new DiagnosticCalculationReference(
            " ", "Nome", "Explicação", DiagnosticCalculationOrigin.DetectorCalculation));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticCalculationReference(
            "zScoreEfetivo", "Nome", "Explicação", (DiagnosticCalculationOrigin)99));

        var mutable = new List<DiagnosticCalculationReference> { ZScoreReference() };
        var result = Executed(new Dictionary<string, double> { ["zScoreEfetivo"] = 4 }, mutable);
        mutable.Clear();

        Assert.Single(result.CalculationReferences);
        var exposed = Assert.IsAssignableFrom<IList<DiagnosticCalculationReference>>(
            result.CalculationReferences);
        Assert.Throws<NotSupportedException>(() => exposed.Clear());
    }
}
