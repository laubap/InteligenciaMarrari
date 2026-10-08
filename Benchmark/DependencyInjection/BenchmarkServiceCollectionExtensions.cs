using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Benchmark.DependencyInjection;

public static class BenchmarkServiceCollectionExtensions
{
    public static IServiceCollection AddBenchmarkCoreServices(
        this IServiceCollection services)
    {
        services.AddScoped<BenchmarkRunner>();

        services.AddSingleton<BenchmarkEvaluator>();
        services.AddSingleton<CleanBenchmarkEvaluator>();

        services.AddSingleton<CalibrationInvestigator>();
        services.AddSingleton<CalibrationAllInvestigator>();

        services.AddSingleton<DeadbandImpactEvaluator>();
        services.AddSingleton<ResultsAnalyzer>();
        services.AddSingleton<DetectorSensitivityReport>();
        services.AddSingleton<FamilyConsensusReport>();
        services.AddSingleton<FalsePositiveRootCauseAnalyzer>();

        return services;
    }
}