using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.ModelTraining;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services;
using IAApi.Infrastructure.Llm.Providers;
using IAApi.Infrastructure.Llm.Configuration;
using IAApi.Benchmark;
using IAApi.Benchmark.Tests;
using IAApi.Benchmark.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Benchmark.Diagnostics;
using IAApi.Benchmark.RegimeShadow;
using IAApi.Benchmark.PersistenceCounterfactual;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Api;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.ReadModels;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using IAApi.Benchmark.Multivariate;
using IAApi.Services.AnomalyDetection.Runtime.Regime;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using IAApi.Benchmark.Cli;


var commandSelection = BenchmarkCommandSelection.Parse(args);

var usarGruposMultivariadosDoBenchmark =
    commandSelection.UsarGruposMultivariadosDoBenchmark;

var builder = WebApplication.CreateBuilder(args);

if (commandSelection.PersistenceTemporalExperiment)
{
    builder.Configuration[
        "AnomalyDetection:ExperimentalPersistenceTemporalConfirmationEnabled"] = "true";
}

// Controllers da API
builder.Services.AddControllers();

// CORS para permitir o front React
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Banco SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=ia_config.db"));

// Serviços gerais
builder.Services.AddSingleton<
    IHistoricoService,
    BridgeHistoricoService>();

builder.Services.AddScoped<HistoricoLimpezaService>();
builder.Services.AddScoped<PerfilTagService>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<BridgeTagsService>();
builder.Services.AddScoped<AnaliseRelacionadasService>();
builder.Services.AddScoped<BridgeValorAtualService>();
builder.Services.AddScoped<AnomaliaHistoricoService>();
builder.Services.AddScoped<RiskScoreService>();
builder.Services.AddScoped<ExplicacaoRiscoService>();
builder.Services.AddScoped<TendenciaRiscoService>();
builder.Services.AddScoped<TendenciaValorService>();
builder.Services.AddScoped<InterpretacaoDependenciasService>();
builder.Services.AddScoped<ProcessoAnaliseService>();
builder.Services.AddScoped<ProcessoInteligenciaService>();
builder.Services.AddScoped<ProcessoHealthScoreService>();
builder.Services.AddScoped<InterpretacaoProcessoService>();
builder.Services.AddScoped<FailurePredictionService>();
builder.Services.AddScoped<EquipamentoInteligenciaService>();
builder.Services.AddScoped<FeedbackAnomaliaAnaliseService>();
builder.Services.AddScoped<OperationalRegimeHistoryResolver>();
builder.Services.AddScoped<TagProfileStatisticsCalculator>();
builder.Services.AddScoped<OperationalRegimeBaselineService>();
builder.Services.AddScoped<OperationalRegimeBaselineProvider>();

// B3.25 — descoberta e memória comportamental.
builder.Services.AddBehaviorMemoryDiscovery();

builder.Services.AddHostedService<MonitoramentoTagsService>();
builder.Services.AddSingleton<EnsembleV2Simulator>();
builder.Services.AddSingleton<AdaptiveEnsembleLaboratory>();
builder.Services.AddSingleton<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeDecisionMetrics>();

builder.Services.Configure<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeDecisionMetricsEndpointOptions>(
    builder.Configuration.GetSection(
        IAApi.Services.AnomalyDetection.Runtime.RuntimeDecisionMetricsEndpointOptions.SectionName)
);

builder.Services.Configure<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutReadinessOptions>(
    builder.Configuration.GetSection(
        IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutReadinessOptions.SectionName)
);

builder.Services.AddSingleton<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutReadinessEvaluator>();

    builder.Services.AddSingleton<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutSnapshotFactory>();

builder.Services.AddScoped<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutSnapshotService>();

    builder.Services.AddScoped<
    MultivariateCorrelationDiagnosticService>();

builder.Services.Configure<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutSnapshotPersistenceOptions>(
    builder.Configuration.GetSection(
        IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutSnapshotPersistenceOptions.SectionName)
);

builder.Services.AddScoped<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutEvolutionService>();

builder.Services.AddScoped<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutHistoryService>();

builder.Services.AddHostedService<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeRolloutSnapshotHostedService>();

