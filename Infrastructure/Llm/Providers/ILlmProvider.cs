using IAApi.Infrastructure.Llm.Models;

namespace IAApi.Infrastructure.Llm.Providers;

public interface ILlmProvider
{
    string Nome { get; }

    Task<LlmResponse> GerarAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default
    );
}