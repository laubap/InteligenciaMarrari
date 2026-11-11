using System.Net;
using System.Reflection;
using IAApi.Services.Diagnostics;
using IAApi.Services.AnomalyDetection.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;
using Xunit.Abstractions;

namespace IAApi.Tests;

public sealed class PerformanceDiagnosticsTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Development", null, false)]
    [InlineData("Development", "", false)]
    [InlineData("Development", "0", false)]
    [InlineData("Development", "true", false)]
    [InlineData("Development", " 1", false)]
    [InlineData("Production", "1", false)]
    [InlineData("Staging", "1", false)]
    [InlineData("Development", "1", true)]
    public async Task Gate_RequiresDevelopmentAndExactFlag(string environment, string? flag, bool enabled)
    {
        var logger = new PerfTestLogger();
        var diagnostics = new PerformanceDiagnostics(environment, flag, logger);
        var calls = 0;
        Assert.Equal(enabled, diagnostics.Enabled);
        var value = await PerformanceDiagnostics.MeasureAsync(diagnostics, "C", "T", "Total", () =>
            Task.FromResult(PerformanceDiagnostics.Measure(diagnostics, "C", "T", "Child", () => ++calls)));
        Assert.Equal(1, calls);
        Assert.Equal(1, value);
        Assert.Equal(enabled ? 2 : 0, logger.Events.Count);
        if (!enabled) return;
        var child = logger.Events[0];
        var parent = logger.Events[1];
        Assert.Equal(parent["SpanId"], child["ParentSpanId"]);
        Assert.Equal(parent["TraceId"], child["TraceId"]);
        Assert.Null(parent["ParentSpanId"]);
        Assert.True((double)parent["DurationMs"]! >= (double)child["DurationMs"]!);
        foreach (var message in logger.Messages) output.WriteLine(message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExceptionsAndCancellation_KeepOriginalInstancesAndTokens(bool throwingLogger)
    {
        var logger = new PerfTestLogger { Throw = throwingLogger };
        var diagnostics = new PerformanceDiagnostics("Development", "1", logger);
        var failure = new InvalidOperationException("private payload must not be logged");
        var actual = Assert.Throws<InvalidOperationException>(() =>
            PerformanceDiagnostics.Measure<int>(diagnostics, "C", "T", "Failure", () => throw failure));
        Assert.Same(failure, actual);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancellation = new OperationCanceledException(cts.Token);
        var cancelled = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            PerformanceDiagnostics.MeasureAsync<int>(diagnostics, "C", "T", "Cancel",
                () => Task.FromException<int>(cancellation)));
        Assert.Same(cancellation, cancelled);
        Assert.Equal(cts.Token, cancelled.CancellationToken);
        Assert.Equal(42, PerformanceDiagnostics.Measure(diagnostics, "C", "T", "Success", () => 42));
        if (throwingLogger) return;
        Assert.Equal(new[] { "Failed", "Cancelled", "Succeeded" }, logger.Events.Select(e => e["Status"]));
        Assert.All(logger.Events, e => Assert.Null(e["ParentSpanId"]));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(failure.Message));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preparation_PreservesHistoryFailureAndCancellation(bool enabled)
    {
        var logger = new PerfTestLogger();
        var diagnostics = new PerformanceDiagnostics("Development", enabled ? "1" : null, logger);
        var failure = new InvalidOperationException("history unavailable");
        var history = new StubHistory { Failure = failure };
        var preparation = new PredictionDetectionPreparationService(history, performanceDiagnostics: diagnostics);
        var request = new AnomaliaRequest { ClienteId = "C", TagName = "T" };
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => preparation.PrepararAsync(
            request, new TagPerfilIa(), "continuous", DateTime.UtcNow)));
        history.Failure = null;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparation.PrepararAsync(
            request, new TagPerfilIa(), "continuous", DateTime.UtcNow, cts.Token));
        Assert.Equal(cts.Token, cancelled.CancellationToken);
        Assert.Equal(2, history.Calls); // Existing preparation checks cancellation after the fetch.
        Assert.Equal(enabled ? 2 : 0, logger.Events.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preparation_BehaviorFailure_RemainsFailOpen(bool enabled)
    {
        // A real provider with a disposed context forces a failure at memory lookup.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        var provider = new BehaviorRuntimeBaselineProvider(db,
            new BehaviorHistorySegmentationService(), new BehaviorChangePointDetector(),
            new BehaviorCandidateEpisodeBuilder(),
            new BehaviorCandidateEpisodeCharacterizer(new TagProfileStatisticsCalculator(new HistoricoLimpezaService())),
            new BehaviorMemoryReconciliationService(db, new BehaviorMemoryPersistencePlanBuilder()));
        db.Dispose();
        var time = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var history = new StubHistory
        {
            Values = Enumerable.Range(0, 40).Select(i => new LeituraBruta
            { DataHora = time.AddMinutes(i - 40), Valor = 100 + i % 3 }).ToList()
        };
        var logger = new PerfTestLogger();
        var diagnostics = new PerformanceDiagnostics("Development", enabled ? "1" : null, logger);
        var result = await new PredictionDetectionPreparationService(history,
            behaviorRuntimeBaselineProvider: provider, performanceDiagnostics: diagnostics).PrepararAsync(
                new AnomaliaRequest { ClienteId = "C", TagName = "T", Valor = 100 },
                new TagPerfilIa { Media = 100, DesvioPadrao = 2, VariacaoMinimaRelevante = 5 },
                "continuous", time);
        Assert.False(result.BaselineComportamentalAplicado);
        Assert.Equal(100, result.EntradaDetector.MediaHistorica);
        Assert.Equal(40, result.EntradaDetector.Historico.Count);
        Assert.Equal(1, history.Calls);
        if (enabled)
            Assert.Contains(logger.Events, e => Equals(e["Stage"], "Preparation.BehaviorBaseline") && Equals(e["Status"], "Failed"));
        else
            Assert.Empty(logger.Events);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("http-error")]
    [InlineData("timeout")]
    [InlineData("invalid-json")]
    public void Bridge_DoesNotRepeatRequestsOrChangeResultsAndFailures(string mode)
    {
        (List<LeituraBruta>? Result, Exception? Error, int Calls, List<Dictionary<string, object?>> Events) Run(bool enabled)
        {
            var logger = new PerfTestLogger();
            var diagnostics = new PerformanceDiagnostics("Development", enabled ? "1" : null, logger);
            var handler = new BridgeHandler(mode);
            using var http = new HttpClient(handler);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Bridge:BaseUrl"] = "http://bridge.test", ["Bridge:HorasHistorico"] = "24" }).Build();
            var bridge = new BridgeHistoricoService(configuration, diagnostics);
            var field = typeof(BridgeHistoricoService).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!;
            ((HttpClient)field.GetValue(bridge)!).Dispose();
            field.SetValue(bridge, http); // Test transport only; production construction stays intact.
            try { return (bridge.BuscarHistorico("C", "C.T", "continuous"), null, handler.Calls, logger.Events); }
            catch (Exception ex) { return (null, ex, handler.Calls, logger.Events); }
        }

        var off = Run(false);
        var on = Run(true);
        Assert.Equal(1, off.Calls);
        Assert.Equal(1, on.Calls);
        Assert.Empty(off.Events);
        if (mode == "success")
        {
            Assert.Null(on.Error);
            Assert.Equal(off.Result!.Select(x => (x.DataHora, x.Valor)), on.Result!.Select(x => (x.DataHora, x.Valor)));
            var decoded = Assert.Single(on.Events, e => Equals(e["Stage"], "Bridge.Decode"));
            Assert.Equal(3, decoded["ReceivedRecords"]);
            Assert.Equal(1, decoded["ValidRecords"]);
        }
        else
        {
            Assert.Equal(off.Error!.GetType(), on.Error!.GetType());
            Assert.Equal(off.Error.Message, on.Error.Message);
            Assert.Equal(off.Error.InnerException?.GetType(), on.Error.InnerException?.GetType());
            Assert.Contains(on.Events, e => Equals(e["Status"], "Failed"));
        }
    }

    [Theory]
    [InlineData(DateTimeKind.Utc, true)]
    [InlineData(DateTimeKind.Local, false)]
    [InlineData(DateTimeKind.Unspecified, false)]
    public void Monitoring_OnlyCalculatesAgeForTrustedUtc(DateTimeKind kind, bool valid)
    {
        var logger = new PerfTestLogger();
        var diagnostics = new PerformanceDiagnostics("Development", "1", logger);
        var start = new DateTimeOffset(2026, 11, 5, 15, 0, 0, TimeSpan.Zero);
        diagnostics.MonitoringReading("C", "T", DateTime.SpecifyKind(start.UtcDateTime.AddSeconds(-2), kind),
            start, start.AddSeconds(1), 1000, "Concluída");
        Assert.Equal(valid ? 2000d : null, logger.Events.Single()["ReadingAgeAtStartMs"]);
        diagnostics.MonitoringReading("C", "T", null, start, start, 0, "Falha");
        diagnostics.MonitoringReading("C", "T", default(DateTime), start, start, 0, "Falha");
        diagnostics.MonitoringReading("C", "T", start.UtcDateTime.AddSeconds(2), start, start, 0, "Falha");
        Assert.All(logger.Events.Skip(1), e => Assert.Null(e["ReadingAgeAtStartMs"]));
    }

    private sealed class StubHistory : IHistoricoService
    {
        public Exception? Failure { get; set; }
        public List<LeituraBruta> Values { get; init; } = [];
        public int Calls { get; private set; }
        public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag)
        { Calls++; if (Failure is not null) throw Failure; return Values; }
        public List<LeituraBruta> BuscarHistoricoPorPeriodo(string c, string t, DateTime i, DateTime f)
            => throw new NotSupportedException();
    }

    private sealed class BridgeHandler(string mode) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("http://bridge.test/historico?tagName=C.T&horas=24", request.RequestUri!.AbsoluteUri);
            if (mode == "timeout") throw new TaskCanceledException("timeout");
            return Task.FromResult(new HttpResponseMessage(mode == "http-error" ? HttpStatusCode.BadGateway : HttpStatusCode.OK)
            {
                Content = new StringContent(mode == "invalid-json" ? "invalid" : """
                    [{"dataHora":"2026-11-05T10:00:00Z","valor":1},
                     {"dataHora":"2026-11-05T10:00:00Z","valor":2},
                     {"dataHora":"2026-11-05T10:01:00Z","valor":"NaN"}]
                    """)
            });
        }
    }
}

internal sealed class PerfTestLogger : ILogger<PerformanceDiagnostics>
{
    public bool Throw { get; init; }
    public List<Dictionary<string, object?>> Events { get; } = [];
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (Throw) throw new InvalidOperationException("logger unavailable");
        Messages.Add(formatter(state, exception));
        Events.Add(((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(x => x.Key, x => x.Value));
    }
}