// Configuração dos detectores de anomalia
builder.Services.Configure<AnomalyDetectionOptions>(
    builder.Configuration.GetSection("AnomalyDetection")
);

// B3.18 — runtime operational decision.
// Ausência da seção mantém Enabled = false e comportamento Legacy.
builder.Services.Configure<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeAnomalyDecisionOptions>(
    builder.Configuration.GetSection(
        IAApi.Services.AnomalyDetection.Runtime.RuntimeAnomalyDecisionOptions.SectionName)
);

builder.Services.AddSingleton<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeAnomalyDecisionResolver>();

builder.Services.AddSingleton<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeFunctionalDecisionCandidateFactory>();

builder.Services.AddSingleton<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeFunctionalDecisionStateStore>();

builder.Services.AddSingleton<
    IAApi.Services.AnomalyDetection.Runtime.RuntimeFunctionalDecisionContextProvider>();

builder.Services.AddSingleton<TagBehaviorProfileResolver>();
builder.Services.AddSingleton<DetectorApplicabilityPolicy>();

builder.Services.AddScoped<
    IAnomalyDetector,
    StatisticalDetector>();

builder.Services.AddScoped<
    IAnomalyDetector,
    MadDetector>();

builder.Services.AddScoped<
    IAnomalyDetector,
    EwmaDetector>();

builder.Services.AddScoped<
    IAnomalyDetector,
    RateOfChangeDetector>();

builder.Services.AddScoped<
    IAnomalyDetector,
    PersistenceDetector>();

builder.Services.AddScoped<
    IAnomalyDetector,
    LevelShiftDetector>();

builder.Services.AddScoped<EnsembleDetectionService>();

builder.Services.AddSingleton<
    IValidateOptions<AnomalyDetectionOptions>,
    AnomalyDetectionOptionsValidator>();

builder.Services.AddSingleton<
    IValidateOptions<IsolationForestOptions>,
    IsolationForestOptionsValidator>();

// Isolation Forest
builder.Services.Configure<IsolationForestOptions>(
    builder.Configuration.GetSection(
        "AnomalyDetection:IsolationForest"
    )
);

builder.Services.AddScoped<
    IsolationForestFeatureExtractor>();

builder.Services.AddScoped<
    IAnomalyDetector,
    IsolationForestDetector>();

builder.Services.AddSingleton<
    IsolationForestPersistenceService>();

builder.Services.AddScoped<DadosTreinamentoTagService>();
builder.Services.AddScoped<ModeloTagStatusService>();

builder.Services.AddScoped<
    IsolationForestTrainingService>();

// Configuração OpenAI
builder.Services.Configure<OpenAiOptions>(
    builder.Configuration.GetSection("OpenAI")
);

builder.Services.AddScoped<MultivariateBufferTester>();
builder.Services.AddScoped<MultivariateDatasetTester>();
builder.Services.AddScoped<MultivariateDetectorTester>();

// Configuração do LLM
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection(
        LlmOptions.SectionName
    )
);

// Configuração do benchmark
var benchmarkSection =
    builder.Configuration.GetRequiredSection(
        BenchmarkOptions.SectionName
    );

var benchmarkOptions =
    benchmarkSection.Get<BenchmarkOptions>()
    ?? throw new InvalidOperationException(
        "Não foi possível carregar a configuração Benchmark."
    );

var executarAdaptiveEnsembleLab =
    commandSelection.ExecutarAdaptiveEnsembleLab;

    Console.WriteLine(
    $"CleanDatabasePath carregado: " +
    $"{benchmarkOptions.CleanDatabasePath}"
);

Console.WriteLine(
    $"AnomalyDatabasePath carregado: " +
    $"{benchmarkOptions.AnomalyDatabasePath}"
);

builder.Services.AddSingleton(benchmarkOptions);

builder.Services.AddSingleton<SQLiteHistoricoService>(
    _ => new SQLiteHistoricoService(
        benchmarkOptions.HistoryWindowSize
    )
);

