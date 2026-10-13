using IAApi.Benchmark.Cli;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Bootstrap;
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

builder.Services.AddIAApiApplicationServices(
    builder.Configuration,
    benchmarkHost.UsarGruposMultivariadosDoBenchmark,
    benchmarkHost.AddServices);

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