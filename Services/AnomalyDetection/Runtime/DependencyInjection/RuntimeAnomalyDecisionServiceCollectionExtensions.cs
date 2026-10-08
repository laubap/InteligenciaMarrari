using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.AnomalyDetection.Runtime.DependencyInjection;

public static class RuntimeAnomalyDecisionServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeAnomalyDecisionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RuntimeAnomalyDecisionOptions>(
            configuration.GetSection(
                RuntimeAnomalyDecisionOptions.SectionName));

        services.AddSingleton<
            RuntimeAnomalyDecisionResolver>();

        services.AddSingleton<
            RuntimeFunctionalDecisionCandidateFactory>();

        services.AddSingleton<
            RuntimeFunctionalDecisionStateStore>();

        services.AddSingleton<
            RuntimeFunctionalDecisionContextProvider>();

        return services;
    }
}