builder.Services.AddScoped<BenchmarkRunner>();
builder.Services.AddSingleton<BenchmarkEvaluator>();
builder.Services.AddSingleton<CleanBenchmarkEvaluator>();
builder.Services.AddSingleton<CalibrationInvestigator>();
builder.Services.AddSingleton<CalibrationAllInvestigator>();
builder.Services.AddSingleton<DeadbandImpactEvaluator>();
builder.Services.AddSingleton<ResultsAnalyzer>();
builder.Services.AddSingleton<DetectorSensitivityReport>();
builder.Services.AddSingleton<FamilyConsensusReport>();
builder.Services.AddSingleton<
    FalsePositiveRootCauseAnalyzer>();

// Experimental Incident API: disabled by default and strictly read-only.
builder.Services.Configure<ExperimentalIncidentApiOptions>(
    builder.Configuration.GetSection(ExperimentalIncidentApiOptions.SectionName));
builder.Services.AddSingleton<IIncidentReadModelStore, InMemoryIncidentReadModelStore>();
builder.Services.AddSingleton<IncidentReadModelSnapshotLoader>();
builder.Services.AddSingleton<IExperimentalIncidentQueryService,
    ExperimentalIncidentQueryService>();


// Provedor do LM Studio no servidor
builder.Services.AddHttpClient<
    ILlmProvider,
    LmStudioLlmProvider>(
    (serviceProvider, httpClient) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<LlmOptions>>()
            .Value;

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "A configuração Llm:BaseUrl não foi informada."
            );
        }

        httpClient.BaseAddress =
            new Uri(options.BaseUrl);

        httpClient.Timeout =
            TimeSpan.FromSeconds(
                options.TimeoutSegundos
            );
    }
);

// Serviços de interpretação
builder.Services.AddScoped<
    RuleBasedAnomalyInterpretationService>();

builder.Services.AddScoped<
    LocalLlmAnomalyInterpretationService>();

builder.Services.AddScoped<
    IAnomalyInterpretationService>(
    serviceProvider =>
    {
        var configuration =
            serviceProvider.GetRequiredService<
                IConfiguration>();

        var llmHabilitado =
            configuration.GetValue<bool>(
                "Llm:Habilitado"
            );

        return llmHabilitado
            ? serviceProvider.GetRequiredService<
                LocalLlmAnomalyInterpretationService>()
            : serviceProvider.GetRequiredService<
                RuleBasedAnomalyInterpretationService>();
    }
);

builder.Services.AddScoped<TagContextoIaService>();

builder.Services.AddScoped<
    AnomalyInterpretationContextBuilder>();

builder.Services.AddScoped<GrupoAnaliseService>();

builder.Services.AddScoped<
    OperationalMultivariateTrainingService>();

builder.Services.AddScoped<GrupoAnaliseCadastroService>();

builder.Services.AddScoped<GrupoAnaliseTester>();

builder.Services.AddScoped<
    MultivariateTrainingService>();

builder.Services.AddScoped<
    MultivariateGroupBufferService>();

builder.Services.AddSingleton<
    MultivariateRuntimeStateStore>();

builder.Services.AddSingleton<
    MultivariateModelPersistenceService>();

// B3.24.2 / MV08.2C — backtest histórico multivariado diagnóstico.
// Somente leitura/análise; não altera modelo, runtime, readiness ou promoção.
builder.Services.AddSingleton<
    MultivariateHistoricalBacktestAnalyzer>();

builder.Services.AddScoped<
    OperationalMultivariateBacktestService>();

// B3.24.2 / MV08.4B — qualificação operacional multivariada.
// Executa avaliação técnica e de governança; não altera ApprovedGroups,
// promoção ou RuntimeDecision.
builder.Services.AddSingleton<
    MultivariateOperationalQualificationEvaluator>();

builder.Services.AddScoped<
    OperationalMultivariateQualificationService>();

// B3.24.2 / MV08.4D1 — armazenamento auditável das revisões
// industriais por evento. Não participa da decisão operacional.
builder.Services.AddScoped<
    MultivariateIndustrialEventReviewStore>();

builder.Services.AddSingleton<
    MultivariateIndustrialReviewAssessmentService>();

// B3.24.2 / MV08.4D3 — fila de revisão industrial.
// Somente consulta/organização da evidência e das revisões persistidas.
// Não concede autoridade operacional ao grupo.
builder.Services.AddSingleton<
    MultivariateIndustrialReviewQueueBuilder>();

