using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.ModelTraining;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services;
using IAApi.Infrastructure.Llm.Providers;
using IAApi.Infrastructure.Llm.Configuration;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddHostedService<MonitoramentoTagsService>();

// Configuração dos detectores de anomalia
builder.Services.Configure<AnomalyDetectionOptions>(
    builder.Configuration.GetSection("AnomalyDetection")
);

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

// Configuração do LLM
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection(
        LlmOptions.SectionName
    )
);

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

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("FrontendCors");

app.MapControllers();

app.Run();