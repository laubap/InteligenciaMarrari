using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.DependencyInjection;

public static class TagProfileStatisticsServiceCollectionExtensions
{
    public static IServiceCollection AddTagProfileStatisticsServices(
        this IServiceCollection services)
    {
        services.AddScoped<HistoricoLimpezaService>();
        services.AddScoped<TagProfileStatisticsCalculator>();

        return services;
    }
}