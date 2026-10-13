using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Bootstrap;
using IAApi.Services.DependencyInjection;

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

builder.Services.AddIAApiApplicationServices(
    builder.Configuration,
    useBenchmarkMultivariateGroups: false);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.LoadExperimentalIncidentSnapshotAsync();

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