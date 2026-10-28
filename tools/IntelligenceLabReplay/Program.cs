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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
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
        // O modo original continua sendo o padrao; a opcao nova e explicita.
        var fullEnsemble = args.Length > 0 && args[^1] == "--full-ensemble";
        var positional = fullEnsemble ? args[..^1] : args;
        if (positional.Length < 3 || positional.Length > 4 ||
            (positional.Length == 4 && (!int.TryParse(positional[3],
                NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 1)) ||
            positional.Any(value => value.StartsWith("--", StringComparison.Ordinal)))
        {
            Console.Error.WriteLine("Uso: dotnet run --project Tools/IntelligenceLabReplay -- " +
                "<treino.json> <avaliacao.json> <pasta_saida> [limite] [--full-ensemble]");
            return 2;
        }

        try
        {
            var executionStartedAtUtc = DateTimeOffset.UtcNow;
            var trainingInput = await LoadReadingsAsync(positional[0]);
            var evaluationInput = await LoadReadingsAsync(positional[1]);
            var training = trainingInput.Readings;
            var evaluation = evaluationInput.Readings;
            int? requestedEvaluationLimit = positional.Length == 4
                ? int.Parse(positional[3], CultureInfo.InvariantCulture)
                : null;
            var count = requestedEvaluationLimit ?? evaluation.Count;
            count = Math.Min(count, evaluation.Count);
            evaluation = evaluation.Take(count).ToList();

            if (training.Count < 10)
                throw new InvalidOperationException("Treinamento requer pelo menos 10 leituras.");
            if (training[^1].DataHora >= evaluation[0].DataHora)
                throw new InvalidOperationException("O treinamento deve terminar antes do inicio do replay.");

            Console.WriteLine($"Treinamento: {training.Count} leituras ({training[0].DataHora:O} a {training[^1].DataHora:O})");
            Console.WriteLine($"Avaliacao: {evaluation.Count} leituras ({evaluation[0].DataHora:O} a {evaluation[^1].DataHora:O})");
            Console.WriteLine(fullEnsemble
                ? "Reexecutando os seis detectores estatisticos + Isolation Forest local, sem multivariada."
                : "Reexecutando com os seis detectores estatisticos, sem Isolation Forest nem multivariada.");

            // Treinamento realizado UMA vez, usando somente o conjunto de treino;
            // a mesma evidencia de modelo atende aos dois percursos off/on.
            using var isolationForest = fullEnsemble
                ? await LocalIsolationForestModel.CreateAsync(training)
                : null;
            var detectorSet = fullEnsemble
                ? "Statistical,MAD,EWMA,RateOfChange,Persistence,LevelShift,IsolationForest"
                : "Statistical,MAD,EWMA,RateOfChange,Persistence,LevelShift";

            isolationForest?.ResetCache();
            var off = await ExecuteAsync(training, evaluation, capture: false, isolationForest);
            // Sem limpar o cache, off e on teriam diferencas nas metricas
            // modeloVeioDoCache/modeloVeioDoDisco (ruido, nao decisao).
            isolationForest?.ResetCache();
            var on = await ExecuteAsync(training, evaluation, capture: true, isolationForest);

            if (fullEnsemble)
                VerifyFullEnsemble(on);

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

            var folder = Path.GetFullPath(positional[2]);
            Directory.CreateDirectory(folder);
            var manifestFile = ReplayRunManifest.PrepareOutputManifestPath(folder);
            var file = Path.Combine(folder, "intelligence-lab-replay-report.json");
            var report = new
            {
                Environment = "IsolatedSQLiteInMemory",
                TrainingSource = Path.GetFileName(positional[0]),
                EvaluationSource = Path.GetFileName(positional[1]),
                ClientId,
                TagName,
                DatasetLabeled = false,
                SourceTimestampAssumption = "UTC-03:00 (horarios do Bridge sem offset)",
                DetectorSet = detectorSet,
                IsolationForestEnabled = fullEnsemble,
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
            var artifacts = new List<ReplayManifestArtifact>();
            if (isolationForest is not null)
            {
                var modelOutput = Path.Combine(folder, "intelligence-lab-isolation-forest-model.json");
                File.Copy(isolationForest.ModelPath, modelOutput, overwrite: true);
                var modelArtifact = await DescribeArtifactAsync(modelOutput);
                if (modelArtifact.Sha256 != isolationForest.ModelSha256)
                    throw new InvalidOperationException("O modelo exportado difere do snapshot executado.");
                artifacts.Add(modelArtifact);
                Console.WriteLine($"Modelo IF isolado: {modelOutput} (SHA256: {modelArtifact.Sha256})");
            }
            // Exportacao de evidencias e leituras observadas, sem gabarito industrial.
            if (diagnosticEntries is not null && evaluationReadings is not null &&
                contentHash is not null && replayRunId is not null)
            {
                var diagnosticsFile = Path.Combine(folder, "intelligence-lab-replay-diagnostics.jsonl");
                await ReplayDiagnosticJsonlExporter.WriteAsync(diagnosticsFile, diagnosticEntries);
                artifacts.Add(await DescribeArtifactAsync(diagnosticsFile));
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
                    RunManifestSchemaVersion = ReplayRunManifest.SchemaVersion,
                    RunManifestFileName = "intelligence-lab-run-manifest.json",
                    DetectorSet = detectorSet,
                    Limitations = fullEnsemble
                        ? "Sem rotulos independentes nem composicao operacional completa (memoria, regime, multivariada). " +
                          "Treinamento IF local somente com dados fornecidos; nao calcular TP/FP/FN sem gabarito."
                        : "Sem rotulos independentes, commit/configuracao/modelos completos, IF ou multivariada. " +
                          "Nao calcular TP/FP/FN a partir deste arquivo isoladamente.",
                    Readings = evaluationReadings
                };
                await File.WriteAllTextAsync(exportFile, JsonSerializer.Serialize(exported,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }), Encoding.UTF8);
                artifacts.Add(await DescribeArtifactAsync(exportFile));
                Console.WriteLine($"Diagnosticos completos: {diagnosticsFile}");
                Console.WriteLine($"Leituras para avaliacao (sem gabarito): {exportFile}");
            }

            // So publicar o relatorio do run apos a exportacao de evidencias.
            // Se a escrita JSONL falhar, a execucao encerra com falha e nao
            // deixa um relatorio novo alegando integridade validada.
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            artifacts.Add(await DescribeArtifactAsync(file));

            // O manifesto e publicado por ultimo: ele so pode declarar os hashes
            // de artefatos que ja foram gravados com sucesso, e nunca inclui hash proprio.
            if (diagnosticEntries is not null && evaluationReadings is not null &&
                replayRunId is not null)
            {
                var manifest = ReplayRunManifest.Create(new ReplayRunManifestRequest(
                    replayRunId,
                    executionStartedAtUtc,
                    DateTimeOffset.UtcNow,
                    trainingInput.ManifestInput,
                    evaluationInput.ManifestInput,
                    ReadGitMetadata(),
                    CreateManifestConfiguration(training.Count, evaluation.Count, requestedEvaluationLimit,
                        isolationForest),
                    CreateManifestContracts(),
                    CreateManifestResources(isolationForest),
                    artifacts,
                    ExportsCompleted: true));
                await WriteManifestAtomicallyAsync(manifestFile, ReplayRunManifest.Serialize(manifest));
                Console.WriteLine($"Manifesto reproduzivel: {manifestFile}");
            }

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

    private static async Task<ReplayInput> LoadReadingsAsync(string path)
    {
        var readStartedAtUtc = DateTimeOffset.UtcNow;
        var bytes = await File.ReadAllBytesAsync(path);
        var readCompletedAtUtc = DateTimeOffset.UtcNow;
        // File.ReadAllText removia BOM UTF-8; manter essa compatibilidade sem
        // alterar o hash, que sempre representa todos os bytes lidos do arquivo.
        var jsonOffset = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)
            ? Encoding.UTF8.Preamble.Length
            : 0;
        using var json = JsonDocument.Parse(bytes.AsMemory(jsonOffset));
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
        return new ReplayInput(readings, new ReplayManifestInput(
            Path.GetFileName(path),
            ReplayRunManifest.Sha256Hex(bytes),
            bytes.LongLength,
            readStartedAtUtc,
            readCompletedAtUtc,
            ManifestVerificationStatus.Verified));
    }

    private static async Task<ReplayManifestArtifact> DescribeArtifactAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        return new ReplayManifestArtifact(Path.GetFileName(path),
            ReplayRunManifest.Sha256Hex(bytes), bytes.LongLength,
            ManifestVerificationStatus.Verified);
    }

    private static async Task WriteManifestAtomicallyAsync(string path, string contents)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ??
            throw new InvalidOperationException("Manifesto sem diretorio valido.");
        var temporaryPath = Path.Combine(directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents, Encoding.UTF8);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static ReplayManifestConfiguration CreateManifestConfiguration(
        int trainingReadingCount, int evaluationReadingCount, int? requestedEvaluationLimit,
        LocalIsolationForestModel? isolationForest)
    {
        var options = new AnomalyDetectionOptions();
        var settings = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["ensembleThreshold"] = options.EnsembleThreshold,
            ["minHistoryPoints"] = options.MinHistoryPoints,
            ["zScoreWeight"] = options.ZScoreWeight,
            ["madWeight"] = options.MadWeight,
            ["ewmaWeight"] = options.EwmaWeight,
            ["rateOfChangeWeight"] = options.RateOfChangeWeight,
            ["persistenceWeight"] = options.PersistenceWeight,
            ["levelShiftWeight"] = options.LevelShiftWeight,
            ["zScoreThreshold"] = options.ZScoreThreshold,
            ["madThreshold"] = options.MadThreshold,
            ["ewmaLambda"] = options.EwmaLambda,
            ["ewmaSigmaLimit"] = options.EwmaSigmaLimit,
            ["rateOfChangeThreshold"] = options.RateOfChangeThreshold,
            ["persistenceMinimumPoints"] = options.PersistenceMinimumPoints,
            ["persistenceMinimumMinutes"] = options.PersistenceMinimumMinutes,
            ["experimentalPersistenceTemporalConfirmationEnabled"] = options.ExperimentalPersistenceTemporalConfirmationEnabled,
            ["levelShiftPreviousWindow"] = options.LevelShiftPreviousWindow,
            ["levelShiftRecentWindow"] = options.LevelShiftRecentWindow,
            ["levelShiftThreshold"] = options.LevelShiftThreshold,
            ["levelShiftMinimumConsistency"] = options.LevelShiftMinimumConsistency,
            ["runtimeFunctionalDecisionEnabled"] = false,
            ["runtimeFunctionalDecisionCanaryOnly"] = false,
            ["runtimeFunctionalDecisionAllowedClientCount"] = 0,
            ["diagnosticCaptureComparedOffAndOn"] = true,
            ["diagnosticVersionConfiguration"] = "unlabeled-historical-replay",
            ["tagRepresentation"] = "continuous",
            ["storage"] = "SQLiteInMemory",
            ["sourceTimestampAssumption"] = "UTC-03:00",
            ["replayHistoryMaximumReadings"] = 1440,
            ["maxInputReadings"] = MaxReadings,
            ["trainingReadingsProcessed"] = trainingReadingCount,
            ["evaluationReadingsProcessed"] = evaluationReadingCount,
            ["evaluationLimitArgument"] = requestedEvaluationLimit?.ToString(CultureInfo.InvariantCulture) ?? "NotSpecified"
        };
        var mode = isolationForest is null
            ? "OfflineIsolatedHistoricalReplay"
            : "OfflineIsolatedHistoricalReplayFullEnsemble";
        string[] enabledDetectors = isolationForest is null
            ? ["Statistical", "MAD", "EWMA", "RateOfChange", "Persistence", "LevelShift"]
            : ["Statistical", "MAD", "EWMA", "RateOfChange", "Persistence", "LevelShift", "IsolationForest"];
        string[] disabledResources = isolationForest is null
            ? ["IsolationForest", "Multivariate", "BehaviorMemory", "LLM", "RuntimeFunctionalDecision"]
            : ["Multivariate", "BehaviorMemory", "LLM", "RuntimeFunctionalDecision"];
        if (isolationForest is not null)
        {
            var ifOptions = isolationForest.Options;
            settings["isolationForestTreeCount"] = ifOptions.TreeCount;
            settings["isolationForestSampleSize"] = ifOptions.SampleSize;
            settings["isolationForestMinimumHistoryPoints"] = ifOptions.MinimumHistoryPoints;
            settings["isolationForestFeatureWindowSize"] = ifOptions.FeatureWindowSize;
            settings["isolationForestAnomalyThreshold"] = ifOptions.AnomalyThreshold;
            settings["isolationForestWeight"] = ifOptions.Weight;
            settings["isolationForestRandomSeed"] = ifOptions.RandomSeed;
            settings["isolationForestNormalizationSlope"] = ifOptions.NormalizationSlope;
            settings["isolationForestModelSha256"] = isolationForest.ModelSha256;
        }
        return new ReplayManifestConfiguration(
            mode,
            enabledDetectors,
            disabledResources,
            settings,
            ReplayRunManifest.CreateConfigurationFingerprint(mode, enabledDetectors,
                disabledResources, settings),
            ManifestVerificationStatus.Verified);
    }

    private static IReadOnlyList<ReplayManifestContract> CreateManifestContracts() =>
    [
        new("ReplayReadingsExport", "industrial-replay-readings-export/v1", ManifestVerificationStatus.Verified, "Produced"),
        new("ReplayDiagnosticsExport", ReplayDiagnosticJsonlExporter.SchemaVersion, ManifestVerificationStatus.Verified, "Produced"),
        new("RunManifest", ReplayRunManifest.SchemaVersion, ManifestVerificationStatus.Verified, "Produced"),
        new("OfflineEvaluationPolicy", "industrial-alert-matching/v1", ManifestVerificationStatus.Unknown, "NotExecutedWithoutIndependentLabels")
    ];

    private static IReadOnlyList<ReplayManifestResource> CreateManifestResources(
        LocalIsolationForestModel? isolationForest) =>
    [
        new("StatisticalDetectors", ManifestResourceExecutionStatus.Executed, ManifestVerificationStatus.Verified,
            "Six in-process detectors instantiated from effective settings; no external model artifact loaded."),
        new("TagProfile", ManifestResourceExecutionStatus.Executed, ManifestVerificationStatus.Verified,
            "Trained in memory from the training input during this execution; no persisted model artifact loaded."),
        isolationForest is null
            ? new ReplayManifestResource("IsolationForest", ManifestResourceExecutionStatus.NotExecuted,
                ManifestVerificationStatus.Verified, "Explicitly not instantiated by this replay.")
            : new ReplayManifestResource("IsolationForest", ManifestResourceExecutionStatus.Executed,
                ManifestVerificationStatus.Verified,
                "Model trained locally once from the training input with fixed seed; same snapshot used off/on.",
                isolationForest.ModelSha256),
        new("Multivariate", ManifestResourceExecutionStatus.NotExecuted, ManifestVerificationStatus.Verified,
            "Explicitly not instantiated by this replay."),
        new("BehaviorMemory", ManifestResourceExecutionStatus.NotExecuted, ManifestVerificationStatus.Verified,
            "No behavior-memory provider is configured."),
        new("LLM", ManifestResourceExecutionStatus.NotExecuted, ManifestVerificationStatus.Verified,
            "Replay interpretation is local and does not invoke an LLM."),
        new("RuntimeFunctionalDecision", ManifestResourceExecutionStatus.NotExecuted, ManifestVerificationStatus.Verified,
            "Runtime feature flag is false; legacy decision path is preserved.")
    ];

    private static ReplayManifestGit ReadGitMetadata()
        => ReplayGitMetadataResolver.Read([
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            typeof(IntelligenceLabReplay).Assembly.Location
        ]);

    private static async Task<ReplayRun> ExecuteAsync(
        List<LeituraBruta> training, List<LeituraBruta> evaluation, bool capture,
        LocalIsolationForestModel? isolationForest)
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
        var detectors = new List<IAnomalyDetector>
        {
            new StatisticalDetector(detectionOptions),
            new MadDetector(detectionOptions),
            new EwmaDetector(detectionOptions),
            new RateOfChangeDetector(detectionOptions),
            new PersistenceDetector(detectionOptions),
            new LevelShiftDetector(detectionOptions)
        };
        if (isolationForest is not null)
            detectors.Add(isolationForest.CreateDetector());
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

    // O modo de sete detectores falha em vez de produzir um manifesto que
    // alegue participacao do IF quando houve bloqueio ou modelo ausente.
    private static void VerifyFullEnsemble(ReplayRun run)
    {
        foreach (var (diagnostic, index) in run.Diagnostics.Select((item, index) => (item, index)))
        {
            var ensemble = diagnostic.Ensemble ??
                throw new InvalidOperationException($"Ensemble ausente na leitura {index + 1}.");
            var contributions = ensemble.Contributions ??
                throw new InvalidOperationException($"Contribuicoes ausentes na leitura {index + 1}.");
            if (contributions.Count != 7)
                throw new InvalidOperationException($"Esperadas 7 contribuicoes na leitura {index + 1}.");
            var isolationForest = contributions.SingleOrDefault(item => item.Detector == "Isolation Forest");
            if (isolationForest is null || isolationForest.Executed != true ||
                isolationForest.ExecutionCompleted != true || isolationForest.Applicable != true ||
                isolationForest.ParticipatedInAggregation != true ||
                isolationForest.EffectiveWeight is not > 0)
                throw new InvalidOperationException($"Isolation Forest nao participou da leitura {index + 1}.");
            var denominator = ensemble.EffectiveWeightSum;
            if (!double.IsFinite(denominator) || denominator <= 0)
                throw new InvalidOperationException($"Peso efetivo invalido na leitura {index + 1}.");
            var score = contributions.Sum(item => item.WeightedNumeratorContribution ?? 0d) /
                denominator;
            if (Math.Abs(score - ensemble.NormalizedScore) > 0.000001)
                throw new InvalidOperationException($"Score nao reconstruivel na leitura {index + 1}.");
        }
    }

    private sealed class LocalIsolationForestModel : IDisposable
    {
        private readonly string _root;
        private readonly IsolationForestPersistenceService _persistence;
        public IsolationForestOptions Options { get; }
        public string ModelPath { get; }
        public string ModelSha256 { get; private set; } = "";

        private LocalIsolationForestModel(string root)
        {
            _root = root;
            Options = new IsolationForestOptions
            {
                Weight = 0.25,
                TreeCount = 16,
                SampleSize = 32,
                MinimumHistoryPoints = 50,
                FeatureWindowSize = 10,
                AnomalyThreshold = 0.62,
                RandomSeed = 42
            };
            _persistence = new IsolationForestPersistenceService(new ReplayWebHostEnvironment(root));
            ModelPath = _persistence.ObterCaminhoModelo(ClientId, TagName);
        }

        public static async Task<LocalIsolationForestModel> CreateAsync(List<LeituraBruta> training)
        {
            var root = Path.Combine(Path.GetTempPath(), "IAApi-IntelligenceLab-FullEnsemble",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var session = new LocalIsolationForestModel(root);
            try
            {
                await session.TrainAsync(training);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        private async Task TrainAsync(List<LeituraBruta> training)
        {
            if (training.Count < Options.MinimumHistoryPoints)
                throw new InvalidOperationException(
                    $"Full ensemble requer ao menos {Options.MinimumHistoryPoints} pontos de treino.");
            var history = training.Select(item => new HistoricoDetectorPoint
            {
                DataHora = item.DataHora,
                Valor = item.Valor
            }).ToArray();
            var values = history.Select(item => item.Valor).ToArray();
            var mean = values.Average();
            var std = Math.Sqrt(values.Average(value => Math.Pow(value - mean, 2)));
            var input = new DetectorInput
            {
                ClienteId = ClientId,
                TagName = TagName,
                TipoTag = "continuous",
                ValorAtual = values[^1],
                DataHoraAtual = history[^1].DataHora,
                MediaHistorica = mean,
                DesvioPadraoHistorico = std,
                Historico = history
            };
            var extractor = new IsolationForestFeatureExtractor();
            var samples = extractor.ExtrairTreinamento(input, Options.FeatureWindowSize)
                .Select(feature => (IReadOnlyList<double>)feature.ParaVetor().ToArray())
                .ToList();
            if (samples.Count < Math.Max(10, Options.MinimumHistoryPoints - Options.FeatureWindowSize))
                throw new InvalidOperationException("Nao ha janelas suficientes para treinar o Isolation Forest.");
            var model = new IsolationForestModel(Options.TreeCount, Options.SampleSize,
                Options.RandomSeed);
            model.Treinar(samples);
            var snapshot = model.ExportarSnapshot(ClientId, TagName,
                Options.FeatureWindowSize, Options.AnomalyThreshold, training[^1].DataHora);
            snapshot.VersaoModelo = 1;
            snapshot.QuantidadePontosTreinamento = training.Count;
            await _persistence.SalvarAsync(snapshot);
            ModelSha256 = ReplayRunManifest.Sha256Hex(await File.ReadAllBytesAsync(ModelPath));
        }

        public IsolationForestDetector CreateDetector() => new(
            Microsoft.Extensions.Options.Options.Create(Options),
            new IsolationForestFeatureExtractor(), _persistence);

        public void ResetCache() => IsolationForestDetector.RemoverDoCache(ClientId, TagName);

        public void Dispose()
        {
            ResetCache();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class ReplayWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "IAApi.IntelligenceLabReplay";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record ReplayRun(
        List<ReplayRecord> Records, int AnomalyCount, int PersistedCount,
        int DiagnosticCount, long CaptureAttempts, long StoredTotal,
        long CaptureFailures, long EvictedTotal,
        IReadOnlyList<IntelligenceLabReadingDiagnostic> Diagnostics);

    private sealed record ReplayInput(List<LeituraBruta> Readings,
        ReplayManifestInput ManifestInput);

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
