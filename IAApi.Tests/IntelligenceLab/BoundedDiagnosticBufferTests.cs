using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;

namespace IAApi.Tests.IntelligenceLab;

public sealed class BoundedDiagnosticBufferTests
{
    [Fact]
    public void DisabledByDefault_DoesNotStoreDiagnostics()
    {
        var buffer = new BoundedDiagnosticBuffer();
        var diagnostic = CreateDiagnostic("PK7.TAG1");

        Assert.False(buffer.Enabled);
        Assert.False(buffer.TryAdd(diagnostic));
        Assert.Empty(buffer.Snapshot());
        Assert.Equal(0, buffer.EvictedCount);
    }

    [Fact]
    public void Enabled_PreservesOriginalDiagnosticWithoutChangingDecision()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 2, enabled: true);
        var diagnostic = CreateDiagnostic("PK7.TAG1");

        Assert.True(buffer.TryAdd(diagnostic));
        Assert.Same(diagnostic, Assert.Single(buffer.Snapshot()));
        Assert.Equal(diagnostic.Completeness, buffer.Snapshot()[0].Completeness);
    }

    [Fact]
    public void FullBuffer_EvictsOldestAndNeverExceedsCapacity()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 2, enabled: true);
        var oldest = CreateDiagnostic("PK7.TAG1");
        var second = CreateDiagnostic("PK7.TAG2");
        var newest = CreateDiagnostic("PK7.TAG3");

        Assert.True(buffer.TryAdd(oldest));
        Assert.True(buffer.TryAdd(second));
        Assert.True(buffer.TryAdd(newest));

        var snapshot = buffer.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Same(second, snapshot[0]);
        Assert.Same(newest, snapshot[1]);
        Assert.Equal(1, buffer.EvictedCount);
    }

    [Fact]
    public void Snapshot_DoesNotChangeWhenNewDiagnosticsArrive()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 1, enabled: true);
        var first = CreateDiagnostic("PK7.TAG1");
        buffer.TryAdd(first);
        var oldSnapshot = buffer.Snapshot();

        buffer.TryAdd(CreateDiagnostic("PK7.TAG2"));

        Assert.Same(first, Assert.Single(oldSnapshot));
        Assert.Equal("PK7.TAG2", Assert.Single(buffer.Snapshot()).TagName);
    }

    [Fact]
    public void ConcurrentWriters_KeepCountBounded()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 16, enabled: true);

        Parallel.For(0, 100, i =>
        {
            Assert.True(buffer.TryAdd(CreateDiagnostic($"PK7.TAG{i}")));
        });

        Assert.Equal(16, buffer.Count);
        Assert.Equal(84, buffer.EvictedCount);
        Assert.Equal(16, buffer.Snapshot().Count);
    }

    [Fact]
    public void InvalidCapacityAndNullEntries_AreRejectedSafely()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BoundedDiagnosticBuffer(capacity: 0));

        var buffer = new BoundedDiagnosticBuffer(capacity: 2, enabled: true);
        Assert.False(buffer.TryAdd(null));
        Assert.Empty(buffer.Snapshot());
    }

    private static IntelligenceLabReadingDiagnostic CreateDiagnostic(string tagName)
    {
        var context = DiagnosticContext.CreateNew();
        var version = new DiagnosticVersionMetadata(
            "api-test", "build-test", "commit-test", "config-test");

        return new IntelligenceLabReadingDiagnostic(
            context,
            version,
            "PK7",
            tagName,
            DateTimeOffset.UtcNow,
            100,
            new DiagnosticCompleteness(
                DiagnosticCompletenessStatus.Partial,
                reason: "Teste de armazenamento: nao inclui as secoes de inferencia."),
            new DiagnosticTiming(TimeSpan.Zero));
    }
}
