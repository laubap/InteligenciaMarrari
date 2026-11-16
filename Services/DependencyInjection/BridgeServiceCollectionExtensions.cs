using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.DependencyInjection;

public static class BridgeServiceCollectionExtensions
{
    /// <summary>
    /// Endereço do Bridge quando <c>Bridge:BaseUrl</c> não está configurada.
    /// Único lugar do código de produção com esse endereço (tarefa 22, P28).
    /// </summary>
    public const string BaseUrlPadrao = "http://localhost:5001";

    /// <summary>
    /// Timeout do valor atual: o mesmo de antes da tarefa 22 (60 s).
    /// </summary>
    public static readonly TimeSpan TimeoutValorAtual = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Timeout da lista de tags: os 100 s do padrão do .NET, que já valiam
    /// antes da tarefa 22, agora escritos explicitamente.
    /// </summary>
    public static readonly TimeSpan TimeoutTags = TimeSpan.FromSeconds(100);

    public static IServiceCollection AddBridgeServices(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IHistoricoService,
            BridgeHistoricoService>();

        // Clientes tipados (Transient), no mesmo padrão do LLM: endereço pela
        // configuração e HttpClient do IHttpClientFactory. Os loggers do
        // factory são removidos para o log do monitor continuar igual
        // (sem 4 linhas extras por requisição ao Bridge).
        services
            .AddHttpClient<BridgeTagsService>(
                (serviceProvider, httpClient) =>
                    Configurar(serviceProvider, httpClient, TimeoutTags))
            .RemoveAllLoggers();

        services
            .AddHttpClient<BridgeValorAtualService>(
                (serviceProvider, httpClient) =>
                    Configurar(serviceProvider, httpClient, TimeoutValorAtual))
            .RemoveAllLoggers();

        return services;
    }

    /// <summary>
    /// Endereço base do Bridge, com uma "/" no fim. Sem ela, um endereço com
    /// caminho (ex.: http://host/api) perderia o último trecho ao juntar com
    /// o caminho relativo. Chave ausente ou vazia usa <see cref="BaseUrlPadrao"/>.
    /// </summary>
    public static Uri ObterBaseAddress(IConfiguration configuration)
    {
        var baseUrl = configuration["Bridge:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = BaseUrlPadrao;
        }

        if (!Uri.TryCreate(
                baseUrl.Trim().TrimEnd('/') + "/",
                UriKind.Absolute,
                out var baseAddress)
            || (baseAddress.Scheme != Uri.UriSchemeHttp
                && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            // O valor não entra na mensagem.
            throw new InvalidOperationException(
                "A configuração Bridge:BaseUrl não é um endereço http(s) válido.");
        }

        return baseAddress;
    }

    private static void Configurar(
        IServiceProvider serviceProvider,
        HttpClient httpClient,
        TimeSpan timeout)
    {
        httpClient.BaseAddress = ObterBaseAddress(
            serviceProvider.GetRequiredService<IConfiguration>());

        httpClient.Timeout = timeout;
    }
}
