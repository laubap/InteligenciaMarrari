using Microsoft.Extensions.DependencyInjection;

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
}