using System.Text.Json;

namespace IAApi.Application.Llm.Contracts;

public sealed class LlmRequest
{
    public string SystemPrompt { get; set; } = "";

    public string UserPrompt { get; set; } = "";

    public JsonElement? JsonSchema { get; set; }

    public double? Temperatura { get; set; }

    public int? MaxTokens { get; set; }
}