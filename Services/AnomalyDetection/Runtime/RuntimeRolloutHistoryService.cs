using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.9 — consulta somente-leitura do histórico persistido de rollout.
/// </summary>
public sealed class RuntimeRolloutHistoryService
{
    public const int DefaultLimit = 20;
    public const int MaximumLimit = 100;

    private readonly AppDbContext _context;

    public RuntimeRolloutHistoryService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RuntimeRolloutHistoryItemResponse>> GetLatestAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit =
            NormalizeLimit(limit);

        // SQLite/EF Core não traduz ORDER BY diretamente sobre DateTimeOffset.
        // Usamos SQL nativo somente para a ordenação/limite, mantendo a
        // projeção tipada e AsNoTracking no EF.
        return await _context.RuntimeRolloutSnapshots
            .FromSqlInterpolated($"""
                SELECT *
                FROM RuntimeRolloutSnapshots
                ORDER BY GeneratedAtUtc DESC, Id DESC
                LIMIT {normalizedLimit}
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
                    DataQualityUncertainRate = x.DataQualityUncertainRate,
                    CandidateMismatchRate = x.CandidateMismatchRate,
                    StableUnresolvedContinuationRate =
                        x.StableUnresolvedContinuationRate,
                    ReasonsJson = x.ReasonsJson,
                    FirstDecisionAtUtc = x.FirstDecisionAtUtc,
                    LastDecisionAtUtc = x.LastDecisionAtUtc
                })
            .ToListAsync(
                cancellationToken);
    }

    public static int NormalizeLimit(
        int? limit)
    {
        if (limit is null)
        {
            return DefaultLimit;
        }

        return Math.Clamp(
            limit.Value,
            1,
            MaximumLimit);
    }
}