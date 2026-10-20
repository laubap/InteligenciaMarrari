using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.Runtime.Persistence;

public sealed class EfRuntimeRolloutSnapshotReader
    : IRuntimeRolloutSnapshotReader
{
private readonly IRuntimeRolloutDbContext _context;

public EfRuntimeRolloutSnapshotReader(
    IRuntimeRolloutDbContext context)
{
    _context = context;
}

    public async Task<IReadOnlyList<RuntimeRolloutHistoryItemResponse>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        // SQLite/EF Core não traduz ORDER BY diretamente sobre DateTimeOffset.
        // SQL nativo mantém a ordenação e o limite usados pelo comportamento atual.
        return await _context.RuntimeRolloutSnapshots
            .FromSqlInterpolated($"""
                SELECT *
                FROM RuntimeRolloutSnapshots
                ORDER BY GeneratedAtUtc DESC, Id DESC
                LIMIT {limit}
                """)
            .AsNoTracking()
            .Select(x =>
                new RuntimeRolloutHistoryItemResponse
                {
                    Id = x.Id,
                    GeneratedAtUtc = x.GeneratedAtUtc,
                    RuntimeDecisionEnabled = x.RuntimeDecisionEnabled,
                    CanaryOnly = x.CanaryOnly,
                    AllowedClientIdsJson = x.AllowedClientIdsJson,
                    Status = x.Status,
                    ReadyToExpand = x.ReadyToExpand,
                    TotalDecisions = x.TotalDecisions,
                    ObservationMinutes = x.ObservationMinutes,
                    LegacyFallbackRate = x.LegacyFallbackRate,
                    DataQualityUncertainRate =
                        x.DataQualityUncertainRate,
                    CandidateMismatchRate =
                        x.CandidateMismatchRate,
                    StableUnresolvedContinuationRate =
                        x.StableUnresolvedContinuationRate,
                    ReasonsJson = x.ReasonsJson,
                    FirstDecisionAtUtc = x.FirstDecisionAtUtc,
                    LastDecisionAtUtc = x.LastDecisionAtUtc
                })
            .ToListAsync(
                cancellationToken);
    }
}