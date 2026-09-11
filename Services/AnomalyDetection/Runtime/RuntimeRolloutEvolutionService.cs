using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.Runtime;

public sealed class RuntimeRolloutEvolutionService
{
    private readonly AppDbContext _context;

    public RuntimeRolloutEvolutionService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<RuntimeRolloutEvolutionResponse> GetEvolutionAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit =
            RuntimeRolloutHistoryService.NormalizeLimit(limit);

        var snapshots =
            await _context.RuntimeRolloutSnapshots
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM RuntimeRolloutSnapshots
                    ORDER BY GeneratedAtUtc DESC, Id DESC
                    LIMIT {normalizedLimit}
                    """)
                .AsNoTracking()
                .ToListAsync(
                    cancellationToken);

        if (snapshots.Count == 0)
        {
            return new RuntimeRolloutEvolutionResponse();
        }

        snapshots.Reverse();

        var changes =
            new List<RuntimeRolloutEvolutionItemResponse>();

        long observedDecisionGrowth = 0;
        double observedObservationGrowth = 0;
        var restartCount = 0;

        for (var i = 1; i < snapshots.Count; i++)
        {
            var previous =
                snapshots[i - 1];

            var current =
                snapshots[i];

            var restartBoundary =
                IsRestartBoundary(
                    previous,
                    current);

            long? decisionDelta = null;
            double? observationDelta = null;

            if (restartBoundary)
            {
                restartCount++;
            }
            else
            {
                decisionDelta =
                    current.TotalDecisions -
                    previous.TotalDecisions;

                observationDelta =
                    current.ObservationMinutes -
                    previous.ObservationMinutes;

                if (decisionDelta > 0)
                {
                    observedDecisionGrowth +=
                        decisionDelta.Value;
                }

                if (observationDelta > 0)
                {
                    observedObservationGrowth +=
                        observationDelta.Value;
                }
            }

            changes.Add(
                new RuntimeRolloutEvolutionItemResponse
                {
                    PreviousSnapshotId =
                        previous.Id,

                    CurrentSnapshotId =
                        current.Id,

                    PreviousGeneratedAtUtc =
                        previous.GeneratedAtUtc,

                    CurrentGeneratedAtUtc =
                        current.GeneratedAtUtc,

                    RestartBoundary =
                        restartBoundary,

                    DecisionDelta =
                        decisionDelta,

                    ObservationMinutesDelta =
                        observationDelta,

                    LegacyFallbackRateDelta =
                        current.LegacyFallbackRate -
                        previous.LegacyFallbackRate,

                    DataQualityUncertainRateDelta =
                        current.DataQualityUncertainRate -
                        previous.DataQualityUncertainRate,

                    CandidateMismatchRateDelta =
                        current.CandidateMismatchRate -
                        previous.CandidateMismatchRate,

                    StableUnresolvedContinuationRateDelta =
                        current.StableUnresolvedContinuationRate -
                        previous.StableUnresolvedContinuationRate,

                    PreviousStatus =
                        previous.Status,

                    CurrentStatus =
                        current.Status,

                    StatusChanged =
                        !string.Equals(
                            previous.Status,
                            current.Status,
                            StringComparison.OrdinalIgnoreCase),

                    BecameReadyToExpand =
                        !previous.ReadyToExpand &&
                        current.ReadyToExpand
                });
        }

        var latest =
            snapshots[^1];

        var firstReady =
            snapshots
                .Where(x =>
                    x.ReadyToExpand)
                .OrderBy(x =>
                    x.GeneratedAtUtc)
                .Select(x =>
                    (DateTimeOffset?)x.GeneratedAtUtc)
                .FirstOrDefault();

        return new RuntimeRolloutEvolutionResponse
        {
            SnapshotCount =
                snapshots.Count,

            OldestSnapshotId =
                snapshots[0].Id,

            LatestSnapshotId =
                latest.Id,

            ObservedDecisionGrowth =
                observedDecisionGrowth,

            ObservedObservationMinutesGrowth =
                observedObservationGrowth,

            ProcessRestartCount =
                restartCount,

            ObservationSessionCount =
                restartCount + 1,

            CurrentStatus =
                latest.Status,

            CurrentReadyToExpand =
                latest.ReadyToExpand,

            FirstReadyToExpandAtUtc =
                firstReady,

            LegacyFallbackTrend =
                TrendWithinLatestSession(
                    snapshots,
                    x => x.LegacyFallbackRate),

            DataQualityUncertainTrend =
                TrendWithinLatestSession(
                    snapshots,
                    x => x.DataQualityUncertainRate),

            CandidateMismatchTrend =
                TrendWithinLatestSession(
                    snapshots,
                    x => x.CandidateMismatchRate),

            StableUnresolvedContinuationTrend =
                TrendWithinLatestSession(
                    snapshots,
                    x => x.StableUnresolvedContinuationRate),

            Changes =
                changes.ToArray()
        };
    }

    private static bool IsRestartBoundary(
        RuntimeRolloutSnapshot previous,
        RuntimeRolloutSnapshot current)
    {
        if (current.TotalDecisions <
            previous.TotalDecisions)
        {
            return true;
        }

        if (current.ObservationMinutes <
            previous.ObservationMinutes)
        {
            return true;
        }

        if (previous.FirstDecisionAtUtc is not null &&
            current.FirstDecisionAtUtc is not null &&
            current.FirstDecisionAtUtc != previous.FirstDecisionAtUtc)
        {
            return true;
        }

        if (previous.FirstDecisionAtUtc is not null &&
            current.FirstDecisionAtUtc is null)
        {
            return true;
        }

        return false;
    }

    private static string TrendWithinLatestSession(
        IReadOnlyList<RuntimeRolloutSnapshot> snapshots,
        Func<RuntimeRolloutSnapshot, double> selector)
    {
        if (snapshots.Count < 2)
        {
            return "Stable";
        }

        var sessionStartIndex =
            snapshots.Count - 1;

        while (sessionStartIndex > 0 &&
               !IsRestartBoundary(
                   snapshots[sessionStartIndex - 1],
                   snapshots[sessionStartIndex]))
        {
            sessionStartIndex--;
        }

        var first =
            selector(
                snapshots[sessionStartIndex]);

        var last =
            selector(
                snapshots[^1]);

        return Trend(
            first,
            last);
    }

    private static string Trend(
        double first,
        double last)
    {
        const double epsilon = 0.000001;

        var delta =
            last - first;

        if (Math.Abs(delta) <= epsilon)
        {
            return "Stable";
        }

        return delta > 0
            ? "Increasing"
            : "Decreasing";
    }
}