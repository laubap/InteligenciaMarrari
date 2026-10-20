using IAApi.Configuration.ExperimentalIncidents;
using IAApi.Infrastructure.Incidents.Experimental.ReadModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IAApi.Application.Incidents.Experimental.Queries;
using Microsoft.Extensions.Options;
using IAApi.Application.Incidents.Experimental.ReadModels;

namespace IAApi.DependencyInjection.ExperimentalIncidents;

public static class ExperimentalIncidentServiceCollectionExtensions
{
    public static IServiceCollection AddExperimentalIncidentApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ExperimentalIncidentApiOptions>(
            configuration.GetSection(
                ExperimentalIncidentApiOptions.SectionName));

        services.AddSingleton<ExperimentalIncidentQueryOptions>(
    serviceProvider =>
    {
        var apiOptions = serviceProvider
            .GetRequiredService<
                IOptions<ExperimentalIncidentApiOptions>>()
            .Value;

        return new ExperimentalIncidentQueryOptions
        {
            DefaultPageSize =
                apiOptions.DefaultPageSize,

            MaximumPageSize =
                apiOptions.MaximumPageSize
        };
    });

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