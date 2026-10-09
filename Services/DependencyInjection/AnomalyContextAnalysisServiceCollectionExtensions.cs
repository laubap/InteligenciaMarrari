using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.DependencyInjection;

public static class AnomalyContextAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddAnomalyContextAnalysisServices(
        this IServiceCollection services)
    {
        services.AddScoped<AnaliseRelacionadasService>();
        services.AddScoped<AnomaliaHistoricoService>();
        services.AddScoped<InterpretacaoDependenciasService>();

        return services;
    }
}