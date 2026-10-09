using Microsoft.Extensions.DependencyInjection;
using IAApi.Services.AnomalyDetection.Orchestration;

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
        services.AddScoped<PredictionRiskAnalysisService>();

        return services;
    }
}