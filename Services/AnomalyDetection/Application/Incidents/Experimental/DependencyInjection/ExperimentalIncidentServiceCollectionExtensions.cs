using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Api;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.ReadModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.DependencyInjection;

public static class ExperimentalIncidentServiceCollectionExtensions
{
    public static IServiceCollection AddExperimentalIncidentApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ExperimentalIncidentApiOptions>(
            configuration.GetSection(
                ExperimentalIncidentApiOptions.SectionName));

        services.AddSingleton<
            IIncidentReadModelStore,
            InMemoryIncidentReadModelStore>();

        services.AddSingleton<
            IncidentReadModelSnapshotLoader>();

        services.AddSingleton<
            IExperimentalIncidentQueryService,
            ExperimentalIncidentQueryService>();

        return services;
    }
}