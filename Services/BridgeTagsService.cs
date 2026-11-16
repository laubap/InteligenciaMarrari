// Serviço responsável por consultar as tags de um cliente no Bridge externo.
// Ele faz uma requisição HTTP GET para o endpoint /tags e retorna o JSON cru.
public class BridgeTagsService
{
    private readonly HttpClient _httpClient;

    // O HttpClient vem da DI (AddBridgeServices): endereço de Bridge:BaseUrl
    // e timeout de 100 s (o padrão do .NET de antes, agora explícito).
    public BridgeTagsService(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    // Retorna as tags do cliente informado como string JSON.
    public async Task<string> BuscarTagsCliente(string clienteId)
    {
        // Caminho relativo ao BaseAddress; clienteId sem codificação extra,
        // como antes.
        var url =
            $"tags?cliente={clienteId}";

        return await _httpClient.GetStringAsync(url);
    }
}
