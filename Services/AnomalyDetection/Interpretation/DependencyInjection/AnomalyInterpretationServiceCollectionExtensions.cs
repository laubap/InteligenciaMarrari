using IAApi.Services.AnomalyDetection.Interpretation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.AnomalyDetection.Interpretation.DependencyInjection;

public static class AnomalyInterpretationServiceCollectionExtensions
{
    public static IServiceCollection AddAnomalyInterpretationServices(
        this IServiceCollection services)
    {
        services.AddScoped<
            RuleBasedAnomalyInterpretationService>();

        services.AddScoped<
            LocalLlmAnomalyInterpretationService>();

        services.AddScoped<
            IAnomalyInterpretationService>(
            serviceProvider =>
            {
                var configuration =
                    serviceProvider.GetRequiredService<
                        IConfiguration>();

                var llmHabilitado =
                    configuration.GetValue<bool>(
                        "Llm:Habilitado");

                return llmHabilitado
                    ? serviceProvider.GetRequiredService<
                        LocalLlmAnomalyInterpretationService>()
                    : serviceProvider.GetRequiredService<
                        RuleBasedAnomalyInterpretationService>();
            });

        return services;
    }
}