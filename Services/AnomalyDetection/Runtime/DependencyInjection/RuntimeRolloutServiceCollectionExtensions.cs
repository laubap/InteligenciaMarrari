using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.AnomalyDetection.Runtime.DependencyInjection;

public static class RuntimeRolloutServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeRolloutCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<
            RuntimeDecisionMetrics>();

        services.Configure<
            RuntimeDecisionMetricsEndpointOptions>(
            configuration.GetSection(
                RuntimeDecisionMetricsEndpointOptions.SectionName));

        services.Configure<
            RuntimeRolloutReadinessOptions>(
            configuration.GetSection(
                RuntimeRolloutReadinessOptions.SectionName));

        services.AddSingleton<
            RuntimeRolloutReadinessEvaluator>();

        services.AddSingleton<
            RuntimeRolloutSnapshotFactory>();

        services.AddScoped<
            RuntimeRolloutSnapshotService>();

        return services;
    }
}