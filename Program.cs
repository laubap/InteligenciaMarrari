using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services;
using IAApi.Infrastructure.Llm.Providers;
using IAApi.Infrastructure.Llm.Configuration;
using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Benchmark.Cli;
using IAApi.Benchmark.DependencyInjection;
using IAApi.Services.AnomalyDetection.Multivariate.DependencyInjection;
using IAApi.Services.AnomalyDetection.Confidence.DependencyInjection;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.DependencyInjection;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Bootstrap;
using IAApi.Infrastructure.Llm.DependencyInjection;
using IAApi.Services.AnomalyDetection.Interpretation.DependencyInjection;
using IAApi.Services.AnomalyDetection.DependencyInjection;
using IAApi.Services.ModelTraining.DependencyInjection;

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

builder.Services.AddLegacyAnomalyDetectionServices();

builder.Services.AddSingleton<
    IValidateOptions<AnomalyDetectionOptions>,
    AnomalyDetectionOptionsValidator>();

// Isolation Forest
builder.Services.AddIsolationForestServices(
    builder.Configuration);

builder.Services.AddModelTrainingServices();

// Configuração OpenAI
builder.Services.Configure<OpenAiOptions>(
    builder.Configuration.GetSection("OpenAI")
);

builder.Services.AddLlmConfiguration(
    builder.Configuration);

// Configuração do benchmark
var benchmarkOptions =
    builder.Services.AddBenchmarkConfiguration(
        builder.Configuration);

builder.Services.AddBenchmarkCoreServices();
builder.Services.AddBenchmarkLaboratoryServices();
builder.Services.AddBenchmarkMultivariateServices();

// Experimental Incident API: disabled by default and strictly read-only.
builder.Services.AddExperimentalIncidentApiServices(
    builder.Configuration);

// Provedor do LM Studio no servidor
builder.Services.AddLmStudioProvider();

// Serviços de interpretação
builder.Services.AddAnomalyInterpretationServices();

builder.Services.AddScoped<TagContextoIaService>();

builder.Services.AddOperationalMultivariateCoreServices();

builder.Services.AddOperationalMultivariateGovernanceServices(
    builder.Configuration);

builder.Services.AddOperationalMultivariateDecisionServices(
    builder.Configuration);

builder.Services.AddMultivariateAnalysisProvider(
    usarGruposMultivariadosDoBenchmark);

builder.Services.AddConfidenceEngineServices(
    builder.Configuration);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.LoadExperimentalIncidentSnapshotAsync();

var benchmarkCommandContext =
    BenchmarkCommandContext.Create(
        args,
        builder.Configuration,
        commandSelection);

BenchmarkCommandValidator.Validate(
    commandSelection,
    benchmarkCommandContext);

var functionalValidationMessages =
    BenchmarkCommandValidator.ValidateFunctional(
        commandSelection,
        benchmarkCommandContext);

if (functionalValidationMessages.Length > 0)
{
    foreach (var message in functionalValidationMessages)
    {
        Console.WriteLine(message);
    }

    return;
}

var modeSelectionValidationMessage =
    BenchmarkCommandValidator.ValidateModeSelection(
        commandSelection);

if (modeSelectionValidationMessage is not null)
{
    Console.WriteLine();
    Console.WriteLine(
        modeSelectionValidationMessage);

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
        benchmarkCommandContext))
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

if (await BenchmarkEvaluationCommandExecutor.TryExecuteAsync(
        app.Services,
        args,
        commandSelection))
{
    return;
}

if (await BenchmarkDetectorSensitivityCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        benchmarkOptions))
{
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

if (await BenchmarkResultsAnalysisCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        benchmarkOptions))
{
    return;
}

if (await BenchmarkAdaptiveEnsembleCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        benchmarkOptions))
{
    return;
}

if (await BenchmarkFamilyConsensusCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        benchmarkOptions))
{
    return;
}

if (await BenchmarkConfidenceCommandExecutor.TryExecuteAsync(
        app.Services,
        commandSelection,
        benchmarkOptions))
{
    return;
}
app.Run();