using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeAnomalyDecisionCanaryTests
{
    [Fact]
    public void CanaryOnly_ClientePermitido_UsaDecisaoFuncional()
    {
        var resolver = new RuntimeAnomalyDecisionResolver();

        var result =
            resolver.Resolve(
                true,
                Options(
                    canaryOnly: true,
                    allowedClientIds: ["PK7"]),
                clientId: "PK7",
                Functional(
                    emitAnomaly: false,
                    emitNewEvidence: false));

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
        Assert.Equal("FunctionalTest", result.Source);
    }

    [Fact]
    public void CanaryOnly_ClienteForaDaAllowlist_PreservaLegacy()
    {
        var resolver = new RuntimeAnomalyDecisionResolver();

        var result =
            resolver.Resolve(
                true,
                Options(
                    canaryOnly: true,
                    allowedClientIds: ["PK7"]),
                clientId: "OUTRO",
                Functional(
                    emitAnomaly: false,
                    emitNewEvidence: false));

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);
        Assert.Equal(
            "LegacyCanaryExcluded",
            result.Source);
        Assert.Contains(
            "ClientNotEnabledForRuntimeDecision",
            result.Reasons);
    }

    [Fact]
    public void CanaryOnly_AllowlistVazia_FazFallbackConservadorParaLegacy()
    {
        var resolver = new RuntimeAnomalyDecisionResolver();

        var result =
            resolver.Resolve(
                true,
                Options(
                    canaryOnly: true,
                    allowedClientIds: []),
                clientId: "PK7",
                Functional(
                    emitAnomaly: false,
                    emitNewEvidence: false));

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);
        Assert.Equal(
            "LegacyCanaryExcluded",
            result.Source);
    }

    [Fact]
    public void RolloutGlobal_IgnoraAllowlist_EUsaDecisaoFuncional()
    {
        var resolver = new RuntimeAnomalyDecisionResolver();

        var result =
            resolver.Resolve(
                true,
                Options(
                    canaryOnly: false,
                    allowedClientIds: []),
                clientId: "QUALQUER-CLIENTE",
                Functional(
                    emitAnomaly: false,
                    emitNewEvidence: true));

        Assert.False(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
        Assert.Equal("FunctionalTest", result.Source);
    }

    private static RuntimeAnomalyDecisionOptions Options(
        bool canaryOnly,
        string[] allowedClientIds)
    {
        return new RuntimeAnomalyDecisionOptions
        {
            Enabled = true,
            CanaryOnly = canaryOnly,
            AllowedClientIds = allowedClientIds
        };
    }

    private static RuntimeFunctionalDecisionCandidate Functional(
        bool emitAnomaly,
        bool emitNewEvidence)
    {
        return new RuntimeFunctionalDecisionCandidate(
            EmitAnomaly: emitAnomaly,
            EmitNewEvidence: emitNewEvidence,
            Source: "FunctionalTest",
            Reasons: ["FunctionalCandidate"]);
    }
}