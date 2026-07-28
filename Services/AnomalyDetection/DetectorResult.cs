namespace IAApi.Services.AnomalyDetection;

public class DetectorResult
{
    public bool Aplicavel { get; set; } = true;

    public bool EhAnomalia { get; set; }

    // Score compatível com as regras antigas da aplicação.
    public double Score { get; set; }

    // Score comum entre 0 e 100 utilizado pelo ensemble.
    public double ScoreNormalizado { get; set; }

    public double Peso { get; set; }

    public double LimiteInferior { get; set; }

    public double LimiteSuperior { get; set; }

    public double Media { get; set; }

    public double DesvioPadrao { get; set; }

    public string Detector { get; set; } = "";

    public string Explicacao { get; set; } = "";

    public Dictionary<string, double> Metricas { get; set; } = new();

    // Resultado individual de cada detector executado.
    public List<DetectorDebugItem> DetalhesDetectores { get; set; } = new();
}