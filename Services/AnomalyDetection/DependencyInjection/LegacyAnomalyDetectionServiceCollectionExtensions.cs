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

services.AddScoped<IAnomalyDetector>(provider =>
    new MadDetector(
        provider
            .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
            .Value));

        services.AddScoped<IAnomalyDetector>(provider =>
            new EwmaDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

        services.AddScoped<IAnomalyDetector>(provider =>
            new RateOfChangeDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

        services.AddScoped<IAnomalyDetector>(provider =>
            new PersistenceDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

              services.AddScoped<IAnomalyDetector>(provider =>
            new LevelShiftDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

        services.AddScoped<
            EnsembleDetectionService>();

        return services;
    }
}