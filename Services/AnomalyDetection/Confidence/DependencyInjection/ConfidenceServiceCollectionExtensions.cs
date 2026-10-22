using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Confidence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.Confidence.DependencyInjection;

public static class ConfidenceServiceCollectionExtensions
{
    public static IServiceCollection AddConfidenceEngineServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConfidenceEngineOptions>(
            configuration.GetSection("ConfidenceEngine"));

        services.AddSingleton<ConfidenceEngine>(provider =>
            new ConfidenceEngine(
                provider
                    .GetRequiredService<IOptions<ConfidenceEngineOptions>>()
                    .Value,
                provider.GetRequiredService<EvidenceBuilder>()));
        services.AddSingleton<LegacyDetectorResultV2Adapter>();
        services.AddSingleton<EvidenceBuilder>();

        return services;
    }
}