builder.Services.AddScoped<
    IMultivariateIndustrialReviewQueueService,
    MultivariateIndustrialReviewQueueService>();

// B3.24.2 / MV07 — readiness operacional multivariado.
// Avalia se grupo e modelo estão aptos a receber autoridade operacional.
// Não cria anomalias por conta própria, mas funciona como gate de segurança
// quando uma promoção multivariada é considerada.
builder.Services.Configure<
    MultivariateOperationalReadinessOptions>(
    builder.Configuration.GetSection(
        "AnomalyDetection:MultivariateOperationalReadiness"));

builder.Services.AddSingleton<
    MultivariateOperationalReadinessEvaluator>();

builder.Services.AddScoped<
    MultivariateOperationalReadinessService>();

builder.Services.AddScoped<
    MultivariateDetector>();


// B3.24.2 / MV06 — avaliação counterfactual da decisão multivariada.
// Permite comparar o efeito da evidência multivariada sem conceder
// automaticamente autoridade operacional ao grupo.
builder.Services.Configure<
    MultivariateDecisionFusionOptions>(
    builder.Configuration.GetSection(
        MultivariateDecisionFusionOptions.SectionName));

builder.Services.AddSingleton<
    MultivariateDecisionCounterfactualEvaluator>();

builder.Services.AddScoped<
    MultivariateDecisionCounterfactualObserver>();

// B3.24.2 / MV06.3 — promoção operacional multivariada.
// A autoridade real possui feature flag própria, passa pelos gates
// de segurança/readiness e permanece desligada por padrão.
builder.Services.Configure<
    MultivariateOperationalPromotionOptions>(
    builder.Configuration.GetSection(
        MultivariateOperationalPromotionOptions.SectionName));

builder.Services.AddSingleton<
    MultivariateOperationalFusionResolver>();

builder.Services.AddSingleton<
    MultivariateOperationalInterpretationFactory>();


// B3.24.2 / MV01 — inferência multivariada operacional.
// O coordinator executa a análise multivariada e mantém o estado necessário.
// A autoridade sobre a decisão final não é definida aqui: ela é controlada
// posteriormente pelas políticas de promoção, readiness e fusão operacional.
builder.Services.AddScoped<
    MultivariateShadowRuntimeCoordinator>();

if (usarGruposMultivariadosDoBenchmark)
{
    builder.Services.AddScoped<
        IGrupoAnaliseProvider,
        JsonGrupoAnaliseProvider>();
}
else
{
    builder.Services.AddScoped<
        IGrupoAnaliseProvider,
        DatabaseGrupoAnaliseProvider>();
}

    builder.Services.Configure<ConfidenceEngineOptions>(
    builder.Configuration.GetSection("ConfidenceEngine"));

builder.Services.AddSingleton<ConfidenceEngine>();
builder.Services.AddSingleton<LegacyDetectorResultV2Adapter>();
builder.Services.AddSingleton<EvidenceBuilder>();
builder.Services.AddSingleton<ConfidenceBenchmarkAnalyzer>();
builder.Services.AddSingleton<ConfidenceBenchmarkReportWriter>();
builder.Services.AddSingleton<ConfidenceEngineBenchmarkRunner>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var experimentalIncidentApiOptions = app.Services
    .GetRequiredService<IOptions<ExperimentalIncidentApiOptions>>().Value;
if (experimentalIncidentApiOptions.Enabled &&
    !string.IsNullOrWhiteSpace(experimentalIncidentApiOptions.SnapshotPath))
{
    try
    {
        var snapshotPath = Path.IsPathRooted(experimentalIncidentApiOptions.SnapshotPath)
            ? experimentalIncidentApiOptions.SnapshotPath
            : Path.Combine(Directory.GetCurrentDirectory(),
                experimentalIncidentApiOptions.SnapshotPath);
        var loader = app.Services.GetRequiredService<IncidentReadModelSnapshotLoader>();
        var store = app.Services.GetRequiredService<IIncidentReadModelStore>();
        var loaded = await loader.LoadAsync(snapshotPath, store);
        app.Logger.LogInformation(
            "Experimental Incident API loaded {IncidentCount} read models from {SnapshotPath}.",
            loaded, snapshotPath);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex,
            "Experimental Incident API snapshot load failed; API remains isolated and empty.");
    }
}


