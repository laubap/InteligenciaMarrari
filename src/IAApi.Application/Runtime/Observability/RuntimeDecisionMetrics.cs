using System.Collections.Concurrent;
using System.Threading;

namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// Métricas agregadas, em memória e thread-safe, do runtime decision.
/// Não altera decisões e não persiste dados.
/// </summary>
public sealed class RuntimeDecisionMetrics
{
    private long _totalDecisions;

    private long _emitAnomalyTrue;
    private long _emitAnomalyFalse;

    private long _emitNewEvidenceTrue;
    private long _emitNewEvidenceFalse;

    private long _usedLegacyFallbackTrue;
    private long _usedLegacyFallbackFalse;

    // 0 = nenhuma decisão registrada ainda.
    private long _firstDecisionUtcTicks;
    private long _lastDecisionUtcTicks;

    private readonly ConcurrentDictionary<string, long>
        _bySource =
            new(StringComparer.OrdinalIgnoreCase);

    public void Record(
        RuntimeAnomalyDecision decision)
    {
        var nowTicks =
            DateTimeOffset.UtcNow.UtcTicks;

        Interlocked.CompareExchange(
            ref _firstDecisionUtcTicks,
            nowTicks,
            comparand: 0);

        Interlocked.Exchange(
            ref _lastDecisionUtcTicks,
            nowTicks);

        Interlocked.Increment(
            ref _totalDecisions);

        Increment(
            decision.EmitAnomaly,
            ref _emitAnomalyTrue,
            ref _emitAnomalyFalse);

        Increment(
            decision.EmitNewEvidence,
            ref _emitNewEvidenceTrue,
            ref _emitNewEvidenceFalse);

        Increment(
            decision.UsedLegacyFallback,
            ref _usedLegacyFallbackTrue,
            ref _usedLegacyFallbackFalse);

        var source =
            string.IsNullOrWhiteSpace(decision.Source)
                ? "Unknown"
                : decision.Source.Trim();

        _bySource.AddOrUpdate(
            source,
            1L,
            static (_, current) =>
                current + 1L);
    }

    public RuntimeDecisionMetricsSnapshot Snapshot()
    {
        var firstTicks =
            Interlocked.Read(
                ref _firstDecisionUtcTicks);

        var lastTicks =
            Interlocked.Read(
                ref _lastDecisionUtcTicks);

        return new RuntimeDecisionMetricsSnapshot(
            TotalDecisions:
                Interlocked.Read(
                    ref _totalDecisions),

            EmitAnomalyTrue:
                Interlocked.Read(
                    ref _emitAnomalyTrue),

            EmitAnomalyFalse:
                Interlocked.Read(
                    ref _emitAnomalyFalse),

            EmitNewEvidenceTrue:
                Interlocked.Read(
                    ref _emitNewEvidenceTrue),

            EmitNewEvidenceFalse:
                Interlocked.Read(
                    ref _emitNewEvidenceFalse),

            UsedLegacyFallbackTrue:
                Interlocked.Read(
                    ref _usedLegacyFallbackTrue),

            UsedLegacyFallbackFalse:
                Interlocked.Read(
                    ref _usedLegacyFallbackFalse),

            BySource:
                new Dictionary<string, long>(
                    _bySource,
                    StringComparer.OrdinalIgnoreCase),

            FirstDecisionAtUtc:
                firstTicks > 0
                    ? new DateTimeOffset(
                        firstTicks,
                        TimeSpan.Zero)
                    : null,

            LastDecisionAtUtc:
                lastTicks > 0
                    ? new DateTimeOffset(
                        lastTicks,
                        TimeSpan.Zero)
                    : null);
    }

    private static void Increment(
        bool condition,
        ref long whenTrue,
        ref long whenFalse)
    {
        if (condition)
        {
            Interlocked.Increment(
                ref whenTrue);
        }
        else
        {
            Interlocked.Increment(
                ref whenFalse);
        }
    }
}