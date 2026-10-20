using System.Text.Json.Serialization;

namespace IAApi.Infrastructure.Llm.Models;

internal sealed class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("message")]
    public OllamaChatResponseMessage Message { get; set; } = new();

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}

internal sealed class OllamaChatResponseMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}