using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAApi.Infrastructure.Llm.Models;

internal sealed class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<LlmChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("format")]
    public JsonElement? Format { get; set; }

    [JsonPropertyName("options")]
    public OllamaOptions Options { get; set; } = new();

    [JsonPropertyName("keep_alive")]
    public string KeepAlive { get; set; } = "5m";
}

internal sealed class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.1;

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 900;
}