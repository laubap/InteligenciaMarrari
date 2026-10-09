using IAApi.Services.AnomalyDetection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.DependencyInjection;

public static class AnomalyDetectionServiceCollectionExtensions
{
    public static IServiceCollection AddAnomalyDetectionConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AnomalyDetectionOptions>(
            configuration.GetSection("AnomalyDetection"));

        return services;
    }

    public static IServiceCollection AddAnomalyDetectionValidation(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IValidateOptions<AnomalyDetectionOptions>,
            AnomalyDetectionOptionsValidator>();

        return services;
    }

    public static IServiceCollection AddAnomalyDetectionCoreServices(
    this IServiceCollection services)
{
    services.AddSingleton<
        TagBehaviorProfileResolver>();

    services.AddSingleton<
        DetectorApplicabilityPolicy>();

    return services;
}

public static IServiceCollection AddOperationalRegimeServices(
    this IServiceCollection services)
{
    services.AddScoped<
        OperationalRegimeHistoryResolver>();

    services.AddScoped<
        OperationalRegimeBaselineService>();

    services.AddScoped<
        OperationalRegimeBaselineProvider>();

    services.AddScoped<
    IAApi.Services.AnomalyDetection.Orchestration
        .PredictionDetectionPreparationService>();

    

    return services;
}


}