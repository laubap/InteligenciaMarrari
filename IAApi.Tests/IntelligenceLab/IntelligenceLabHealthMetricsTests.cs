using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IAApi.Tests.IntelligenceLab;

public sealed class IntelligenceLabHealthMetricsTests
{
    [Fact]
    public void BufferDesligado_RegistraTentativaIgnoradaSemArmazenamento()
    {
        var buffer = new BoundedDiagnosticBuffer();
        Assert.False(buffer.TryAdd(Diagnostic("TAG1")));

        var health = buffer.Health.Snapshot(buffer);
        Assert.False(health.Enabled);
        Assert.Equal(100, health.BufferCapacity);
        Assert.Equal(0, health.BufferedCount);
        Assert.Equal(0, health.StoredTotal);
        Assert.Equal(1, health.SkippedDisabled);
        Assert.Equal(0, health.RejectedTotal);
    }

    [Fact]
    public void GravacoesEEvicoes_DistinguemTotalCapturadoDoRetido()
    {
        var buffer = new BoundedDiagnosticBuffer(2, enabled: true);
        Assert.True(buffer.TryAdd(Diagnostic("TAG1")));
        Assert.True(buffer.TryAdd(Diagnostic("TAG2")));
        Assert.True(buffer.TryAdd(Diagnostic("TAG3")));

        var health = buffer.Health.Snapshot(buffer);
        Assert.True(health.Enabled);
        Assert.Equal(2, health.BufferCapacity);
        Assert.Equal(2, health.BufferedCount);
        Assert.Equal(3, health.StoredTotal);
        Assert.Equal(1, health.EvictedTotal);
        Assert.Equal(0, health.CaptureFailures);
    }

    [Fact]
    public void DiagnosticoNuloEIdentidadeIncorreta_SaoRejeitados()
    {
        var buffer = new BoundedDiagnosticBuffer(enabled: true);
        Assert.False(buffer.TryAdd(null));
        var first = Diagnostic("TAG1");
        var session = new InMemoryDiagnosticCollector(buffer).Begin(first.Context, first.Version);
        var second = Diagnostic("TAG2");
        Assert.Same(second, session.Complete(second));
        Assert.Same(first, session.Complete(first));

        var health = buffer.Health.Snapshot(buffer);
        Assert.Equal(2, health.RejectedTotal);
        Assert.Equal(1, health.StoredTotal);
        Assert.Equal(1, health.BufferedCount);
    }

    [Fact]
    public void ExcessoDeEtapas_ContaDescarteSemPerderDiagnostico()
    {
        var buffer = new BoundedDiagnosticBuffer(enabled: true);
        var diagnostic = Diagnostic("TAG1");
        var session = new InMemoryDiagnosticCollector(buffer).Begin(diagnostic.Context, diagnostic.Version);
        var observation = new DiagnosticStageObservation(
            DiagnosticStage.RequestReceived, DateTimeOffset.UtcNow, TimeSpan.Zero);

        for (var i = 0; i < 35; i++) session.RecordStage(observation);
        session.Complete(diagnostic);
        var health = buffer.Health.Snapshot(buffer);

        Assert.Equal(3, health.DiscardedStages);
        Assert.Equal(1, health.StoredTotal);
    }

    [Fact]
    public void EscritasConcorrentes_MantemContagensEBufferLimitado()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 10, enabled: true);
        Parallel.For(0, 200, i => Assert.True(buffer.TryAdd(Diagnostic($"TAG{i}"))));

        var health = buffer.Health.Snapshot(buffer);
        Assert.Equal(200, health.StoredTotal);
        Assert.Equal(190, health.EvictedTotal);
        Assert.Equal(10, health.BufferedCount);
    }

    [Fact]
    public void Snapshot_RecusaBufferDeOutraInstancia()
    {
        var first = new BoundedDiagnosticBuffer(enabled: true);
        var second = new BoundedDiagnosticBuffer(enabled: true);
        Assert.Throws<ArgumentException>(() => first.Health.Snapshot(second));
    }

    [Fact]
    public void RegistroDiHabilitado_CompartilhaMonitorComBufferEColetor()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["IntelligenceLab:Enabled"] = "true",
                ["IntelligenceLab:BufferCapacity"] = "2",
                ["IntelligenceLab:Version:ApiVersion"] = "v1",
                ["IntelligenceLab:Version:BuildIdentifier"] = "build",
                ["IntelligenceLab:Version:CommitIdentifier"] = "commit",
                ["IntelligenceLab:Version:AlgorithmConfigurationVersion"] = "config"
            }).Build();
        var services = new ServiceCollection();
        services.AddIntelligenceLabObservability(config);
        using var provider = services.BuildServiceProvider();

        var buffer = provider.GetRequiredService<BoundedDiagnosticBuffer>();
        var health = provider.GetRequiredService<IntelligenceLabHealthMetrics>();
        Assert.Same(buffer.Health, health);
        var diagnostic = Diagnostic("TAG1");
        provider.GetRequiredService<IIntelligenceLabDiagnosticCollector>()
            .Begin(diagnostic.Context, diagnostic.Version).Complete(diagnostic);
        Assert.Equal(1, health.Snapshot(buffer).StoredTotal);
    }

    [Fact]
    public void RegistroDiDesligado_NaoRegistraContadores()
    {
        var services = new ServiceCollection();
        services.AddIntelligenceLabObservability(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IntelligenceLabHealthMetrics>());
        Assert.Null(provider.GetService<BoundedDiagnosticBuffer>());
    }

    private static IntelligenceLabReadingDiagnostic Diagnostic(string tag)
    {
        var context = DiagnosticContext.CreateNew();
        var version = new DiagnosticVersionMetadata("api", "build", "commit", "config");
        return new IntelligenceLabReadingDiagnostic(
            context, version, "PK7", tag, DateTimeOffset.UtcNow, 100,
            new DiagnosticCompleteness(
                DiagnosticCompletenessStatus.Partial,
                reason: "Teste de contadores sem analise operacional."),
            new DiagnosticTiming(TimeSpan.Zero));
    }
}
