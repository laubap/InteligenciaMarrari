using Microsoft.EntityFrameworkCore;

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

// Serviços
builder.Services.AddSingleton<IHistoricoService, BridgeHistoricoService>();
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
builder.Services.AddHostedService<MonitoramentoTagsService>();
builder.Services.AddScoped<FeedbackAnomaliaAnaliseService>();

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