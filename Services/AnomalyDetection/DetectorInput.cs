namespace IAApi.Services.AnomalyDetection;

public class DetectorInput
{
    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public string TipoTag { get; set; } = "";

    public double ValorAtual { get; set; }

    public DateTime DataHoraAtual { get; set; } = DateTime.Now;

    public double MediaHistorica { get; set; }

    public double DesvioPadraoHistorico { get; set; }

    public IReadOnlyList<HistoricoDetectorPoint> Historico { get; set; } =
        Array.Empty<HistoricoDetectorPoint>();
}