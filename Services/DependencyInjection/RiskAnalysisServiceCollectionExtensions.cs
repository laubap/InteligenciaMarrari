using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.DependencyInjection;

public static class RiskAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddRiskAnalysisServices(
        this IServiceCollection services)
    {
        services.AddScoped<RiskScoreService>();
        services.AddScoped<ExplicacaoRiscoService>();
        services.AddScoped<TendenciaRiscoService>();
        services.AddScoped<TendenciaValorService>();

        return services;
    }
}