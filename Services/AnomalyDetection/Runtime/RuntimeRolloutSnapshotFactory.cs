using System.Text.Json;

namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// Converte o estado atual do rollout em entidade persistível.
/// Não grava no banco e não altera nenhuma decisão.
/// </summary>
public sealed class RuntimeRolloutSnapshotFactory
{
    private readonly RuntimeRolloutReadinessEvaluator
        _readinessEvaluator;

    public RuntimeRolloutSnapshotFactory(
        RuntimeRolloutReadinessEvaluator readinessEvaluator)
    {
        _readinessEvaluator = readinessEvaluator;
    }

public RuntimeRolloutSnapshotData Create(
    RuntimeAnomalyDecisionOptions runtimeOptions,
    RuntimeDecisionMetricsSnapshot metrics,
    RuntimeRolloutReadinessOptions readinessOptions,
    DateTimeOffset generatedAtUtc)
    {
        var readiness =
            _readinessEvaluator.Evaluate(
                runtimeOptions,
                metrics,
                readinessOptions);

        var allowedClientIds =
            runtimeOptions.AllowedClientIds
                ?.Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Select(x =>
                    x.Trim())
                .ToArray()
            ?? Array.Empty<string>();

return new RuntimeRolloutSnapshotData
        {
            GeneratedAtUtc =
                generatedAtUtc,

            RuntimeDecisionEnabled =
                runtimeOptions.Enabled,

            CanaryOnly =
                runtimeOptions.CanaryOnly,

            AllowedClientIdsJson =
                JsonSerializer.Serialize(
                    allowedClientIds),

            Status =
                readiness.Status,

            ReadyToExpand =
                readiness.ReadyToExpand,

            TotalDecisions =
                readiness.TotalDecisions,

            ObservationMinutes =
                readiness.ObservationMinutes,

            LegacyFallbackRate =
                readiness.LegacyFallbackRate,

            DataQualityUncertainRate =
                readiness.DataQualityUncertainRate,

            CandidateMismatchRate =
                readiness.CandidateMismatchRate,

            StableUnresolvedContinuationRate =
                readiness.StableUnresolvedContinuationRate,

            ReasonsJson =
                JsonSerializer.Serialize(
                    readiness.Reasons),

            FirstDecisionAtUtc =
                readiness.FirstDecisionAtUtc,

            LastDecisionAtUtc =
                readiness.LastDecisionAtUtc
        };
    }
}