using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.DependencyInjection;

public static class BridgeServiceCollectionExtensions
{
    public static IServiceCollection AddBridgeServices(
        this IServiceCollection services)
    {
        services.AddSingleton<
            IHistoricoService,
            BridgeHistoricoService>();

        services.AddScoped<BridgeTagsService>();
        services.AddScoped<BridgeValorAtualService>();

        return services;
    }
}