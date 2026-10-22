using IAApi.Services.AnomalyDetection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.DependencyInjection;

public static class LegacyAnomalyDetectionServiceCollectionExtensions
{
    public static IServiceCollection AddLegacyAnomalyDetectionServices(
        this IServiceCollection services)
    {
services.AddScoped<IAnomalyDetector>(provider =>
    new StatisticalDetector(
        provider
            .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
            .Value));

        services.AddScoped<
            IAnomalyDetector,
            MadDetector>();

        services.AddScoped<
            IAnomalyDetector,
            EwmaDetector>();

        services.AddScoped<
            IAnomalyDetector,
            RateOfChangeDetector>();

        services.AddScoped<
            IAnomalyDetector,
            PersistenceDetector>();

        services.AddScoped<
            IAnomalyDetector,
            LevelShiftDetector>();

        services.AddScoped<
            EnsembleDetectionService>();

        return services;
    }
}