using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeRolloutReadinessEvaluatorTests
{
    private readonly RuntimeRolloutReadinessEvaluator
        _sut = new();

    [Fact]
    public void Evaluate_RuntimeDecisionDesabilitado_RetornaBlocked()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: false),
                Snapshot(total: 100, observationMinutes: 60),
                Options());

        Assert.Equal("Blocked", result.Status);
        Assert.False(result.ReadyToExpand);
        Assert.Contains(
            "RuntimeDecisionDisabled",
            result.Reasons);
    }

    [Fact]
    public void Evaluate_AmostraInsuficiente_RetornaWarning()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: true),
                Snapshot(total: 3, observationMinutes: 60),
                Options(minimumDecisions: 20));

        Assert.Equal("Warning", result.Status);
        Assert.Contains(
            "InsufficientDecisionSample",
            result.Reasons);
    }

    [Fact]
    public void Evaluate_JanelaTemporalInsuficiente_RetornaWarning()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: true),
                Snapshot(total: 100, observationMinutes: 10),
                Options(minimumObservationMinutes: 30));

        Assert.Equal("Warning", result.Status);
        Assert.Contains(
            "InsufficientObservationWindow",
            result.Reasons);
    }

    [Fact]
    public void Evaluate_FallbackAcimaDoLimite_RetornaWarning()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: true),
                Snapshot(
                    total: 100,
                    observationMinutes: 60,
                    legacyFallbackTrue: 10),
                Options(
                    maximumLegacyFallbackRate: 0.05));

        Assert.Contains(
            "LegacyFallbackRateAboveLimit",
            result.Reasons);
    }

    [Fact]
    public void Evaluate_DataQualityAcimaDoLimite_RetornaWarning()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: true),
                Snapshot(
                    total: 100,
                    observationMinutes: 60,
                    dataQualityUncertain: 25),
                Options(
                    maximumDataQualityUncertainRate: 0.20));

        Assert.Contains(
            "DataQualityUncertainRateAboveLimit",
            result.Reasons);
    }

    [Fact]
    public void Evaluate_CandidateMismatchAcimaDoLimite_RetornaWarning()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: true),
                Snapshot(
                    total: 100,
                    observationMinutes: 60,
                    candidateMismatch: 30),
                Options(
                    maximumCandidateMismatchRate: 0.25));

        Assert.Contains(
            "CandidateMismatchRateAboveLimit",
            result.Reasons);
    }

    [Fact]
    public void Evaluate_StableContinuationAcimaDoLimite_RetornaWarning()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: true),
                Snapshot(
                    total: 100,
                    observationMinutes: 60,
                    stableContinuation: 70),
                Options(
                    maximumStableContinuationRate: 0.60));

        Assert.Contains(
            "StableUnresolvedContinuationRateAboveLimit",
            result.Reasons);
    }

    [Fact]
    public void Evaluate_CriteriosAtendidos_RetornaHealthy()
    {
        var result =
            _sut.Evaluate(
                RuntimeOptions(enabled: true),
                Snapshot(
                    total: 100,
                    observationMinutes: 60,
                    legacyFallbackTrue: 2,
                    dataQualityUncertain: 5,
                    candidateMismatch: 10,
                    stableContinuation: 20),
                Options());

        Assert.Equal("Healthy", result.Status);
        Assert.True(result.ReadyToExpand);
        Assert.Contains(
            "ReadinessCriteriaSatisfied",
            result.Reasons);
    }

    private static RuntimeAnomalyDecisionOptions RuntimeOptions(
        bool enabled)
    {
        return new RuntimeAnomalyDecisionOptions
        {
            Enabled = enabled,
            CanaryOnly = true,
            AllowedClientIds = ["PK7"]
        };
    }

    private static RuntimeRolloutReadinessOptions Options(
        int minimumDecisions = 20,
        double minimumObservationMinutes = 30,
        double maximumLegacyFallbackRate = 0.05,
        double maximumDataQualityUncertainRate = 0.20,
        double maximumCandidateMismatchRate = 0.25,
        double maximumStableContinuationRate = 0.60)
    {
        return new RuntimeRolloutReadinessOptions
        {
            MinimumDecisions = minimumDecisions,
            MinimumObservationMinutes =
                minimumObservationMinutes,
            MaximumLegacyFallbackRate =
                maximumLegacyFallbackRate,
            MaximumDataQualityUncertainRate =
                maximumDataQualityUncertainRate,
            MaximumCandidateMismatchRate =
                maximumCandidateMismatchRate,
            MaximumStableUnresolvedContinuationRate =
                maximumStableContinuationRate
        };
    }

    private static RuntimeDecisionMetricsSnapshot Snapshot(
        long total,
        double observationMinutes,
        long legacyFallbackTrue = 0,
        long dataQualityUncertain = 0,
        long candidateMismatch = 0,
        long stableContinuation = 0)
    {
        var bySource =
            new Dictionary<string, long>(
                StringComparer.OrdinalIgnoreCase);

        if (dataQualityUncertain > 0)
        {
            bySource["DataQualityUncertain"] =
                dataQualityUncertain;
        }

        if (candidateMismatch > 0)
        {
            bySource["CandidateMismatch"] =
                candidateMismatch;
        }

        if (stableContinuation > 0)
        {
            bySource["StableUnresolvedContinuation"] =
                stableContinuation;
        }

        var first =
            DateTimeOffset.Parse(
                "2026-08-24T12:00:00+00:00");

        var last =
            first.AddMinutes(
                observationMinutes);

        return new RuntimeDecisionMetricsSnapshot(
            TotalDecisions: total,
            EmitAnomalyTrue: 0L,
            EmitAnomalyFalse: total,
            EmitNewEvidenceTrue: 0L,
            EmitNewEvidenceFalse: total,
            UsedLegacyFallbackTrue:
                legacyFallbackTrue,
            UsedLegacyFallbackFalse:
                Math.Max(
                    0L,
                    total - legacyFallbackTrue),
            BySource: bySource,
            FirstDecisionAtUtc: first,
            LastDecisionAtUtc: last);
    }
}