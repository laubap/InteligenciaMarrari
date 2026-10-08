using IAApi.Benchmark;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using IAApi.Benchmark.Multivariate;
using IAApi.Benchmark.Tests;
using IAApi.Services.AnomalyDetection.Multivariate;

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

    public static BenchmarkOptions AddBenchmarkConfiguration(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var benchmarkSection =
        configuration.GetRequiredSection(
            BenchmarkOptions.SectionName);

    var benchmarkOptions =
        benchmarkSection.Get<BenchmarkOptions>()
        ?? throw new InvalidOperationException(
            "Não foi possível carregar a configuração Benchmark.");

    Console.WriteLine(
        $"CleanDatabasePath carregado: " +
        $"{benchmarkOptions.CleanDatabasePath}");

    Console.WriteLine(
        $"AnomalyDatabasePath carregado: " +
        $"{benchmarkOptions.AnomalyDatabasePath}");

    services.AddSingleton(benchmarkOptions);

    services.AddSingleton<SQLiteHistoricoService>(
        _ => new SQLiteHistoricoService(
            benchmarkOptions.HistoryWindowSize));

    return benchmarkOptions;
}

public static IServiceCollection AddBenchmarkLaboratoryServices(
    this IServiceCollection services)
{
    services.AddSingleton<AdaptiveEnsembleLaboratory>();

    services.AddSingleton<ConfidenceBenchmarkAnalyzer>();
    services.AddSingleton<ConfidenceBenchmarkReportWriter>();
    services.AddSingleton<ConfidenceEngineBenchmarkRunner>();

    return services;
}
public static IServiceCollection AddBenchmarkMultivariateServices(
    this IServiceCollection services)
{
    services.AddScoped<GrupoAnaliseTester>();
    services.AddScoped<MultivariateBufferTester>();
    services.AddScoped<MultivariateDatasetTester>();
    services.AddScoped<MultivariateDetectorTester>();
    services.AddScoped<MultivariateTrainingService>();

    return services;
}
}