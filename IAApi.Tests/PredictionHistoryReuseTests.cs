using System.Net;
using System.Reflection;
using System.Text.Json;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using IAApi.Services.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace IAApi.Tests;

public sealed class PredictionHistoryReuseTests(ITestOutputHelper output)
{
    private static readonly DateTime Time = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static PredictionHistoryReuseOptions Enabled => new("Development", "1");

    [Theory]
    [InlineData("Development", null, false)]
    [InlineData("Development", "0", false)]
    [InlineData("Development", "true", false)]
    [InlineData("Development", " 1", false)]
    [InlineData("Production", "1", false)]
    [InlineData("Staging", "1", false)]
    [InlineData("Development", "1", true)]
    public void Gate_RequiresDevelopmentAndExactFlag(string environment, string? flag, bool expected) =>
        Assert.Equal(expected, new PredictionHistoryReuseOptions(environment, flag).Enabled);

    [Theory]
    [InlineData(null, 2)]
    [InlineData("0", 2)]
    [InlineData("1", 1)]
    public async Task FixedHistory_PreservesResultsAndSideEffects(string? flag, int expectedCalls)
    {
        var originalHistory = new RecordingHistory();
        await using var original = await Fixture.CreateAsync(originalHistory);
        var expected = await original.PredictAsync();
        var history = new RecordingHistory();
        await using var experiment = await Fixture.CreateAsync(history, new("Development", flag));
        var actual = await experiment.PredictAsync();
        AssertEquivalent(expected, actual);
        Assert.Equal(2, originalHistory.Calls.Count);
        Assert.Equal(expectedCalls, history.Calls.Count);
        Assert.All(history.Calls, c => Assert.Equal(("CLIENT-A", "TAG-A", "continuous"), c));
        Assert.Equal(1, experiment.Detector.Calls);
        var saved = Assert.Single(await experiment.Db.AnomaliasDetectadas.ToListAsync());
        Assert.Equal(actual.EhAnomalia, saved.EhAnomalia);
        Assert.Equal((double)actual.Score, saved.Score);
        Assert.All(await experiment.Db.PerfisIa.ToListAsync(), p => Assert.Equal(100, p.Media));
        Assert.Empty(await experiment.Db.TagsComportamentosIa.ToListAsync());
        Assert.Empty(await experiment.Db.TagsComportamentosSnapshotsIa.ToListAsync());
    }

