using IAApi.Infrastructure.Llm.DependencyInjection;
using IAApi.Services;
using IAApi.Services.AnomalyDetection;
using IAApi.DependencyInjection.ExperimentalIncidents;
using IAApi.Services.AnomalyDetection.Confidence.DependencyInjection;
using IAApi.Services.AnomalyDetection.DependencyInjection;
using IAApi.Services.AnomalyDetection.Interpretation;
using IAApi.Services.AnomalyDetection.Interpretation.DependencyInjection;
using IAApi.Services.AnomalyDetection.Multivariate.DependencyInjection;
using IAApi.Services.AnomalyDetection.Runtime.DependencyInjection;
using IAApi.Services.ModelTraining.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Services.DependencyInjection;

public static class IAApiApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIAApiApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useBenchmarkMultivariateGroups,
        Action<IServiceCollection, IConfiguration>? registerExternalServices = null)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=ia_config.db"));

        services.AddBridgeServices();

        services.AddTagProfileStatisticsServices();
        services.AddScoped<PerfilTagService>();
        services.AddScoped<PredictionService>();
        services.AddScoped<CurrentValuePredictionService>();
        services.AddRiskAnalysisServices();
        services.AddAnomalyContextAnalysisServices();
        services.AddProcessIntelligenceServices();

        services.AddOperationalRegimeServices();

        services.AddBehaviorMemoryDiscovery();

        services.AddHostedService<MonitoramentoTagsService>();
        services.AddSingleton<EnsembleV2Simulator>();

        services.AddRuntimeRolloutCoreServices(
            configuration);

        services.AddMultivariateDiagnosticServices();

        services.AddRuntimeRolloutPersistenceServices(
            configuration);

        services.AddAnomalyDetectionConfiguration(
            configuration);

        services.AddRuntimeAnomalyDecisionServices(
            configuration);

        services.AddAnomalyDetectionCoreServices();

        services.AddLegacyAnomalyDetectionServices();

        services.AddAnomalyDetectionValidation();

        services.AddIsolationForestServices(
            configuration);

        services.AddModelTrainingServices();

        services.Configure<OpenAiOptions>(
            configuration.GetSection("OpenAI"));

        services.AddLlmConfiguration(
            configuration);

        registerExternalServices?.Invoke(
            services,
            configuration);

        services.AddExperimentalIncidentApiServices(
            configuration);

        services.AddLmStudioProvider();

        services.AddAnomalyInterpretationServices();

        services.AddScoped<TagContextoIaService>();

        services.AddOperationalMultivariateCoreServices();

        services.AddOperationalMultivariateGovernanceServices(
            configuration);

        services.AddOperationalMultivariateDecisionServices(
            configuration);

        services.AddMultivariateAnalysisProvider(
            useBenchmarkMultivariateGroups);

        services.AddConfidenceEngineServices(
            configuration);

        return services;
    }
}
