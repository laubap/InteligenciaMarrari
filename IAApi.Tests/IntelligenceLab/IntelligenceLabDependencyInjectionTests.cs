using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IAApi.Tests.IntelligenceLab;

public sealed class IntelligenceLabDependencyInjectionTests
{
    [Fact]
    public void ConfiguracaoAusente_NaoRegistraColeta()
    {
        var services = new ServiceCollection();
        services.AddIntelligenceLabObservability(Config());
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IIntelligenceLabDiagnosticCollector>());
        Assert.Null(provider.GetService<BoundedDiagnosticBuffer>());
        Assert.Null(provider.GetService<DiagnosticVersionMetadata>());
    }

    [Fact]
    public void Desabilitada_ComMetadadosIncompletos_NaoFalhaNemRegistraColeta()
    {
        var services = new ServiceCollection();
        services.AddIntelligenceLabObservability(Config(
            ("IntelligenceLab:Enabled", "false"),
            ("IntelligenceLab:Version:ApiVersion", "")));
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IIntelligenceLabDiagnosticCollector>());
    }

    [Fact]
    public void Habilitada_RegistraBufferEColetorCompartilhadosComMetadadosExplicitos()
    {
        var services = new ServiceCollection();
        services.AddIntelligenceLabObservability(EnabledConfig());
        using var provider = services.BuildServiceProvider();

        var buffer = provider.GetRequiredService<BoundedDiagnosticBuffer>();
        Assert.True(buffer.Enabled);
        Assert.Equal(12, buffer.Capacity);
        Assert.Same(buffer, provider.GetRequiredService<BoundedDiagnosticBuffer>());
        var collector = provider.GetRequiredService<IIntelligenceLabDiagnosticCollector>();
        Assert.IsType<InMemoryDiagnosticCollector>(collector);
        Assert.Same(collector, provider.GetRequiredService<IIntelligenceLabDiagnosticCollector>());
        var version = provider.GetRequiredService<DiagnosticVersionMetadata>();
        Assert.Equal("1.2.3", version.ApiVersion);
        Assert.Equal("build-456", version.BuildIdentifier);
        Assert.Equal("abc123", version.CommitIdentifier);
        Assert.Equal("config-v1", version.AlgorithmConfigurationVersion);
        Assert.NotNull(collector.Begin(DiagnosticContext.CreateNew(), version));
    }

    [Theory]
    [InlineData("IntelligenceLab:Version:ApiVersion")]
    [InlineData("IntelligenceLab:Version:BuildIdentifier")]
    [InlineData("IntelligenceLab:Version:CommitIdentifier")]
    [InlineData("IntelligenceLab:Version:AlgorithmConfigurationVersion")]
    public void Habilitada_SemUmaVersao_RecusaInicializacao(string missingKey)
    {
        var values = EnabledEntries().Where(x => x.Key != missingKey).ToArray();
        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddIntelligenceLabObservability(Config(values)));
        Assert.Contains(missingKey, error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10001")]
    public void Habilitada_ComCapacidadeInvalida_RecusaInicializacao(string capacity)
    {
        var entries = EnabledEntries()
            .Where(x => x.Key != "IntelligenceLab:BufferCapacity")
            .Append(new KeyValuePair<string, string?>("IntelligenceLab:BufferCapacity", capacity));
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddIntelligenceLabObservability(Config(entries.ToArray())));
    }

    [Fact]
    public void RegistroDaAplicacaoIntegraObservabilidadeHabilitada()
    {
        var services = new ServiceCollection();
        services.AddIAApiApplicationServices(
            EnabledConfig(), useBenchmarkMultivariateGroups: false);

        Assert.Contains(services, entry => entry.ServiceType == typeof(PredictionService));
        Assert.Contains(services, entry => entry.ServiceType == typeof(BoundedDiagnosticBuffer));
        Assert.Contains(services, entry => entry.ServiceType == typeof(IIntelligenceLabDiagnosticCollector));
        Assert.Contains(services, entry => entry.ServiceType == typeof(DiagnosticVersionMetadata));
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values)
        => Config(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)).ToArray());

    private static IConfiguration Config(KeyValuePair<string, string?>[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IConfiguration EnabledConfig() => Config(EnabledEntries());

    private static KeyValuePair<string, string?>[] EnabledEntries() =>
    [
        new("IntelligenceLab:Enabled", "true"),
        new("IntelligenceLab:BufferCapacity", "12"),
        new("IntelligenceLab:Version:ApiVersion", "1.2.3"),
        new("IntelligenceLab:Version:BuildIdentifier", "build-456"),
        new("IntelligenceLab:Version:CommitIdentifier", "abc123"),
        new("IntelligenceLab:Version:AlgorithmConfigurationVersion", "config-v1")
    ];
}