var baselineDiagnosticOptions =
    BaselineDiagnosticOptions.Parse(args);

var regimeShadowOptions = RegimeShadowOptions.Parse(args);

var detectorOptionsForDualBaseline = builder.Configuration
    .GetRequiredSection("AnomalyDetection")
    .Get<AnomalyDetectionOptions>() ?? new AnomalyDetectionOptions();

var dualBaselineShadowOptions =
    DualBaselineShadowOptions.Parse(args, detectorOptionsForDualBaseline);

var behaviorProfileObservabilityOptions =
    BenchmarkBehaviorProfileOptions.Parse(args);

var persistenceCounterfactualOptions =
    PersistenceCounterfactualOptions.Parse(args);

var experimentalIncidentOptions = builder.Configuration
    .GetSection("ExperimentalIncidentLayer")
    .Get<IAApi.Benchmark.ExperimentalIncidents.ExperimentalIncidentOptions>()
    ?? new IAApi.Benchmark.ExperimentalIncidents.ExperimentalIncidentOptions();
if (IAApi.Benchmark.ExperimentalIncidents.ExperimentalIncidentOptions.Parse(args).Enabled)
    experimentalIncidentOptions = experimentalIncidentOptions with { Enabled = true };

    var experimentalAnomalyDecisionOptions =
    ExperimentalAnomalyDecisionOptions.Parse(args);

var functionalOperationalCanaryOptions =
    new FunctionalOperationalCanaryOptions
    {
        Enabled =
            commandSelection.FunctionalOperationalCanaryEnabled,
        AllowedTags =
            commandSelection.FunctionalOperationalCanaryTags,
        AllowedFastWindows =
            commandSelection.FunctionalOperationalCanaryWindows
    };

BenchmarkCommandValidator.Validate(
    commandSelection,
    baselineDiagnosticOptions,
    regimeShadowOptions,
    dualBaselineShadowOptions,
    behaviorProfileObservabilityOptions,
    persistenceCounterfactualOptions,
    experimentalIncidentOptions,
    experimentalAnomalyDecisionOptions);

var functionalValidationMessages =
    BenchmarkCommandValidator.ValidateFunctional(
        commandSelection,
        experimentalAnomalyDecisionOptions);

if (functionalValidationMessages.Length > 0)
{
    foreach (var message in functionalValidationMessages)
    {
        Console.WriteLine(message);
    }

    return;
}

var indiceEvaluate =
    commandSelection.IndiceEvaluate;

var executarAvaliacao =
    commandSelection.ExecutarAvaliacao;

var executarAnaliseCausaRaiz =
    commandSelection.ExecutarAnaliseCausaRaiz;

var executarAnaliseResultados =
    commandSelection.ExecutarAnaliseResultados;

var executarAnaliseSensibilidadeDetectores =
    commandSelection.ExecutarAnaliseSensibilidadeDetectores;

var executarFamilyConsensusReport =
    commandSelection.ExecutarFamilyConsensusReport;

var executarConfidenceBenchmark =
    commandSelection.ExecutarConfidenceBenchmark;

var quantidadeModosSelecionados =
    commandSelection.QuantidadeModosSelecionados;

if (quantidadeModosSelecionados > 1)
{
    Console.WriteLine();
    Console.WriteLine(
        "Use apenas um modo por execução: --benchmark, --benchmark-clean, --benchmark-fast, --benchmark-clean-fast, --evaluate, --evaluate-functional-oracle, --evaluate-clean, --investigate-clean-all, --test-groups, --test-multivariate-buffer, --test-multivariate-dataset, --evaluate-deadband-impact, --test-multivariate-detector, --analyze-results, --analyze-detector-sensitivity, --adaptive-ensemble-lab, --family-consensus-report, --confidence-benchmark ou --train-multivariate."
    );

    return;
}

if (await BenchmarkUtilityCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection))
{
    return;
}

if (await BenchmarkRunCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection))
{
    return;
}

