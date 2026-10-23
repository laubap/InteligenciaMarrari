using IAApi.IntelligenceLab.Observability;

namespace IAApi.Services.AnomalyDetection;

public class DetectorDebugItem
{
    public string Nome { get; set; } = "";

    public bool Aplicavel { get; set; }

    public bool VotouAnomalia { get; set; }

    public double ScoreNormalizado { get; set; }

    public double ScoreOriginal { get; set; }

    public double Peso { get; set; }

    public string Explicacao { get; set; } = "";

    public Dictionary<string, double> Metricas { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public DetectorEvidenceRole EvidenceRole { get; set; } =
        DetectorEvidenceRole.StatisticalEvidence;

    [System.Text.Json.Serialization.JsonIgnore]
    public UnderlyingEvidenceKind UnderlyingEvidenceKind { get; set; } =
        UnderlyingEvidenceKind.Unknown;

    [System.Text.Json.Serialization.JsonIgnore]
    public TemporalConfirmationSnapshot? TemporalConfirmation { get; set; }

    /// <summary>
    /// Resultado interno da tentativa de executar o detector. Não compõe o
    /// contrato HTTP legado; existe para preservar a diferença entre uma
    /// não aplicabilidade legítima e uma exceção absorvida pelo Ensemble.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DetectorDiagnosticStatus ExecutionStatus { get; set; }
}
