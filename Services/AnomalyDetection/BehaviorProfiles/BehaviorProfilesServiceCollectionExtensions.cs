using Microsoft.Extensions.DependencyInjection.Extensions;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra o pipeline produtivo de descoberta e memória comportamental B3.25.
///
/// O runtime de detecção não é alterado por este registro.
/// </summary>
public static class BehaviorProfilesServiceCollectionExtensions
{
    public static IServiceCollection AddBehaviorMemoryDiscovery(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        /*
         * Estatística compartilhada.
         * HistoricoLimpezaService já pode existir no Program.cs;
         * TryAdd evita duplicar o registro.
         */
        services.TryAddScoped<HistoricoLimpezaService>();
        services.TryAddScoped<TagProfileStatisticsCalculator>();

        /*
         * Histórico e descoberta.
         */
        services.TryAddScoped<BehaviorHistoryCollectionService>();
        services.TryAddScoped<BehaviorHistorySegmentationService>();
        services.TryAddScoped<BehaviorChangePointDetector>();
        services.TryAddScoped<BehaviorCandidateEpisodeBuilder>();
        services.TryAddScoped<BehaviorCandidateEpisodeCharacterizer>();
        services.TryAddScoped<BehaviorCandidateEpisodeSimilarityComparer>();
        services.TryAddScoped<BehaviorCandidateEpisodeSimilarityGrouper>();
        services.TryAddScoped<BehaviorDiscoveryCandidateFactory>();

        /*
         * Lifecycle, identidade e persistência.
         */
        services.TryAddScoped<BehaviorMemoryLifecyclePolicy>();
        services.TryAddScoped<BehaviorIdentityConfidenceCalculator>();
        services.TryAddScoped<BehaviorIdentityProposalService>();
        services.TryAddScoped<BehaviorMemoryPersistencePlanBuilder>();
        services.TryAddScoped<BehaviorMemoryReconciliationService>();
        services.TryAddScoped<BehaviorMemoryPersistenceService>();
        services.TryAddScoped<BehaviorCandidateMemoryService>();
        services.TryAddScoped<BehaviorMemorySnapshotRebuildService>();
        services.TryAddScoped<BehaviorMemoryConsolidationService>();
        services.TryAddScoped<BehaviorMemoryClassificationService>();

        /*
         * Runtime comportamental B3.25.
         *
         * Nesta etapa o provider permanece read-only e não altera
         * o baseline utilizado pelos detectores.
         */
        services.TryAddScoped<BehaviorRuntimeBaselineProvider>();

        /*
         * Ponto único de entrada produtivo.
         */
        services.TryAddScoped<BehaviorDiscoveryWorkflowService>();

        return services;
    }
}
