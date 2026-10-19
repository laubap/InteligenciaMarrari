using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Runtime.Persistence;

public sealed class EfRuntimeRolloutSnapshotWriter
    : IRuntimeRolloutSnapshotWriter
{
    private readonly AppDbContext _context;

    public EfRuntimeRolloutSnapshotWriter(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(
        RuntimeRolloutSnapshotData snapshot,
        CancellationToken cancellationToken = default)
    {
        var entity =
            new RuntimeRolloutSnapshot
            {
                GeneratedAtUtc =
                    snapshot.GeneratedAtUtc,

                RuntimeDecisionEnabled =
                    snapshot.RuntimeDecisionEnabled,

                CanaryOnly =
                    snapshot.CanaryOnly,

                AllowedClientIdsJson =
                    snapshot.AllowedClientIdsJson,

                Status =
                    snapshot.Status,

                ReadyToExpand =
                    snapshot.ReadyToExpand,

                TotalDecisions =
                    snapshot.TotalDecisions,

                ObservationMinutes =
                    snapshot.ObservationMinutes,

                LegacyFallbackRate =
                    snapshot.LegacyFallbackRate,

                DataQualityUncertainRate =
                    snapshot.DataQualityUncertainRate,

                CandidateMismatchRate =
                    snapshot.CandidateMismatchRate,

                StableUnresolvedContinuationRate =
                    snapshot.StableUnresolvedContinuationRate,

                ReasonsJson =
                    snapshot.ReasonsJson,

                FirstDecisionAtUtc =
                    snapshot.FirstDecisionAtUtc,

                LastDecisionAtUtc =
                    snapshot.LastDecisionAtUtc
            };

        _context.RuntimeRolloutSnapshots.Add(
            entity);

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}