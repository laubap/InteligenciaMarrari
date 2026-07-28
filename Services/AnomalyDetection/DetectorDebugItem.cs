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
}