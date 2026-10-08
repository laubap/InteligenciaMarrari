using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace IAApi.Services.AnomalyDetection.Multivariate.DependencyInjection;

public static class MultivariateServiceCollectionExtensions
{
    public static IServiceCollection AddOperationalMultivariateCoreServices(
        this IServiceCollection services)
    {
        services.AddScoped<GrupoAnaliseService>();
        services.AddScoped<OperationalMultivariateTrainingService>();
        services.AddScoped<GrupoAnaliseCadastroService>();
        services.AddScoped<MultivariateGroupBufferService>();

        services.AddSingleton<MultivariateRuntimeStateStore>();
        services.AddSingleton<MultivariateModelPersistenceService>();

        return services;
    }
    public static IServiceCollection AddOperationalMultivariateGovernanceServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddSingleton<MultivariateHistoricalBacktestAnalyzer>();
    services.AddScoped<OperationalMultivariateBacktestService>();

    services.AddSingleton<MultivariateOperationalQualificationEvaluator>();
    services.AddScoped<OperationalMultivariateQualificationService>();

    services.AddScoped<MultivariateIndustrialEventReviewStore>();
    services.AddSingleton<MultivariateIndustrialReviewAssessmentService>();
    services.AddSingleton<MultivariateIndustrialReviewQueueBuilder>();

    services.AddScoped<
        IMultivariateIndustrialReviewQueueService,
        MultivariateIndustrialReviewQueueService>();

    services.Configure<MultivariateOperationalReadinessOptions>(
        configuration.GetSection(
            "AnomalyDetection:MultivariateOperationalReadiness"));

    services.AddSingleton<MultivariateOperationalReadinessEvaluator>();
    services.AddScoped<MultivariateOperationalReadinessService>();

    return services;
}
}