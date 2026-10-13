using IAApi.Benchmark.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.Cli;

public sealed class BenchmarkHost
{
    private readonly string[] _args;

    private readonly BenchmarkCommandSelection
        _commandSelection;

    private BenchmarkOptions
        _benchmarkOptions = null!;

    private BenchmarkHost(
        string[] args,
        BenchmarkCommandSelection commandSelection)
    {
        _args = args;
        _commandSelection = commandSelection;
    }

    public bool UsarGruposMultivariadosDoBenchmark =>
        _commandSelection.UsarGruposMultivariadosDoBenchmark;

    public static BenchmarkHost Create(
        string[] args)
    {
        var commandSelection =
            BenchmarkCommandSelection.Parse(args);

        return new BenchmarkHost(
            args,
            commandSelection);
    }

    public void ApplyPreServiceConfiguration(
        IConfiguration configuration)
    {
        if (_commandSelection.PersistenceTemporalExperiment)
        {
            configuration[
                "AnomalyDetection:ExperimentalPersistenceTemporalConfirmationEnabled"] =
                    "true";
        }
    }

    public void AddServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        _benchmarkOptions =
            services.AddBenchmarkConfiguration(
                configuration);

        services.AddBenchmarkCoreServices();
        services.AddBenchmarkLaboratoryServices();
        services.AddBenchmarkMultivariateServices();
    }

    public Task<bool> TryExecuteBeforePipelineAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        return BenchmarkCommandOrchestrator
            .TryExecuteBeforePipelineAsync(
                services,
                _args,
                configuration,
                _commandSelection,
                _benchmarkOptions);
    }

    public Task<bool> TryExecuteAfterPipelineAsync(
        IServiceProvider services)
    {
        return BenchmarkCommandOrchestrator
            .TryExecuteAfterPipelineAsync(
                services,
                _commandSelection,
                _benchmarkOptions);
    }
}