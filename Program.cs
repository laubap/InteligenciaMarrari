using Microsoft.EntityFrameworkCore;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services;
using IAApi.Benchmark.Cli;
using IAApi.Services.AnomalyDetection.Multivariate.DependencyInjection;
using IAApi.Services.AnomalyDetection.Confidence.DependencyInjection;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.DependencyInjection;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Bootstrap;
using IAApi.Infrastructure.Llm.DependencyInjection;
using IAApi.Services.AnomalyDetection.Interpretation.DependencyInjection;
using IAApi.Services.AnomalyDetection.DependencyInjection;
using IAApi.Services.ModelTraining.DependencyInjection;
using IAApi.Services.AnomalyDetection.Runtime.DependencyInjection;
using IAApi.Services.DependencyInjection;

var benchmarkHost =
    BenchmarkHost.Create(args);

var builder = WebApplication.CreateBuilder(args);

benchmarkHost.ApplyPreServiceConfiguration(
    builder.Configuration);

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

builder.Services.AddBridgeServices();

builder.Services.AddTagProfileStatisticsServices();
builder.Services.AddScoped<PerfilTagService>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<CurrentValuePredictionService>();
builder.Services.AddRiskAnalysisServices();
builder.Services.AddAnomalyContextAnalysisServices();
builder.Services.AddProcessIntelligenceServices();



builder.Services.AddOperationalRegimeServices();


// B3.25 — descoberta e memória comportamental.
builder.Services.AddBehaviorMemoryDiscovery();

builder.Services.AddHostedService<MonitoramentoTagsService>();
builder.Services.AddSingleton<EnsembleV2Simulator>();

builder.Services.AddRuntimeRolloutCoreServices(
    builder.Configuration);

builder.Services.AddMultivariateDiagnosticServices();

builder.Services.AddRuntimeRolloutPersistenceServices(
    builder.Configuration);

// Configuração dos detectores de anomalia
builder.Services.AddAnomalyDetectionConfiguration(
    builder.Configuration);

// B3.18 — runtime operational decision.
// Ausência da seção mantém Enabled = false e comportamento Legacy.
builder.Services.AddRuntimeAnomalyDecisionServices(
    builder.Configuration);

builder.Services.AddAnomalyDetectionCoreServices();

builder.Services.AddLegacyAnomalyDetectionServices();

builder.Services.AddAnomalyDetectionValidation();

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
benchmarkHost.AddServices(
    builder.Services,
    builder.Configuration);

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
    benchmarkHost.UsarGruposMultivariadosDoBenchmark);

builder.Services.AddConfidenceEngineServices(
    builder.Configuration);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.LoadExperimentalIncidentSnapshotAsync();

if (await benchmarkHost.TryExecuteBeforePipelineAsync(
        app.Services,
        builder.Configuration))
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

if (await benchmarkHost.TryExecuteAfterPipelineAsync(
        app.Services))
{
    return;
}
app.Run();