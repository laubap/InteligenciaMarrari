using IAApi.Infrastructure.Llm.Configuration;
using IAApi.Infrastructure.Llm.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IAApi.Infrastructure.Llm.DependencyInjection;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLmStudioProvider(
        this IServiceCollection services)
    {
        services.AddHttpClient<
            ILlmProvider,
            LmStudioLlmProvider>(
            (serviceProvider, httpClient) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<LlmOptions>>()
                    .Value;

                if (string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    throw new InvalidOperationException(
                        "A configuração Llm:BaseUrl não foi informada.");
                }

                httpClient.BaseAddress =
                    new Uri(options.BaseUrl);

                httpClient.Timeout =
                    TimeSpan.FromSeconds(
                        options.TimeoutSegundos);
            });

        return services;
    }
}