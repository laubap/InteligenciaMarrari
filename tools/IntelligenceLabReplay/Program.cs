using System.Globalization;
using System.Security.Cryptography;
using IAApi.IntelligenceLab.Evaluation;
using System.Text;
using System.Text.Json;
using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Orchestration;
using IAApi.Services.AnomalyDetection.Presentation;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Laboratorio isolado: nao inicia a API, nao consulta o Bridge, nao abre o
// banco operacional e nao altera a configuracao de producao.
return await IntelligenceLabReplay.RunAsync(args);

internal static class IntelligenceLabReplay
{
    private const string ClientId = "SIM-IL-REPLAY-PK7";
    private const string ReplayScenarioId = "unlabeled-historical-replay-pk7";
    private const string TagName = "PK7.TAG1";
    private const int MaxReadings = 10_000;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 3 || args.Length > 4 ||
            (args.Length == 4 && (!int.TryParse(args[3], out var limit) || limit < 1)))
        {
            Console.Error.WriteLine("Uso: dotnet run --project Tools/IntelligenceLabReplay -- <treino.json> <avaliacao.json> <pasta_saida> [limite]");
            return 2;
        }

        try
        {
            var training = LoadReadings(args[0]);
            var evaluation = LoadReadings(args[1]);
            var count = args.Length == 4 ? int.Parse(args[3], CultureInfo.InvariantCulture) : evaluation.Count;
            count = Math.Min(count, evaluation.Count);
            evaluation = evaluation.Take(count).ToList();

            if (training.Count < 10)
                throw new InvalidOperationException("Treinamento requer pelo menos 10 leituras.");
            if (training[^1].DataHora >= evaluation[0].DataHora)
                throw new InvalidOperationException("O treinamento deve terminar antes do inicio do replay.");

            Console.WriteLine($"Treinamento: {training.Count} leituras ({training[0].DataHora:O} a {training[^1].DataHora:O})");
            Console.WriteLine($"Avaliacao: {evaluation.Count} leituras ({evaluation[0].DataHora:O} a {evaluation[^1].DataHora:O})");
            Console.WriteLine("Reexecutando com os seis detectores estatisticos, sem Isolation Forest nem multivariada.");

            var off = await ExecuteAsync(training, evaluation, capture: false);
            var on = await ExecuteAsync(training, evaluation, capture: true);

            if (off.Records.Count != on.Records.Count)
                throw new InvalidOperationException("Quantidade de decisoes diferente entre os modos.");

            var differences = new List<string>();
            for (var i = 0; i < off.Records.Count; i++)
            {
                var a = off.Records[i];
                var b = on.Records[i];
                if (a.DataHora != b.DataHora || a.Valor != b.Valor ||
                    a.Score != b.Score || a.EhAnomalia != b.EhAnomalia ||
                    a.DetectoresJson != b.DetectoresJson ||
                    a.HistoryScore != b.HistoryScore ||
                    a.HistoryAnomaly != b.HistoryAnomaly ||
                    a.HistorySource != b.HistorySource ||
                    a.HistoryReasonsJson != b.HistoryReasonsJson)
                {
                    differences.Add($"Leitura {i + 1}: {a.DataHora:O}");
                }
            }

            if (on.StoredTotal != on.Records.Count || on.DiagnosticCount != on.Records.Count ||
                on.CaptureAttempts != on.Records.Count || on.CaptureFailures != 0 ||
                on.EvictedTotal != 0)
            {
                throw new InvalidOperationException(
                    $"Falha na captura: tentativas={on.CaptureAttempts}, armazenados={on.StoredTotal}, " +
                    $"no buffer={on.DiagnosticCount}, falhas={on.CaptureFailures}, expulsos={on.EvictedTotal}.");
            }

            string? contentHash = null;
            string? replayRunId = null;
            IReadOnlyList<AiReadingEvaluationResult>? evaluationReadings = null;
            IReadOnlyList<ReplayDiagnosticJsonlEntry>? diagnosticEntries = null;
            if (differences.Count == 0)
            {
                // O hash representa o conteudo observado, nao uma identidade de
                // execucao completa. Nenhum score ou decisao e recalculado aqui.
                contentHash = Convert.ToHexString(SHA256.HashData(
                    JsonSerializer.SerializeToUtf8Bytes(on.Records.Select(r => new
                    { r.DataHora, r.Valor, r.Score, r.EhAnomalia,
                      r.DetectoresJson, r.HistorySource }).ToArray()))).ToLowerInvariant();
                replayRunId = $"replay-unlabeled-{contentHash[..16]}";
                var observed = on.Records.Select((r, i) => new ReplayObservedPrediction(
                    new DateTimeOffset(DateTime.SpecifyKind(r.DataHora, DateTimeKind.Utc)),
                    r.EhAnomalia,
                    on.Diagnostics[i].Ensemble?.NormalizedScore,
                    on.Diagnostics[i].Context.DiagnosticId,
                    on.Diagnostics[i].RuntimeDecision?.Source ?? throw new InvalidOperationException(
                        $"Diagnostico sem fonte de decisao na leitura {i + 1}."))).ToArray();
                evaluationReadings = ReplayReadingEvaluationMapper.Map(
                    replayRunId, ReplayScenarioId, ClientId, TagName, observed);
                diagnosticEntries = ReplayDiagnosticJsonlExporter.CreateEntries(
                    replayRunId, ReplayScenarioId, evaluationReadings, on.Diagnostics);
            }

            var folder = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "intelligence-lab-replay-report.json");
            var report = new
            {
                Environment = "IsolatedSQLiteInMemory",
                TrainingSource = Path.GetFileName(args[0]),
                EvaluationSource = Path.GetFileName(args[1]),
                ClientId,
                TagName,
                DatasetLabeled = false,
                SourceTimestampAssumption = "UTC-03:00 (horarios do Bridge sem offset)",
                DetectorSet = "Statistical,MAD,EWMA,RateOfChange,Persistence,LevelShift",
                IsolationForestEnabled = false,
                MultivariateEnabled = false,
                TrainingCount = training.Count,
                EvaluationCount = evaluation.Count,
                Off = new { off.AnomalyCount, off.PersistedCount },
                On = new { on.AnomalyCount, on.PersistedCount, on.CaptureAttempts,
                           on.StoredTotal, on.DiagnosticCount, on.CaptureFailures, on.EvictedTotal },
                EquivalentDecisions = differences.Count == 0,
                DiagnosticExportIntegrity = differences.Count == 0 ? "Validated" : "NotExportedDecisionMismatch",
                Mismatches = differences,
                Readings = on.Records.Select(r => new {
                    r.DataHora, r.Valor, r.Score, r.EhAnomalia,
                    r.HistoryScore, r.HistoryAnomaly, r.HistorySource,
                    r.DetectoresJson
                })
            };
            // Exportacao de evidencias e leituras observadas, sem gabarito industrial.
            if (diagnosticEntries is not null && evaluationReadings is not null &&
                contentHash is not null && replayRunId is not null)
            {
                var diagnosticsFile = Path.Combine(folder, "intelligence-lab-replay-diagnostics.jsonl");
                await ReplayDiagnosticJsonlExporter.WriteAsync(diagnosticsFile, diagnosticEntries);
                var exportFile = Path.Combine(folder, "intelligence-lab-replay-evaluation-readings.json");
                var exported = new
                {
                    SchemaVersion = "industrial-replay-readings-export/v1",
                    RunId = replayRunId,
                    ScenarioId = ReplayScenarioId,
                    TestClientId = ClientId,
                    TagName,
                    DatasetLabeled = false,
                    EffectivenessEvaluated = false,
                    DecisionEquivalenceVerified = true,
                    ReplayContentSha256 = contentHash,
                    ExecutionIdentityComplete = false,
                    DetectorSet = "Statistical,MAD,EWMA,RateOfChange,Persistence,LevelShift",
                    Limitations = "Sem rotulos independentes, commit/configuracao/modelos completos, IF ou multivariada. " +
                                  "Nao calcular TP/FP/FN a partir deste arquivo isoladamente.",
                    Readings = evaluationReadings
                };
                await File.WriteAllTextAsync(exportFile, JsonSerializer.Serialize(exported,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }), Encoding.UTF8);
                Console.WriteLine($"Diagnosticos completos: {diagnosticsFile}");
                Console.WriteLine($"Leituras para avaliacao (sem gabarito): {exportFile}");
            }

            // So publicar o relatorio do run apos a exportacao de evidencias.
            // Se a escrita JSONL falhar, a execucao encerra com falha e nao
            // deixa um relatorio novo alegando integridade validada.
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

            Console.WriteLine($"Comparacao: {differences.Count} divergencias em {evaluation.Count} leituras.");
            Console.WriteLine($"Anomalias: {on.AnomalyCount}; capturas: {on.StoredTotal}; perdas: {on.EvictedTotal}; falhas: {on.CaptureFailures}.");
            Console.WriteLine($"Relatorio salvo em: {file}");
            return differences.Count == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"REPLAY FALHOU: {ex}");
            return 1;
        }
    }

    private static List<LeituraBruta> LoadReadings(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Arquivo nao contem array JSON: {path}");

        var readings = new List<LeituraBruta>();
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("tagName", out var tagProperty) ||
                tagProperty.GetString() != TagName)
                throw new InvalidDataException($"Tag diferente de {TagName}: {path}");
            var dateString = item.GetProperty("dataHora").GetString() ?? "";
            var localClock = DateTime.ParseExact(dateString, "yyyy-MM-dd'T'HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None);
            // O JSON do Bridge nao informa fuso. Para este replay, adotar
            // explicitamente UTC-03:00 e registrar essa suposicao no relatorio.
            // A captura do Intelligence Lab exige um instante UTC inequivoco.
            var date = new DateTimeOffset(localClock, TimeSpan.FromHours(-3)).UtcDateTime;
            var value = item.GetProperty("valor").GetSingle();
            if (float.IsInfinity(value) || float.IsNaN(value))
                throw new InvalidDataException("Leitura nao finita.");
            readings.Add(new LeituraBruta { DataHora = date, Valor = value });
            if (readings.Count > MaxReadings)
                throw new InvalidDataException($"Limite de {MaxReadings} leituras excedido.");
        }
        readings.Sort((a, b) => a.DataHora.CompareTo(b.DataHora));
        if (readings.Count == 0)
            throw new InvalidDataException($"Historico vazio: {path}");
        for (var i = 1; i < readings.Count; i++)
            if (readings[i].DataHora <= readings[i - 1].DataHora)
                throw new InvalidDataException($"Horarios repetidos no arquivo: {path}");
        return readings;
    }

    private static async Task<ReplayRun> ExecuteAsync(
        List<LeituraBruta> training, List<LeituraBruta> evaluation, bool capture)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.TagsIaConfig.Add(new TagIaConfig { ClienteId = ClientId, TagName = TagName, IaAtiva = true });
        db.TagsContextoIa.Add(new TagContextoIa {
            ClienteId = ClientId, TagName = TagName, TipoRepresentacao = "continuous"
        });
        await db.SaveChangesAsync();

        var history = new ReplayHistory(training);
        var trainingService = new PerfilTagService(history, db, new HistoricoLimpezaService());
        var profile = trainingService.TreinarPerfil(ClientId, TagName);
        profile.DataTreinamento = training[^1].DataHora;
        await db.SaveChangesAsync();

        var detectionOptions = new AnomalyDetectionOptions();
        IAnomalyDetector[] detectors = [
            new StatisticalDetector(detectionOptions),
            new MadDetector(detectionOptions),
            new EwmaDetector(detectionOptions),
            new RateOfChangeDetector(detectionOptions),
            new PersistenceDetector(detectionOptions),
            new LevelShiftDetector(detectionOptions)
        ];
        var ensemble = new EnsembleDetectionService(detectors,
            Options.Create(detectionOptions),
            new TagBehaviorProfileResolver(), new DetectorApplicabilityPolicy());
        var interpretation = new PredictionInterpretationService(
            new AnomalyInterpretationContextBuilder(new TagContextoIaService(db)),
            new ReplayInterpretation());
        var runtimeContext = new RuntimeFunctionalDecisionContextProvider(
            new RuntimeFunctionalDecisionStateStore(), Options.Create(detectionOptions));
        var runtimeDecision = new PredictionRuntimeDecisionService(
            new RuntimeFunctionalDecisionCandidateFactory(),
            new RuntimeAnomalyDecisionResolver(),
            new RuntimeAnomalyDecisionOptions { Enabled = false },
            new RuntimeDecisionMetrics());
        var decision = new PredictionDecisionOrchestrationService(
            ensemble, interpretation, runtimeContext, runtimeDecision);
        var postDecision = new PredictionPostDecisionAnalysisService(
            new AnaliseRelacionadasService(db, null!),
            new PredictionRiskAnalysisService(new RiskScoreService(), new ExplicacaoRiscoService()),
            new PredictionTrendAnalysisService(
                new TendenciaRiscoService(db), new TendenciaValorService(history)),
            new FeedbackAnomaliaAnaliseService(db));
        var finalization = new PredictionFinalizationService(
            new AnomaliaHistoricoService(db),
            new PredictionResponseBuilder(
                new PredictionDependencyMapper(new InterpretacaoDependenciasService())));

        var buffer = new BoundedDiagnosticBuffer(evaluation.Count + 10, enabled: capture);
        IIntelligenceLabDiagnosticCollector? collector = capture ? new InMemoryDiagnosticCollector(buffer) : null;
        var version = capture ? new DiagnosticVersionMetadata(
            "replay-dev", "local-replay", "not-a-production-build", "unlabeled-historical-replay") : null;
        var service = new PredictionService(
            new PredictionContextLoader(db, new ProcessoAnaliseService(db)),
            postDecision, finalization, decision,
            new PredictionDetectionPreparationService(history),
            multivariateShadowRuntimeCoordinator: null,
            diagnosticCollector: collector,
            diagnosticVersion: version,
            diagnosticHealth: capture ? buffer.Health : null);

        var records = new List<ReplayRecord>(evaluation.Count);
        foreach (var reading in evaluation)
        {
            // Nao acrescentar a leitura atual ao historico ANTES da decisao.
            var response = await service.Detectar(new AnomaliaRequest {
                ClienteId = ClientId, TagName = TagName,
                Valor = reading.Valor, DataHora = reading.DataHora
            });
            var saved = await db.AnomaliasDetectadas.AsNoTracking()
                .OrderByDescending(x => x.Id).FirstAsync();
            records.Add(new ReplayRecord(reading.DataHora, reading.Valor,
                response.Score, response.EhAnomalia,
                JsonSerializer.Serialize(response.Detectores),
                saved.Score, saved.EhAnomalia,
                saved.RuntimeDecisionSource, saved.RuntimeDecisionReasonsJson));
            history.Observe(reading);
        }

        var diagnostics = buffer.Snapshot();
        if (capture)
        {
            if (diagnostics.Count != records.Count)
                throw new InvalidOperationException("Nem toda decisao gerou diagnostico.");
            for (var i = 0; i < records.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Ensemble is null ||
                    diagnostic.ReadingTimestampUtc !=
                        new DateTimeOffset(DateTime.SpecifyKind(records[i].DataHora, DateTimeKind.Utc)) ||
                    diagnostic.ClienteId != ClientId || diagnostic.TagName != TagName ||
                    Math.Abs(diagnostic.Ensemble.OriginalScore - records[i].Score) > 0.00001 ||
                    diagnostic.RuntimeDecision?.FinalAnomaly != records[i].EhAnomalia)
                    throw new InvalidOperationException($"Diagnostico inconsistente na leitura {i + 1}.");
            }
        }
        var health = buffer.Health.Snapshot(buffer);
        return new ReplayRun(records, records.Count(x => x.EhAnomalia),
            await db.AnomaliasDetectadas.CountAsync(),
            diagnostics.Count, health.CaptureAttempts, health.StoredTotal,
            health.CaptureFailures, health.EvictedTotal, diagnostics.ToArray());
    }

    private sealed record ReplayRun(
        List<ReplayRecord> Records, int AnomalyCount, int PersistedCount,
        int DiagnosticCount, long CaptureAttempts, long StoredTotal,
        long CaptureFailures, long EvictedTotal,
        IReadOnlyList<IntelligenceLabReadingDiagnostic> Diagnostics);

    private sealed record ReplayRecord(
        DateTime DataHora, float Valor, double Score, bool EhAnomalia,
        string DetectoresJson, double HistoryScore, bool HistoryAnomaly,
        string? HistorySource, string? HistoryReasonsJson);

    private sealed class ReplayHistory(List<LeituraBruta> training) : IHistoricoService
    {
        private readonly List<LeituraBruta> _seen = new(training);
        public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag)
            => _seen.TakeLast(1440).ToList();
        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId, string tagName, DateTime inicio, DateTime fim)
            => _seen.Where(r => r.DataHora >= inicio && r.DataHora <= fim).ToList();
        public void Observe(LeituraBruta reading) => _seen.Add(reading);
    }

    private sealed class ReplayInterpretation : IAnomalyInterpretationService
    {
        public Task<AnomalyInterpretationResult> InterpretarAsync(
            AnomalyInterpretationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AnomalyInterpretationResult {
                Resumo = "Replay isolado", InterpretacaoTecnica = "Replay isolado",
                Severidade = "Desconhecida", ModeloUtilizado = "Replay"
            });
    }
}