if (await BenchmarkFastCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        baselineDiagnosticOptions,
        regimeShadowOptions,
        dualBaselineShadowOptions,
        behaviorProfileObservabilityOptions,
        persistenceCounterfactualOptions,
        experimentalIncidentOptions,
        experimentalAnomalyDecisionOptions,
        functionalOperationalCanaryOptions))
{
    return;
}

if (await BenchmarkDeadbandImpactCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        benchmarkOptions))
{
    return;
}

if (await BenchmarkCleanInvestigationCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        benchmarkOptions))
{
    return;
}

if (await BenchmarkCleanEvaluationCommandExecutor.TryExecuteAsync(
        app.Services,
        args,
        commandSelection))
{
    return;
}

if (await BenchmarkFunctionalOracleEvaluationCommandExecutor.TryExecuteAsync(
        args,
        commandSelection))
{
    return;
}

if (executarAvaliacao)
{
    Console.WriteLine();
    Console.WriteLine(
        "API iniciada em modo de avaliação do benchmark."
    );

    var argumentosAvaliacao = args
        .Skip(indiceEvaluate + 1)
        .ToArray();

    var diretorioProjeto = Directory.GetCurrentDirectory();
    var diretorioResultadosPadrao = Path.Combine(
        diretorioProjeto,
        "Results"
    );

    var oraclePadrao = Path.Combine(
        diretorioProjeto,
        "Benchmark",
        "oracle.json"
    );

    string detectionsPath;
    string errorsPath;
    string oraclePath;
    string outputDirectory;

    if (argumentosAvaliacao.Length == 0)
    {
        if (!Directory.Exists(diretorioResultadosPadrao))
        {
            throw new DirectoryNotFoundException(
                $"Diretório de resultados não encontrado: {diretorioResultadosPadrao}"
            );
        }

        var arquivoDeteccoesMaisRecente = new DirectoryInfo(
            diretorioResultadosPadrao
        )
            .GetFiles("detections_*.jsonl")
            .OrderByDescending(arquivo => arquivo.LastWriteTimeUtc)
            .FirstOrDefault();

        if (arquivoDeteccoesMaisRecente is null)
        {
            throw new FileNotFoundException(
                $"Nenhum arquivo detections_*.jsonl foi encontrado em {diretorioResultadosPadrao}."
            );
        }

        detectionsPath = arquivoDeteccoesMaisRecente.FullName;

        var sufixoExecucao = Path.GetFileNameWithoutExtension(
            arquivoDeteccoesMaisRecente.Name
        )
            .Replace(
                "detections_",
                string.Empty,
                StringComparison.OrdinalIgnoreCase
            );

        errorsPath = Path.Combine(
            diretorioResultadosPadrao,
            $"errors_{sufixoExecucao}.jsonl"
        );

        oraclePath = oraclePadrao;
        outputDirectory = diretorioResultadosPadrao;
    }
    else if (argumentosAvaliacao.Length == 4)
    {
        detectionsPath = argumentosAvaliacao[0];
        errorsPath = argumentosAvaliacao[1];
        oraclePath = argumentosAvaliacao[2];
        outputDirectory = argumentosAvaliacao[3];
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine(
            "Uso automático:"
        );
        Console.WriteLine(
            "  dotnet run -c Release -- --evaluate"
        );
        Console.WriteLine();
        Console.WriteLine(
            "Uso com caminhos informados manualmente:"
        );
        Console.WriteLine(
            "  dotnet run -c Release -- --evaluate <detections.jsonl> <errors.jsonl> <oracle.json> <diretório-saída>"
        );

        return;
    }

    if (!File.Exists(detectionsPath))
    {
        throw new FileNotFoundException(
            "Arquivo de detecções não encontrado.",
            detectionsPath
        );
    }

    if (!File.Exists(errorsPath))
    {
        throw new FileNotFoundException(
            "Arquivo de erros não encontrado.",
            errorsPath
        );
    }

    if (!File.Exists(oraclePath))
    {
        throw new FileNotFoundException(
            "Arquivo oracle.json não encontrado.",
            oraclePath
        );
    }

    Directory.CreateDirectory(outputDirectory);

    Console.WriteLine();
    Console.WriteLine($"Detecções: {detectionsPath}");
    Console.WriteLine($"Erros:     {errorsPath}");
    Console.WriteLine($"Oráculo:   {oraclePath}");
    Console.WriteLine($"Saída:     {outputDirectory}");
    Console.WriteLine();

    using var scope = app.Services.CreateScope();

    var evaluator =
        scope.ServiceProvider
            .GetRequiredService<BenchmarkEvaluator>();

    await evaluator.EvaluateAsync(
        detectionsPath,
        errorsPath,
        oraclePath,
        outputDirectory
    );

    Console.WriteLine();
    Console.WriteLine(
        "Avaliação concluída. Confira os relatórios gerados na pasta de saída."
    );

    return;

    
}




