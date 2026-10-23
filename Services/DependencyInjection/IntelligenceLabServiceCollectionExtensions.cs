using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.DependencyInjection;

/// <summary>
/// Ativacao explicita da coleta local de diagnosticos. Sem endpoints, disco ou
/// alteracoes nos algoritmos. Com Enabled=false, nao registra sequer o coletor.
/// </summary>
public static class IntelligenceLabServiceCollectionExtensions
{
    private const int DefaultBufferCapacity = 100;
    private const int MaximumBufferCapacity = 10_000;

    public static IServiceCollection AddIntelligenceLabObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("IntelligenceLab");
        if (!section.GetValue<bool>("Enabled"))
        {
            return services;
        }

        var capacity = section.GetValue<int?>("BufferCapacity") ?? DefaultBufferCapacity;
        if (capacity is < 1 or > MaximumBufferCapacity)
        {
            throw new InvalidOperationException(
                $"IntelligenceLab:BufferCapacity deve estar entre 1 e {MaximumBufferCapacity}.");
        }

        // Nunca atribuir versoes ou commits ficticios a diagnosticos industriais.
        // Configuracao incompleta e erro de inicializacao, nao falha silenciosa.
        var versions = section.GetSection("Version");
        var apiVersion = Required(versions, "ApiVersion");
        var buildIdentifier = Required(versions, "BuildIdentifier");
        var commitIdentifier = Required(versions, "CommitIdentifier");
        var algorithmVersion = Required(versions, "AlgorithmConfigurationVersion");

        var metadata = new DiagnosticVersionMetadata(
            apiVersion, buildIdentifier, commitIdentifier, algorithmVersion);

        services.AddSingleton(new BoundedDiagnosticBuffer(capacity, enabled: true));
        services.AddSingleton<IIntelligenceLabDiagnosticCollector, InMemoryDiagnosticCollector>();
        services.AddSingleton(metadata);

        return services;
    }

    private static string Required(IConfiguration section, string name)
    {
        var value = section[name];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"IntelligenceLab:Version:{name} deve ser informado quando Enabled=true.");
        }

        return value.Trim();
    }
}
