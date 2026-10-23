using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;

namespace IAApi.Tests.IntelligenceLab;

public sealed class InMemoryDiagnosticCollectorTests
{
    [Fact]
    public void DefaultCollector_DoesNotStoreAnything()
    {
        var buffer = new BoundedDiagnosticBuffer();
        var session = new InMemoryDiagnosticCollector(buffer);
        var (context, version) = Identity();
        var diagnostic = Diagnostic(context, version, "PK7.TAG1");

        var received = session.Begin(context, version).Complete(diagnostic);

        Assert.Same(diagnostic, received);
        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void EnabledCollector_StoresExactDiagnosticWithoutChangingDecision()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 2, enabled: true);
        var collector = new InMemoryDiagnosticCollector(buffer);
        var (context, version) = Identity();
        var diagnostic = Diagnostic(context, version, "PK7.TAG1");

        var returned = collector.Begin(context, version).Complete(diagnostic);

        Assert.Same(diagnostic, returned);
        Assert.Same(diagnostic, Assert.Single(buffer.Snapshot()));
        Assert.Equal(DiagnosticCompletenessStatus.Partial, returned.Completeness.Status);
    }

    [Fact]
    public void Session_RejectsMismatchedContextAndVersionWithoutConsumingSession()
    {
        var buffer = new BoundedDiagnosticBuffer(enabled: true);
        var (context, version) = Identity();
        var session = Assert.IsType<InMemoryDiagnosticSession>(
            new InMemoryDiagnosticCollector(buffer).Begin(context, version));
        var wrongContext = Diagnostic(DiagnosticContext.CreateNew(), version, "OUTRA.TAG");
        var wrongVersion = Diagnostic(
            context,
            new DiagnosticVersionMetadata("v2", "build", "commit", "config"),
            "OUTRA.TAG");

        Assert.Same(wrongContext, session.Complete(wrongContext));
        Assert.Same(wrongVersion, session.Complete(wrongVersion));
        Assert.Equal(2, session.RejectedDiagnosticCount);
        Assert.Empty(buffer.Snapshot());

        var correct = Diagnostic(context, version, "PK7.TAG1");
        Assert.Same(correct, session.Complete(correct));
        Assert.Same(correct, Assert.Single(buffer.Snapshot()));
    }

    [Fact]
    public void MarkIncomplete_CopiesAndPreservesOriginalDiagnosis()
    {
        var buffer = new BoundedDiagnosticBuffer(enabled: true);
        var (context, version) = Identity();
        var session = new InMemoryDiagnosticCollector(buffer).Begin(context, version);
        var original = Diagnostic(context, version, "PK7.TAG2");
        var completeness = new DiagnosticCompleteness(
            DiagnosticCompletenessStatus.Lost,
            missingSections: ["Detectors"],
            reason: "Captura interrompida.");

        var captured = session.MarkIncomplete(original, completeness);

        Assert.NotSame(original, captured);
        Assert.Equal(DiagnosticCompletenessStatus.Partial, original.Completeness.Status);
        Assert.Equal(DiagnosticCompletenessStatus.Lost, captured.Completeness.Status);
        Assert.Equal(original.ReadingValue, captured.ReadingValue);
        Assert.Equal(original.TagName, captured.TagName);
        Assert.Same(captured, Assert.Single(buffer.Snapshot()));
    }

    [Fact]
    public void StageObservations_AreBoundedAndAreNotRecordedAfterFinalization()
    {
        var buffer = new BoundedDiagnosticBuffer(enabled: true);
        var (context, version) = Identity();
        var session = Assert.IsType<InMemoryDiagnosticSession>(
            new InMemoryDiagnosticCollector(buffer).Begin(context, version));
        var observation = new DiagnosticStageObservation(
            DiagnosticStage.RequestReceived, DateTimeOffset.UtcNow, TimeSpan.Zero);

        for (var i = 0; i < 40; i++)
        {
            session.RecordStage(observation);
        }

        var before = session.StageObservations;
        Assert.Equal(32, before.Count);
        Assert.Equal(8, session.DiscardedStageCount);

        session.Complete(Diagnostic(context, version, "PK7.TAG3"));
        session.RecordStage(observation);

        Assert.Equal(32, session.StageObservations.Count);
        Assert.Equal(32, before.Count);
        Assert.Single(buffer.Snapshot());
    }

    [Fact]
    public void Completion_IsIdempotentAndDoesNotOverwriteFirstReading()
    {
        var buffer = new BoundedDiagnosticBuffer(enabled: true);
        var (context, version) = Identity();
        var session = new InMemoryDiagnosticCollector(buffer).Begin(context, version);
        var first = Diagnostic(context, version, "PK7.TAG1");
        var second = Diagnostic(context, version, "PK7.TAG2");

        Assert.Same(first, session.Complete(first));
        Assert.Same(first, session.Complete(second));
        Assert.Same(first, Assert.Single(buffer.Snapshot()));
    }

    [Fact]
    public void Sessions_RespectBufferEvictionOrder()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 1, enabled: true);
        var collector = new InMemoryDiagnosticCollector(buffer);
        var (context1, version) = Identity();
        var (context2, _) = Identity();

        collector.Begin(context1, version).Complete(Diagnostic(context1, version, "PK7.TAG1"));
        collector.Begin(context2, version).Complete(Diagnostic(context2, version, "PK7.TAG2"));

        Assert.Equal("PK7.TAG2", Assert.Single(buffer.Snapshot()).TagName);
        Assert.Equal(1, buffer.EvictedCount);
    }

    [Fact]
    public void ParallelSessions_KeepEachDiagnosticAssociatedWithItsContext()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 100, enabled: true);
        var collector = new InMemoryDiagnosticCollector(buffer);
        var version = new DiagnosticVersionMetadata("api", "build", "commit", "config");

        Parallel.For(0, 50, i =>
        {
            var context = DiagnosticContext.CreateNew();
            var diagnostic = Diagnostic(context, version, $"PK7.TAG{i}");
            collector.Begin(context, version).Complete(diagnostic);
        });

        var snapshot = buffer.Snapshot();
        Assert.Equal(50, snapshot.Count);
        Assert.Equal(50, snapshot.Select(d => d.Context.DiagnosticId).Distinct().Count());
        Assert.Equal(0, buffer.EvictedCount);
    }

    private static (DiagnosticContext, DiagnosticVersionMetadata) Identity() =>
        (DiagnosticContext.CreateNew(),
         new DiagnosticVersionMetadata("api", "build", "commit", "config"));

    private static IntelligenceLabReadingDiagnostic Diagnostic(
        DiagnosticContext context,
        DiagnosticVersionMetadata version,
        string tag) => new(
            context,
            version,
            "PK7",
            tag,
            DateTimeOffset.UtcNow,
            100,
            new DiagnosticCompleteness(
                DiagnosticCompletenessStatus.Partial,
                reason: "Teste de coletor: sem secoes de inferencia."),
            new DiagnosticTiming(TimeSpan.Zero));
}
