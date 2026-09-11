using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeRolloutObservationSummaryTests
{
    [Fact]
    public void GetObservation_EndpointDesabilitado_Retorna404()
    {
        var controller =
            CreateController(
                metricsEnabled: false);

        var result =
            controller.GetObservation();

        Assert.IsType<NotFoundResult>(
            result.Result);
    }

    [Fact]
    public void GetObservation_ConsolidaConfiguracaoMetricasEReadiness()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: false,
                EmitAnomaly: false,
                EmitNewEvidence: false,
                UsedLegacyFallback: false,
                Source: "LegacyNormal",
                Reasons: ["Test"]));

        var controller =
            CreateController(
                metricsEnabled: true,
                runtimeDecisionEnabled: true,
                canaryOnly: true,
                allowedClientIds: [" PK7 ", ""],
                metrics: metrics);

        var result =
            controller.GetObservation();

        var ok =
            Assert.IsType<OkObjectResult>(
                result.Result);

        var summary =
            Assert.IsType<RuntimeRolloutObservationSummaryResponse>(
                ok.Value);

        Assert.True(
            summary.RuntimeDecisionEnabled);

        Assert.True(
            summary.CanaryOnly);

        Assert.True(
            summary.MetricsEndpointEnabled);

        Assert.Single(
            summary.AllowedClientIds);

        Assert.Equal(
            "PK7",
            summary.AllowedClientIds[0]);

        Assert.Equal(
            1,
            summary.Metrics.TotalDecisions);

        Assert.Equal(
            1,
            summary.Metrics.BySource["LegacyNormal"]);

        Assert.Equal(
            "Warning",
            summary.Readiness.Status);

        Assert.False(
            summary.Readiness.ReadyToExpand);
    }

    [Fact]
    public void GetObservation_UsaMesmoSnapshotParaMetricasEReadiness()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: true,
                EmitAnomaly: true,
                EmitNewEvidence: true,
                UsedLegacyFallback: false,
                Source: "NewAnomaly",
                Reasons: ["Test"]));

        var controller =
            CreateController(
                metricsEnabled: true,
                metrics: metrics);

        var result =
            controller.GetObservation();

        var ok =
            Assert.IsType<OkObjectResult>(
                result.Result);

        var summary =
            Assert.IsType<RuntimeRolloutObservationSummaryResponse>(
                ok.Value);

        Assert.Equal(
            summary.Metrics.TotalDecisions,
            summary.Readiness.TotalDecisions);

        Assert.Equal(
            summary.Metrics.FirstDecisionAtUtc,
            summary.Readiness.FirstDecisionAtUtc);

        Assert.Equal(
            summary.Metrics.LastDecisionAtUtc,
            summary.Readiness.LastDecisionAtUtc);
    }

    private static RuntimeDecisionMetricsController CreateController(
        bool metricsEnabled,
        bool runtimeDecisionEnabled = true,
        bool canaryOnly = true,
        string[]? allowedClientIds = null,
        RuntimeDecisionMetrics? metrics = null)
    {
        return new RuntimeDecisionMetricsController(
            metrics ?? new RuntimeDecisionMetrics(),

            Options.Create(
                new RuntimeDecisionMetricsEndpointOptions
                {
                    Enabled = metricsEnabled
                }),

            Options.Create(
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = runtimeDecisionEnabled,
                    CanaryOnly = canaryOnly,
                    AllowedClientIds =
                        allowedClientIds
                        ?? ["PK7"]
                }),

            Options.Create(
                new RuntimeRolloutReadinessOptions
                {
                    MinimumDecisions = 20,
                    MinimumObservationMinutes = 30,
                    MaximumLegacyFallbackRate = 0.05,
                    MaximumDataQualityUncertainRate = 0.20,
                    MaximumCandidateMismatchRate = 0.25,
                    MaximumStableUnresolvedContinuationRate = 0.60
                }),

            new RuntimeRolloutReadinessEvaluator());
    }
}