    [Fact]
    public async Task Production_DoesNotCaptureOrReuseEvenWithFlag()
    {
        var history = new RecordingHistory();
        await using var fixture = await Fixture.CreateAsync(history, new("Production", "1"));
        await fixture.PredictAsync();
        Assert.Equal(2, history.Calls.Count);
        var prepared = await fixture.Preparation.PrepararAsync(Request(), Profile(), "continuous", Time);
        Assert.Null(prepared.RawHistorySnapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualBridgeTransport_IsCalledTwiceOrOnce_AndDiagnosticsExplainSnapshot(bool enabled)
    {
        var logger = new PerfTestLogger();
        var perf = new PerformanceDiagnostics("Development", "1", logger);
        using var handler = new BridgeHandler();
        using var http = new HttpClient(handler);
        var bridge = CreateBridge(http, perf);
        await using var fixture = await Fixture.CreateAsync(bridge, enabled ? Enabled : null, perf);
        var response = await fixture.PredictAsync();
        Assert.Equal(enabled ? 1 : 2, handler.Calls);
        var total = Assert.Single(logger.Events, e => Equals(e["Stage"], "Prediction.Total"));
        Assert.Equal(enabled ? 1 : 2, total["HistoricalRequests"]);
        Assert.Equal(response.Tendencias.TendenciaValor, total["ValueTrend"]);
        Assert.Equal((double)response.Score, total["DetectorScore"]);
        Assert.Equal(false, total["DetectorAnomaly"]);
        Assert.Equal(Time, total["PredictionReferenceOriginal"]);
        using var detectorResults = JsonDocument.Parse(Assert.IsType<string>(total["DetectorResults"]));
        var loggedDetector = Assert.Single(detectorResults.RootElement.EnumerateArray());
        Assert.Equal(response.Detectores.Single().Nome, loggedDetector.GetProperty("Nome").GetString());
        Assert.Equal(response.Detectores.Single().ScoreOriginal, loggedDetector.GetProperty("ScoreOriginal").GetDouble());
        var initial = Assert.Single(logger.Events, e => Equals(e["Stage"], "Preparation.History"));
        var trend = Assert.Single(logger.Events, e => Equals(e["Stage"],
            enabled ? "ValueTrend.ReuseRawHistory" : "ValueTrend.History"));
        Assert.Equal(enabled ? "ReusedPredictionRaw" : "Fetched", trend["HistorySource"]);
        Assert.Equal(initial["HistoryFirstOriginal"], trend["HistoryFirstOriginal"]);
        Assert.Equal(initial["HistoryLastOriginal"], trend["HistoryLastOriginal"]);
        Assert.Equal(40, trend["ReceivedRecords"]);
        if (enabled)
        {
            Assert.Equal(initial["HistoryAcquiredStartedUtc"], trend["HistoryAcquiredStartedUtc"]);
            Assert.Equal(initial["HistoryAcquiredCompletedUtc"], trend["HistoryAcquiredCompletedUtc"]);
            Assert.DoesNotContain(logger.Events, e => Equals(e["Stage"], "ValueTrend.History"));
        }
        foreach (var line in logger.Messages.Where(m => m.Contains("Stage=Prediction.Total")
            || m.Contains("Stage=ValueTrend."))) output.WriteLine(line);
    }

    [Fact]
    public async Task MovingSource_ExplicitlyChangesTrendSnapshot_WithoutChangingDetector()
    {
        var first = Readings();
        var second = Readings(descending: true);
        var originalSource = new RecordingHistory { Read = (_, _, n) => n == 1 ? first : second };
        var reuseSource = new RecordingHistory { Read = (_, _, n) => n == 1 ? first : second };
        await using var original = await Fixture.CreateAsync(originalSource);
        await using var reused = await Fixture.CreateAsync(reuseSource, Enabled);
        var old = await original.PredictAsync();
        var current = await reused.PredictAsync();
        Assert.Equal("queda", old.Tendencias.TendenciaValor);
        Assert.Equal("alta", current.Tendencias.TendenciaValor);
        Assert.Equal(JsonSerializer.Serialize(old.Detectores), JsonSerializer.Serialize(current.Detectores));
        Assert.Equal(old.EhAnomalia, current.EhAnomalia);
        Assert.Equal(2, originalSource.Calls.Count);
        Assert.Single(reuseSource.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialFailureOrCancellation_RemainsUnchanged(bool enabled)
    {
        var failure = new HttpRequestException("Bridge unavailable");
        var history = new RecordingHistory { Read = (_, _, _) => throw failure };
        await using var fixture = await Fixture.CreateAsync(history, enabled ? Enabled : null);
        Assert.Same(failure, await Assert.ThrowsAsync<HttpRequestException>(() => fixture.PredictAsync()));
        Assert.Single(history.Calls);
        Assert.Equal(0, fixture.Detector.Calls);
        Assert.Empty(await fixture.Db.AnomaliasDetectadas.ToListAsync());
    }

    [Theory]
    [InlineData(false, "before")]
    [InlineData(true, "before")]
    [InlineData(false, "history")]
    [InlineData(true, "history")]
    [InlineData(false, "decision")]
    [InlineData(true, "decision")]
    public async Task Cancellation_KeepsTokenAndDoesNotPersist(bool enabled, string when)
    {
        using var cts = new CancellationTokenSource();
        var history = new RecordingHistory { Read = (_, _, _) =>
        { if (when == "history") cts.Cancel(); return Readings(); } };
        await using var fixture = await Fixture.CreateAsync(history, enabled ? Enabled : null);
        if (when == "before") cts.Cancel();
        if (when == "decision") fixture.Detector.OnDetect = () => cts.Cancel();
        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.PredictAsync(ct: cts.Token));
        Assert.Equal(cts.Token, cancelled.CancellationToken);
        Assert.Equal(when == "before" ? 0 : 1, history.Calls.Count);
        Assert.Equal(when == "decision" ? 1 : 0, fixture.Detector.Calls);
        Assert.Empty(await fixture.Db.AnomaliasDetectadas.ToListAsync());
    }

    [Theory]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "null")]
    [InlineData(true, "null")]
    [InlineData(false, "invalid")]
    [InlineData(true, "invalid")]
    [InlineData(false, "null-item")]
    [InlineData(true, "null-item")]
    public async Task EmptyInvalidOrNullHistory_PreservesExistingFailure(bool enabled, string mode)
    {
        var values = mode switch
        {
            "empty" => new List<LeituraBruta>(),
            "null" => null!,
            "invalid" => Readings().Select(r => new LeituraBruta { DataHora = r.DataHora, Valor = float.NaN }).ToList(),
            _ => Readings().Append(null!).ToList()
        };
        var source = new RecordingHistory { Read = (_, _, _) => values };
        await using var fixture = await Fixture.CreateAsync(source, enabled ? Enabled : null);
        var error = await Record.ExceptionAsync(() => fixture.PredictAsync());
        Assert.NotNull(error);
        if (mode == "null-item") Assert.IsType<NullReferenceException>(error);
        else if (mode == "null") Assert.Contains("resposta nula", error.Message);
        else Assert.Contains("Histórico insuficiente", error.Message);
        Assert.Equal(mode == "null-item" && !enabled ? 2 : 1, source.Calls.Count);
        Assert.Empty(await fixture.Db.AnomaliasDetectadas.ToListAsync());
    }

    [Fact]
    public async Task Snapshot_ProtectsValuesFromLaterMutationAndDoesNotRetainBetweenPredictions()
    {
        var values = Readings();
        var source = new RecordingHistory { Read = (_, _, _) => values };
        await using var fixture = await Fixture.CreateAsync(source, Enabled);
        fixture.Detector.OnDetect = () =>
        {
            foreach (var reading in values) { reading.Valor = 999; reading.DataHora = default; }
            values.Clear();
        };
        Assert.Equal("alta", (await fixture.PredictAsync()).Tendencias.TendenciaValor);
        Assert.Single(source.Calls);
        fixture.Detector.OnDetect = null;
        source.Read = (_, _, _) => Readings(descending: true);
        Assert.Equal("queda", (await fixture.PredictAsync()).Tendencias.TendenciaValor);
        Assert.Equal(2, source.Calls.Count);
    }

    [Fact]
    public async Task ClientsAndTags_AreIsolatedEvenOnSameServiceInstance()
    {
        var source = new RecordingHistory
        { Read = (client, tag, _) => Readings(descending: client == "CLIENT-B" || tag == "TAG-B") };
        await using var fixture = await Fixture.CreateAsync(source, Enabled);
        Assert.Equal("alta", (await fixture.PredictAsync()).Tendencias.TendenciaValor);
        Assert.Equal("queda", (await fixture.PredictAsync(tag: "TAG-B")).Tendencias.TendenciaValor);
        Assert.Equal("queda", (await fixture.PredictAsync(client: "CLIENT-B")).Tendencias.TendenciaValor);
        Assert.Equal("alta", (await fixture.PredictAsync()).Tendencias.TendenciaValor);
        Assert.Equal(4, source.Calls.Count);
        Assert.Equal(4, await fixture.Db.AnomaliasDetectadas.CountAsync());
    }

    [Theory]
    [InlineData("client")]
    [InlineData("tag")]
    [InlineData("type")]
    [InlineData("source")]
    [InlineData("consumer-off")]
    public async Task MismatchedSnapshotOrDisabledConsumer_FallsBackToOriginalQuery(string mismatch)
    {
        var source = new RecordingHistory();
        var result = await new PredictionDetectionPreparationService(source, historyReuse: Enabled)
            .PrepararAsync(Request(), Profile(), "continuous", Time);
        Assert.NotNull(result.RawHistorySnapshot);
        var consumerSource = mismatch == "source" ? new RecordingHistory() : source;
        var trend = new TendenciaValorService(consumerSource,
            historyReuse: mismatch == "consumer-off" ? null : Enabled);
        var callsBefore = consumerSource.Calls.Count;
        trend.Analisar(mismatch == "client" ? "CLIENT-B" : "CLIENT-A",
            mismatch == "tag" ? "TAG-B" : "TAG-A", mismatch == "type" ? "other" : "continuous",
            result.RawHistorySnapshot);
        Assert.Equal(callsBefore + 1, consumerSource.Calls.Count);
    }

    [Fact]
    public async Task RegimeSelection_OnlyChangesDetectorHistory_TrendKeepsAllRawReadings()
    {
        var source = new RecordingHistory();
        await using var fixture = await Fixture.CreateAsync(source, Enabled, regime: true);
        var response = await fixture.PredictAsync();
        Assert.Equal(20, fixture.Detector.LastInput!.Historico.Count);
        Assert.Equal(200, fixture.Detector.LastInput.MediaHistorica);
        Assert.Equal("alta", response.Tendencias.TendenciaValor); // Selected subset alone would be stable.
        Assert.Single(source.Calls);
        Assert.Equal(1, source.PeriodCalls); // Regime request is intentionally preserved.
    }

    [Fact]
    public async Task InvalidRawPointsAndFutureTimestamps_AreNotReplacedBySanitizedDetectorHistory()
    {
        var readings = Readings();
        readings.Add(new LeituraBruta { DataHora = Time.AddMinutes(5), Valor = float.NaN });
        readings.Add(new LeituraBruta { DataHora = default, Valor = 1 });
        var source = new RecordingHistory { Read = (_, _, _) => readings };
        var prepared = await new PredictionDetectionPreparationService(source, historyReuse: Enabled)
            .PrepararAsync(Request(), Profile(), "continuous", Time);
        var snapshot = Assert.IsType<PredictionRawHistorySnapshot>(prepared.RawHistorySnapshot);
        Assert.Equal(42, snapshot.Count);
        Assert.Equal(40, prepared.EntradaDetector.Historico.Count);
        var trend = new TendenciaValorService(source, historyReuse: Enabled);
        var original = new TendenciaValorService(source).Analisar("CLIENT-A", "TAG-A", "continuous");
        var reused = trend.Analisar("CLIENT-A", "TAG-A", "continuous", snapshot);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(reused));
        Assert.Equal(Time, snapshot.PredictionReference);
        Assert.Equal(2, source.Calls.Count); // One preparation, one explicit original comparison.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOnlyOnSecondFetch_IsIntentionallyAvoidedByReuse(bool enabled)
    {
        var failure = new HttpRequestException("second snapshot unavailable");
        var source = new RecordingHistory { Read = (_, _, n) => n == 1 ? Readings() : throw failure };
        await using var fixture = await Fixture.CreateAsync(source, enabled ? Enabled : null);
        if (enabled)
        {
            Assert.Equal("alta", (await fixture.PredictAsync()).Tendencias.TendenciaValor);
            Assert.Single(source.Calls);
            Assert.Single(await fixture.Db.AnomaliasDetectadas.ToListAsync());
        }
        else
        {
            Assert.Same(failure, await Assert.ThrowsAsync<HttpRequestException>(() => fixture.PredictAsync()));
            Assert.Equal(2, source.Calls.Count);
            Assert.Empty(await fixture.Db.AnomaliasDetectadas.ToListAsync());
        }
    }

    [Fact]
    public async Task MultivariateShadow_StillClosesVectorAndExecutesDetector_WithSameResults()
    {
        async Task<(string Results, int Requests)> Run(bool enabled)
        {
            var path = Path.Combine(Path.GetTempPath(), "IAApiTests", "HistoryReuse-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            try
            {
                var persistence = new MultivariateModelPersistenceService(new TestEnvironment(path));
                var modelPath = persistence.ObterCaminhoModelo("CLIENT-A", "GROUP-AB");
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
                await File.WriteAllTextAsync(modelPath, JsonSerializer.Serialize(new MultivariateGroupModel
                {
                    Version = 1, ClienteId = "CLIENT-A", GroupName = "GROUP-AB", Algorithm = "Mahalanobis",
                    TrainedAtUtc = Time, SampleCount = 100, MinimumAvailableTags = 2, MaxLagMinutes = 0,
                    Tags = ["TAG-A", "TAG-B"], Means = [10, 20], StandardDeviations = [1, 1],
                    CovarianceMatrix = [[1, 0], [0, 1]], CorrelationMatrix = [[1, 0], [0, 1]]
                }));
                var collector = new ShadowCollector();
                var coordinator = new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(new GrupoAnaliseService(new Groups()),
                        new MultivariateRuntimeStateStore(), collector),
                    new MultivariateDetector(persistence), NullLogger<MultivariateShadowRuntimeCoordinator>.Instance,
                    collector);
                var source = new RecordingHistory();
                await using var fixture = await Fixture.CreateAsync(source, enabled ? Enabled : null, shadow: coordinator);
                var a = await fixture.PredictAsync(value: 10);
                var b = await fixture.PredictAsync(tag: "TAG-B", value: 20);
                var c = await fixture.PredictAsync(time: Time.AddMinutes(1), value: 10);
                var executed = Assert.Single(collector.Events, e => e.EventKind == MultivariateShadowEventKind.MahalanobisExecuted);
                Assert.True(executed.MahalanobisExecuted);
                Assert.NotNull(executed.NormalizedScore);
                Assert.NotNull(executed.MahalanobisSquared);
                var finalized = Assert.Single(collector.Events, e => e.EventKind == MultivariateShadowEventKind.VectorFinalized);
                Assert.Equal(finalized.VectorId, executed.VectorId);
                Assert.Null(c.Multivariado);
                return (JsonSerializer.Serialize(new { a.Score, a.EhAnomalia, b.Tendencias, c.Detectores,
                    executed.GroupName, executed.MinuteIndex, executed.MahalanobisExecuted, executed.NormalizedScore,
                    executed.Threshold, executed.MahalanobisSquared, executed.OperationalDecisionStatus }),
                    source.Calls.Count);
            }
            finally { Directory.Delete(path, recursive: true); }
        }
        var off = await Run(false);
        var on = await Run(true);
        Assert.Equal(off.Results, on.Results);
        Assert.Equal(6, off.Requests);
        Assert.Equal(3, on.Requests);
    }

    private static void AssertEquivalent(AnomaliaResponse expected, AnomaliaResponse actual)
    {
        Assert.Equal(expected.Score, actual.Score);
        Assert.Equal(expected.EhAnomalia, actual.EhAnomalia);
        Assert.Equal(expected.Mensagem, actual.Mensagem);
        Assert.Equal(JsonSerializer.Serialize(expected.Tendencias), JsonSerializer.Serialize(actual.Tendencias));
        Assert.Equal(JsonSerializer.Serialize(expected.Detectores), JsonSerializer.Serialize(actual.Detectores));
        Assert.Equal(JsonSerializer.Serialize(expected.Perfil), JsonSerializer.Serialize(actual.Perfil));
        Assert.Equal(JsonSerializer.Serialize(expected.Risco), JsonSerializer.Serialize(actual.Risco));
    }

    private static List<LeituraBruta> Readings(bool descending = false) => Enumerable.Range(0, 40)
        .Select(i => new LeituraBruta { DataHora = Time.AddMinutes(i - 40),
            Valor = descending ? (i < 20 ? 200 : 100) : (i < 20 ? 100 : 200) }).ToList();
    private static TagPerfilIa Profile() => new() { Media = 100, DesvioPadrao = 2, VariacaoMinimaRelevante = 5 };
    private static AnomaliaRequest Request() => new() { ClienteId = "CLIENT-A", TagName = "TAG-A", Valor = 100, DataHora = Time };

    private sealed class RecordingHistory : IHistoricoService
    {
        public Func<string, string, int, List<LeituraBruta>> Read { get; set; } = (_, _, _) => Readings();
        public List<(string Client, string Tag, string Type)> Calls { get; } = [];
        public int PeriodCalls { get; private set; }
        public List<LeituraBruta> BuscarHistorico(string c, string t, string type)
        { Calls.Add((c, t, type)); return Read(c, t, Calls.Count); }
        public List<LeituraBruta> BuscarHistoricoPorPeriodo(string c, string t, DateTime start, DateTime end)
        {
            PeriodCalls++;
            return Enumerable.Range(0, 41).Select(i => new LeituraBruta
            { DataHora = Time.AddMinutes(i - 40), Valor = i < 20 ? 0 : 1 }).ToList();
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }
        public CaptureDetector Detector { get; } = new();
        public PredictionDetectionPreparationService Preparation { get; }
        private readonly PredictionService _prediction;
        private Fixture(SqliteConnection connection, AppDbContext db, IHistoricoService source,
            PredictionHistoryReuseOptions? reuse, PerformanceDiagnostics? perf,
            MultivariateShadowRuntimeCoordinator? shadow, bool regime)
        {
            _connection = connection; Db = db;
            var ensemble = new EnsembleDetectionService([Detector], Options.Create(new AnomalyDetectionOptions()),
                new TagBehaviorProfileResolver(), new DetectorApplicabilityPolicy());
            var interpretation = new PredictionInterpretationService(
                new AnomalyInterpretationContextBuilder(new TagContextoIaService(db)), new Interpretation());
            var decision = new PredictionDecisionOrchestrationService(ensemble, interpretation,
                new RuntimeFunctionalDecisionContextProvider(new RuntimeFunctionalDecisionStateStore(), Options.Create(new AnomalyDetectionOptions())),
                new PredictionRuntimeDecisionService(new RuntimeFunctionalDecisionCandidateFactory(),
                    new RuntimeAnomalyDecisionResolver(), new RuntimeAnomalyDecisionOptions { Enabled = false }, new RuntimeDecisionMetrics()),
                performanceDiagnostics: perf);
            var post = new PredictionPostDecisionAnalysisService(new AnaliseRelacionadasService(db, null!),
                new PredictionRiskAnalysisService(new RiskScoreService(), new ExplicacaoRiscoService()),
                new PredictionTrendAnalysisService(new TendenciaRiscoService(db), new TendenciaValorService(source, perf, reuse), perf),
                new FeedbackAnomaliaAnaliseService(db), performanceDiagnostics: perf);
            Preparation = new PredictionDetectionPreparationService(source, regime
                ? new OperationalRegimeBaselineProvider(db, source, new OperationalRegimeBaselineService(
                    new OperationalRegimeHistoryResolver(), new TagProfileStatisticsCalculator(new HistoricoLimpezaService()))) : null,
                performanceDiagnostics: perf, historyReuse: reuse);
            _prediction = new PredictionService(new PredictionContextLoader(db, new ProcessoAnaliseService(db)), post,
                new PredictionFinalizationService(new AnomaliaHistoricoService(db), new PredictionResponseBuilder(
                    new PredictionDependencyMapper(new InterpretacaoDependenciasService()))), decision, Preparation,
                multivariateShadowRuntimeCoordinator: shadow, performanceDiagnostics: perf);
        }

        public static async Task<Fixture> CreateAsync(IHistoricoService source, PredictionHistoryReuseOptions? reuse = null,
            PerformanceDiagnostics? perf = null, MultivariateShadowRuntimeCoordinator? shadow = null, bool regime = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            foreach (var client in new[] { "CLIENT-A", "CLIENT-B" })
            foreach (var tag in new[] { "TAG-A", "TAG-B" })
            {
                db.TagsIaConfig.Add(new TagIaConfig { ClienteId = client, TagName = tag, IaAtiva = true });
                db.PerfisIa.Add(new TagPerfilIa { ClienteId = client, TagName = tag, Media = 100,
                    DesvioPadrao = 2, VariacaoMinimaRelevante = 5, ConfiancaDeadband = 1, DataTreinamento = Time.AddDays(-1) });
                db.TagsContextoIa.Add(new TagContextoIa { ClienteId = client, TagName = tag, TipoRepresentacao = "continuous" });
            }
            if (regime) db.TagsRegimesOperacionaisIa.Add(new TagRegimeOperacionalIa
            { ClienteId = "CLIENT-A", TagName = "TAG-A", TagRegime = "REGIME", Ativo = true,
                LagMinutos = 0, ToleranciaAlinhamentoMinutos = 1, ToleranciaEstado = 0.001, MinimoAmostrasPorRegime = 10 });
            await db.SaveChangesAsync();
            return new Fixture(connection, db, source, reuse, perf, shadow, regime);
        }

        public Task<AnomaliaResponse> PredictAsync(string client = "CLIENT-A", string tag = "TAG-A", DateTime? time = null,
            double value = 100, CancellationToken ct = default) => _prediction.Detectar(new AnomaliaRequest
            { ClienteId = client, TagName = tag, DataHora = time ?? Time, Valor = (float)value }, ct);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private sealed class CaptureDetector : IAnomalyDetector
    {
        public string Nome => "Normal";
        public double Peso => 1;
        public int Calls { get; private set; }
        public DetectorInput? LastInput { get; private set; }
        public Action? OnDetect { get; set; }
        public DetectorResult Detectar(DetectorInput input)
        {
            Calls++; LastInput = input; OnDetect?.Invoke();
            return new DetectorResult { Detector = Nome, Aplicavel = true, EhAnomalia = false, Peso = 1,
                Score = 0, ScoreNormalizado = 0, Explicacao = "Normal", Media = input.MediaHistorica, DesvioPadrao = input.DesvioPadraoHistorico };
        }
    }
    private sealed class Interpretation : IAnomalyInterpretationService
    {
        public Task<AnomalyInterpretationResult> InterpretarAsync(AnomalyInterpretationContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(new AnomalyInterpretationResult
            { Resumo = "Test", InterpretacaoTecnica = "Test", Severidade = "Normal", ModeloUtilizado = "Test" });
    }
    private sealed class BridgeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("/historico?tagName=TAG-A&horas=24", request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(JsonSerializer.Serialize(Readings())) });
        }
    }
    private static BridgeHistoricoService CreateBridge(HttpClient http, PerformanceDiagnostics perf)
    {
        var bridge = new BridgeHistoricoService(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Bridge:BaseUrl"] = "http://bridge.test" }).Build(), perf);
        var field = typeof(BridgeHistoricoService).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((HttpClient)field.GetValue(bridge)!).Dispose();
        field.SetValue(bridge, http);
        return bridge;
    }
    private sealed class Groups : IGrupoAnaliseProvider
    {
        public Task<List<GrupoAnaliseDto>> CarregarGruposAsync(string client) => Task.FromResult(new List<GrupoAnaliseDto>
            { new() { Id = 1, Nome = "GROUP-AB", MinimumAvailableTags = 2, MaxLagMinutes = 0, Tags = ["TAG-A", "TAG-B"] } });
    }
    private sealed class ShadowCollector : IIntelligenceLabMultivariateShadowCollector
    {
        public List<MultivariateShadowDiagnostic> Events { get; } = [];
        public void Record(MultivariateShadowDiagnostic diagnostic) => Events.Add(diagnostic);
        public IReadOnlyList<MultivariateShadowDiagnostic> Snapshot() => Events.ToArray();
    }
    private sealed class TestEnvironment(string path) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "IAApi.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = path;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = path;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