if (executarAnaliseSensibilidadeDetectores)
{
    Console.WriteLine();
    Console.WriteLine(
        "API iniciada em modo de análise " +
        "de sensibilidade dos detectores."
    );

    var diretorioResultados =
        Path.IsPathRooted(
            benchmarkOptions.ResultDirectory)
            ? benchmarkOptions.ResultDirectory
            : Path.Combine(
                Directory.GetCurrentDirectory(),
                benchmarkOptions.ResultDirectory);

    using var scope =
        app.Services.CreateScope();

    var report =
        scope.ServiceProvider
            .GetRequiredService<
                DetectorSensitivityReport>();

    await report.AnalyzeAsync(
        diretorioResultados);

    return;
}

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("FrontendCors");

app.MapControllers();

if (executarAnaliseResultados)
{
    Console.WriteLine();
    Console.WriteLine(
        "API iniciada em modo de análise da pasta Results."
    );

    var diretorioResultados =
        Path.IsPathRooted(
            benchmarkOptions.ResultDirectory)
            ? benchmarkOptions.ResultDirectory
            : Path.Combine(
                Directory.GetCurrentDirectory(),
                benchmarkOptions.ResultDirectory);

    using var scope =
        app.Services.CreateScope();

    var analyzer =
        scope.ServiceProvider
            .GetRequiredService<ResultsAnalyzer>();

    await analyzer.AnalyzeAsync(
        diretorioResultados);

    return;
}


if (executarAdaptiveEnsembleLab)
{
    Console.WriteLine();
    Console.WriteLine(
        "API iniciada em modo Adaptive Ensemble Laboratory."
    );

    var diretorioResultados =
        Path.IsPathRooted(
            benchmarkOptions.ResultDirectory)
            ? benchmarkOptions.ResultDirectory
            : Path.Combine(
                Directory.GetCurrentDirectory(),
                benchmarkOptions.ResultDirectory);

    using var scope =
        app.Services.CreateScope();

    var laboratory =
        scope.ServiceProvider
            .GetRequiredService<
                AdaptiveEnsembleLaboratory>();

    await laboratory.RunAsync(
        diretorioResultados);

    return;
}

if (executarFamilyConsensusReport)
{
    Console.WriteLine();
    Console.WriteLine(
        "API iniciada em modo Family Consensus Report."
    );

    var diretorioResultados =
        Path.IsPathRooted(
            benchmarkOptions.ResultDirectory)
            ? benchmarkOptions.ResultDirectory
            : Path.Combine(
                Directory.GetCurrentDirectory(),
                benchmarkOptions.ResultDirectory);

    using var scope =
        app.Services.CreateScope();

    var report =
        scope.ServiceProvider
            .GetRequiredService<FamilyConsensusReport>();

    await report.RunAsync(
        diretorioResultados);

    return;
}

if (executarConfidenceBenchmark)
{
    Console.WriteLine();
    Console.WriteLine(
        "API iniciada em modo Confidence Engine Benchmark."
    );

    var diretorioResultados =
        Path.IsPathRooted(
            benchmarkOptions.ResultDirectory)
            ? benchmarkOptions.ResultDirectory
            : Path.Combine(
                Directory.GetCurrentDirectory(),
                benchmarkOptions.ResultDirectory);

    using var scope =
        app.Services.CreateScope();

    var runner =
        scope.ServiceProvider
            .GetRequiredService<ConfidenceEngineBenchmarkRunner>();

    await runner.RunAsync(
        diretorioResultados);

    return;
}
app.Run();