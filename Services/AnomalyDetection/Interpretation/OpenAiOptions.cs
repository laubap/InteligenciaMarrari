namespace IAApi.Services.AnomalyDetection.Interpretation;

public class OpenAiOptions
{
    public bool Habilitado { get; set; } = false;

    public string Modelo { get; set; } = "gpt-5-mini";
}