using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeDecisionMetricsControllerTests
{
    [Fact]
    public void GetMetrics_EndpointDesabilitado_Retorna404()
    {
        var controller =
            CreateController(
                metricsEnabled: false);

        var result =
            controller.GetMetrics();

        Assert.IsType<NotFoundResult>(
            result.Result);
    }

    [Fact]
    public void GetMetrics_EndpointHabilitado_RetornaSnapshotAtual()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: true,
                EmitAnomaly: false,
                EmitNewEvidence: true,
                UsedLegacyFallback: false,
                Source: "CandidateMismatch",
                Reasons: ["Test"]));

        var controller =
            CreateController(
                metricsEnabled: true,
                metrics: metrics);

        var result =
            controller.GetMetrics();

        var ok =
            Assert.IsType<OkObjectResult>(
                result.Result);

        var snapshot =
            Assert.IsType<RuntimeDecisionMetricsSnapshot>(
                ok.Value);

        Assert.Equal(
            1,
            snapshot.TotalDecisions);

        Assert.Equal(
            1,
            snapshot.BySource["CandidateMismatch"]);
    }

    [Fact]
    public void GetStatus_EndpointDesabilitado_Retorna404()
    {
        var controller =
            CreateController(
                metricsEnabled: false,
                runtimeDecisionEnabled: true,
                canaryOnly: true,
                allowedClientIds: ["PK7"]);

        var result =
            controller.GetStatus();

        Assert.IsType<NotFoundResult>(
            result.Result);
    }

    [Fact]
    public void GetStatus_RetornaConfiguracaoDoRolloutEMetricas()
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
                runtimeDecisionEnabled: true,
                canaryOnly: true,
                allowedClientIds: [" PK7 ", "", "  "],
                metrics: metrics);

        var result =
            controller.GetStatus();

        var ok =
            Assert.IsType<OkObjectResult>(
                result.Result);

        var status =
            Assert.IsType<RuntimeRolloutStatusResponse>(
                ok.Value);

        Assert.True(
            status.RuntimeDecisionEnabled);

        Assert.True(
            status.CanaryOnly);

        Assert.True(
            status.MetricsEndpointEnabled);

        Assert.Single(
            status.AllowedClientIds);

        Assert.Equal(
            "PK7",
            status.AllowedClientIds[0]);

        Assert.Equal(
            1,
            status.Metrics.TotalDecisions);

        Assert.Equal(
            1,
            status.Metrics.BySource["NewAnomaly"]);
    }

    [Fact]
    public void GetStatus_RuntimeDecisionDesabilitado_RefleteConfiguracao()
    {
        var controller =
            CreateController(
                metricsEnabled: true,
                runtimeDecisionEnabled: false,
                canaryOnly: false);

        var result =
            controller.GetStatus();

        var ok =
            Assert.IsType<OkObjectResult>(
                result.Result);

        var status =
            Assert.IsType<RuntimeRolloutStatusResponse>(
                ok.Value);

        Assert.False(
            status.RuntimeDecisionEnabled);

        Assert.False(
            status.CanaryOnly);

        Assert.Empty(
            status.AllowedClientIds);
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
                        ?? Array.Empty<string>()
                }),

            Options.Create(
                new RuntimeRolloutReadinessOptions
                {
                    MinimumDecisions = 20,
                    MaximumLegacyFallbackRate = 0.05,
                    MaximumDataQualityUncertainRate = 0.20
                }),

            new RuntimeRolloutReadinessEvaluator());
    }
}