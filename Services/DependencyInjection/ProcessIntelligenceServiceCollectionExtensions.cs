using Microsoft.Extensions.DependencyInjection;
using IAApi.Services.AnomalyDetection.Orchestration;

namespace IAApi.Services.DependencyInjection;

public static class ProcessIntelligenceServiceCollectionExtensions
{
    public static IServiceCollection AddProcessIntelligenceServices(
        this IServiceCollection services)
    {
        services.AddScoped<ProcessoAnaliseService>();
        services.AddScoped<ProcessoInteligenciaService>();
        services.AddScoped<ProcessoHealthScoreService>();
        services.AddScoped<InterpretacaoProcessoService>();
        services.AddScoped<EquipamentoInteligenciaService>();
        services.AddScoped<FailurePredictionService>();
        services.AddScoped<PredictionContextLoader>();

        return services;
    }
}