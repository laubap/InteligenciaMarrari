namespace IAApi.Services.AnomalyDetection.Interpretation;

public class AnomalyInterpretationResult
{
    public string Resumo { get; set; } = "";

    public string InterpretacaoTecnica { get; set; } = "";

    public List<string> Evidencias { get; set; } = new();

    public List<string> PossiveisCausas { get; set; } = new();

    public List<string> Recomendacoes { get; set; } = new();

    public string Severidade { get; set; } = "";

    public bool GeradoPorLlm { get; set; }

    public string ModeloUtilizado { get; set; } = "";

    public DateTime DataGeracao { get; set; } =
        DateTime.UtcNow;
}