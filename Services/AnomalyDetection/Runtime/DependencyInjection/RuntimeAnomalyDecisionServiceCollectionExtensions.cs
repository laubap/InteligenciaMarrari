using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        services.AddSingleton<RuntimeAnomalyDecisionOptions>(
    serviceProvider =>
        serviceProvider
            .GetRequiredService<IOptions<RuntimeAnomalyDecisionOptions>>()
            .Value);

        services.AddSingleton<
            RuntimeAnomalyDecisionResolver>();

        services.AddSingleton<
            RuntimeFunctionalDecisionCandidateFactory>();

        services.AddSingleton<
            RuntimeFunctionalDecisionStateStore>();

        services.AddSingleton<
            RuntimeFunctionalDecisionContextProvider>();

        services.AddSingleton<
            PredictionRuntimeDecisionService>();

        return services;
    }
}