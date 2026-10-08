using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.ModelTraining.DependencyInjection;

public static class ModelTrainingServiceCollectionExtensions
{
    public static IServiceCollection AddModelTrainingServices(
        this IServiceCollection services)
    {
        services.AddScoped<DadosTreinamentoTagService>();
        services.AddScoped<ModeloTagStatusService>();

        return services;
    }
}