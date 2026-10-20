namespace IAApi.Application.Llm.Contracts;

public interface ILlmProvider
{
    string Nome { get; }

    Task<LlmResponse> GerarAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default
    );
}