namespace IAApi.Infrastructure.Llm.Configuration;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public bool Habilitado { get; set; } = true;

    public string Provider { get; set; } = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Modelo { get; set; } = "qwen2.5:3b";

    public int TimeoutSegundos { get; set; } = 120;

    public double Temperatura { get; set; } = 0.1;

    public int MaxTokens { get; set; } = 900;

    public string KeepAlive { get; set; } = "5m";
}