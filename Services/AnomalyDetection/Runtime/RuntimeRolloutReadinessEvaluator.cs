namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20.6 — converte configuração + métricas em Healthy / Warning / Blocked.
/// Somente leitura/observabilidade.
/// </summary>
public sealed class RuntimeRolloutReadinessEvaluator
{
    public RuntimeRolloutReadinessResponse Evaluate(
        RuntimeAnomalyDecisionOptions runtimeOptions,
        RuntimeDecisionMetricsSnapshot metrics,
        RuntimeRolloutReadinessOptions readinessOptions)
    {
        var reasons =
            new List<string>();

        var total =
            metrics.TotalDecisions;

        var legacyFallbackRate =
            Rate(
                metrics.UsedLegacyFallbackTrue,
                total);

        var dataQualityUncertainRate =
            SourceRate(
                metrics,
                "DataQualityUncertain");

        var candidateMismatchRate =
            SourceRate(
                metrics,
                "CandidateMismatch");

        var stableUnresolvedContinuationRate =
            SourceRate(
                metrics,
                "StableUnresolvedContinuation");

        var observationMinutes =
            ObservationMinutes(
                metrics.FirstDecisionAtUtc,
                metrics.LastDecisionAtUtc);

        if (!runtimeOptions.Enabled)
        {
            reasons.Add(
                "RuntimeDecisionDisabled");

            return Create(
                "Blocked",
                false,
                metrics,
                readinessOptions,
                observationMinutes,
                legacyFallbackRate,
                dataQualityUncertainRate,
                candidateMismatchRate,
                stableUnresolvedContinuationRate,
                reasons);
        }

        if (total < readinessOptions.MinimumDecisions)
        {
            reasons.Add(
                "InsufficientDecisionSample");
        }

        if (observationMinutes <
            readinessOptions.MinimumObservationMinutes)
        {
            reasons.Add(
                "InsufficientObservationWindow");
        }

        if (legacyFallbackRate >
            readinessOptions.MaximumLegacyFallbackRate)
        {
            reasons.Add(
                "LegacyFallbackRateAboveLimit");
        }

        if (dataQualityUncertainRate >
            readinessOptions.MaximumDataQualityUncertainRate)
        {
            reasons.Add(
                "DataQualityUncertainRateAboveLimit");
        }

        if (candidateMismatchRate >
            readinessOptions.MaximumCandidateMismatchRate)
        {
            reasons.Add(
                "CandidateMismatchRateAboveLimit");
        }

        if (stableUnresolvedContinuationRate >
            readinessOptions.MaximumStableUnresolvedContinuationRate)
        {
            reasons.Add(
                "StableUnresolvedContinuationRateAboveLimit");
        }

        if (reasons.Count == 0)
        {
            reasons.Add(
                "ReadinessCriteriaSatisfied");

            return Create(
                "Healthy",
                true,
                metrics,
                readinessOptions,
                observationMinutes,
                legacyFallbackRate,
                dataQualityUncertainRate,
                candidateMismatchRate,
                stableUnresolvedContinuationRate,
                reasons);
        }

        return Create(
            "Warning",
            false,
            metrics,
            readinessOptions,
            observationMinutes,
            legacyFallbackRate,
            dataQualityUncertainRate,
            candidateMismatchRate,
            stableUnresolvedContinuationRate,
            reasons);
    }

    private static RuntimeRolloutReadinessResponse Create(
        string status,
        bool readyToExpand,
        RuntimeDecisionMetricsSnapshot metrics,
        RuntimeRolloutReadinessOptions options,
        double observationMinutes,
        double legacyFallbackRate,
        double dataQualityUncertainRate,
        double candidateMismatchRate,
        double stableUnresolvedContinuationRate,
        List<string> reasons)
    {
        return new RuntimeRolloutReadinessResponse
        {
            Status = status,
            ReadyToExpand = readyToExpand,
            TotalDecisions = metrics.TotalDecisions,
            MinimumDecisions = options.MinimumDecisions,

            ObservationMinutes = observationMinutes,
            MinimumObservationMinutes =
                options.MinimumObservationMinutes,

            LegacyFallbackRate = legacyFallbackRate,
            MaximumLegacyFallbackRate =
                options.MaximumLegacyFallbackRate,

            DataQualityUncertainRate =
                dataQualityUncertainRate,
            MaximumDataQualityUncertainRate =
                options.MaximumDataQualityUncertainRate,

            CandidateMismatchRate =
                candidateMismatchRate,
            MaximumCandidateMismatchRate =
                options.MaximumCandidateMismatchRate,

            StableUnresolvedContinuationRate =
                stableUnresolvedContinuationRate,
            MaximumStableUnresolvedContinuationRate =
                options.MaximumStableUnresolvedContinuationRate,

            FirstDecisionAtUtc =
                metrics.FirstDecisionAtUtc,
            LastDecisionAtUtc =
                metrics.LastDecisionAtUtc,

            Reasons = reasons.ToArray()
        };
    }

    private static double SourceRate(
        RuntimeDecisionMetricsSnapshot metrics,
        string source)
    {
        if (!metrics.BySource.TryGetValue(
            source,
            out var count))
        {
            return 0;
        }

        return Rate(
            count,
            metrics.TotalDecisions);
    }

    private static double Rate(
        long count,
        long total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return (double)count / total;
    }

    private static double ObservationMinutes(
        DateTimeOffset? first,
        DateTimeOffset? last)
    {
        if (first is null ||
            last is null ||
            last < first)
        {
            return 0;
        }

        return (
            last.Value -
            first.Value
        ).TotalMinutes;
    }
}