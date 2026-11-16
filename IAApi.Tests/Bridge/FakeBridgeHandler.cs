using System.Net;

namespace IAApi.Tests.Bridge;

/// <summary>
/// Handler HTTP falso para os serviços do Bridge. Nenhuma requisição sai do
/// processo: cada chamada é contada e registrada (método, URL, corpo e
/// Content-Type), e a resposta vem da função do teste.
/// Origem: os <c>BridgeHandler</c> privados de <c>PerformanceDiagnosticsTests</c>
/// e <c>PredictionHistoryReuseTests</c>, unidos aqui na tarefa 22 (P28).
/// </summary>
internal sealed class FakeBridgeHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    private readonly object _lock = new();
    private readonly List<FakeBridgeRequest> _requests = [];

    public int Calls
    {
        get { lock (_lock) return _requests.Count; }
    }

    public IReadOnlyList<FakeBridgeRequest> Requests
    {
        get { lock (_lock) return _requests.ToList(); }
    }

    /// <summary>Responde sempre o mesmo corpo, com o status informado.</summary>
    public static FakeBridgeHandler Json(
        string json,
        HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json)
        });

    /// <summary>
    /// Falha o teste se for chamado. Conferir também <see cref="Calls"/> = 0
    /// no fim, porque o código chamador pode capturar a exceção.
    /// </summary>
    public static FakeBridgeHandler FailIfCalled()
        => new(request => throw new Xunit.Sdk.XunitException(
            $"O Bridge não deveria ser chamado neste teste: {request.Method} {request.RequestUri}"));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Leitura síncrona: o conteúdo enviado pelos serviços já está em memória.
        // A resposta (ou a exceção) da função sai de forma síncrona, como nos
        // handlers originais.
        var body = request.Content?
            .ReadAsStringAsync(cancellationToken)
            .GetAwaiter()
            .GetResult();

        lock (_lock)
        {
            _requests.Add(new FakeBridgeRequest(
                request.Method,
                request.RequestUri!,
                body,
                request.Content?.Headers.ContentType?.ToString()));
        }

        return Task.FromResult(responder(request));
    }
}

internal sealed record FakeBridgeRequest(
    HttpMethod Method,
    Uri Uri,
    string? Body,
    string? ContentType);
