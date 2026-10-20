namespace IAApi.Infrastructure.Llm.Models;

public sealed class LlmChatMessage
{
    public string Role { get; set; } = "";

    public string Content { get; set; } = "";
}