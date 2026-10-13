using Microsoft.Extensions.DependencyInjection;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Orchestration;

namespace IAApi.Services.DependencyInjection;

public static class AnomalyContextAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddAnomalyContextAnalysisServices(
        this IServiceCollection services)
    {
    services.AddScoped<AnaliseRelacionadasService>();
    services.AddScoped<AnomaliaHistoricoService>();
    services.AddScoped<InterpretacaoDependenciasService>();
    services.AddScoped<FeedbackAnomaliaAnaliseService>();
    services.AddScoped<PredictionDependencyMapper>();
    services.AddScoped<PredictionResponseBuilder>();
    services.AddScoped<PredictionPostDecisionAnalysisService>();
    services.AddScoped<PredictionFinalizationService>();

        return services;
    }
}