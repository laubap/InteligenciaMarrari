using IAApi.Benchmark.Cli;
using IAApi.Services.AnomalyDetection.Application.Incidents.Experimental.Bootstrap;
using IAApi.Services.DependencyInjection;

var benchmarkHost =
    BenchmarkHost.Create(args);

var builder =
    WebApplication.CreateBuilder(args);

benchmarkHost.ApplyPreServiceConfiguration(
    builder.Configuration);

builder.Services.AddIAApiApplicationServices(
    builder.Configuration,
    benchmarkHost.UsarGruposMultivariadosDoBenchmark,
    benchmarkHost.AddServices);

var app =
    builder.Build();

await app.LoadExperimentalIncidentSnapshotAsync();

if (await benchmarkHost.TryExecuteBeforePipelineAsync(
        app.Services,
        builder.Configuration))
{
    return;
}

if (await benchmarkHost.TryExecuteAfterPipelineAsync(
        app.Services))
{
    return;
}

Console.WriteLine(
    "Nenhum comando de benchmark foi selecionado.");
