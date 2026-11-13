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

        // RC-7: lista vazia se a seção não existir (padrão).
        services.Configure<OperationalRegimeForbiddenTagsOptions>(
            configuration.GetSection(
                OperationalRegimeForbiddenTagsOptions.SectionName));

        // RC-4: chave do regime combinado; Habilitado = false se a seção
        // não existir (padrão). Lida pelo OperationalRegimeBaselineProvider (RC-8).
        services.Configure<RegimeCombinadoOptions>(
            configuration.GetSection(
                RegimeCombinadoOptions.SectionName));

        // P31: recorte causal do histórico; Habilitado = false se a seção
        // não existir (padrão).
        services.Configure<HistoricoCausalOptions>(
            configuration.GetSection(
                HistoricoCausalOptions.SectionName));

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