using IAApi.Services.AnomalyDetection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.DependencyInjection;

public static class LegacyAnomalyDetectionServiceCollectionExtensions
{
    /// <summary>
    /// Detectores legados mais os de evidência independente (BACKLOG 33)
    /// cuja chave estiver ligada na configuração. A chave é lida na
    /// inicialização: com ela desligada (padrão), o detector nem é
    /// registrado e o ensemble fica exatamente como antes. Mudar a chave
    /// exige reiniciar a API.
    /// </summary>
    public static IServiceCollection AddLegacyAnomalyDetectionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddLegacyAnomalyDetectionServices();

        var valorCongelado =
            configuration
                .GetSection(ValorCongeladoOptions.SectionName)
                .Get<ValorCongeladoOptions>();

        if (valorCongelado?.Habilitado == true)
        {
            // Configuração inválida derruba a inicialização, e não cada
            // avaliação de tag.
            services.AddOptions<ValorCongeladoOptions>()
                .ValidateOnStart();

            services.AddScoped<IAnomalyDetector>(provider =>
                new ValorCongeladoDetector(
                    provider
                        .GetRequiredService<IOptions<ValorCongeladoOptions>>()
                        .Value,
                    provider
                        .GetRequiredService<TagBehaviorProfileResolver>()));
        }

        return services;
    }

    public static IServiceCollection AddLegacyAnomalyDetectionServices(
        this IServiceCollection services)
    {
services.AddScoped<IAnomalyDetector>(provider =>
    new StatisticalDetector(
        provider
            .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
            .Value));

services.AddScoped<IAnomalyDetector>(provider =>
    new MadDetector(
        provider
            .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
            .Value));

        services.AddScoped<IAnomalyDetector>(provider =>
            new EwmaDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

        services.AddScoped<IAnomalyDetector>(provider =>
            new RateOfChangeDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

        services.AddScoped<IAnomalyDetector>(provider =>
            new PersistenceDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

              services.AddScoped<IAnomalyDetector>(provider =>
            new LevelShiftDetector(
                provider
                    .GetRequiredService<IOptions<AnomalyDetectionOptions>>()
                    .Value));

        services.AddScoped<
            EnsembleDetectionService>();

        return services;
    }
}