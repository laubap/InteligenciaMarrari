using IAApi.Services.AnomalyDetection;
using IAApi.Services.ModelTraining;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.DependencyInjection;

public static class IsolationForestServiceCollectionExtensions
{
    public static IServiceCollection AddIsolationForestServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<
            IValidateOptions<IsolationForestOptions>,
            IsolationForestOptionsValidator>();

        services.Configure<IsolationForestOptions>(
            configuration.GetSection(
                "AnomalyDetection:IsolationForest"));

        services.AddScoped<
            IsolationForestFeatureExtractor>();

        services.AddScoped<
            IAnomalyDetector,
            IsolationForestDetector>();

        services.AddSingleton<
            IsolationForestPersistenceService>();

        services.AddScoped<
            IsolationForestTrainingService>();

        return services;
    }
}