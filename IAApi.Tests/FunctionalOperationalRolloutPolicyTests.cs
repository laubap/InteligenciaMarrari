using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using Xunit;

namespace IAApi.Tests;

public sealed class FunctionalOperationalRolloutPolicyTests
{
    private readonly FunctionalOperationalRolloutPolicy _policy = new();

    [Fact]
    public void SemDecisaoOperacional_UsaLegacyFallback()
    {
        var result = _policy.Resolve(
            legacyFinalAnomaly: true,
            operationalDecision: null);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseLegacyFallback,
            result.Mode);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Contains("OperationalDecisionUnavailable", result.Reasons);
        Assert.Contains("LegacyRollback", result.Reasons);
    }

    [Fact]
    public void UpstreamLegacyFallback_ContinuaNoLegacy()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: true,
            emitAnomaly: true,
            emitNewEvidence: true,
            sourceDecision: ExperimentalAnomalyDecisionKind.NewAnomaly,
            usedLegacyFallback: true);

        var result = _policy.Resolve(true, operational);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseLegacyFallback,
            result.Mode);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
    }

    [Fact]
    public void DataQualityUncertain_UsaLegacyConservadoramente()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: true,
            emitAnomaly: true,
            emitNewEvidence: true,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.DataQualityUncertain);

        var result = _policy.Resolve(true, operational);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseLegacyFallback,
            result.Mode);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Contains("DataQualityUncertain", result.Reasons);
    }

    [Fact]
    public void NewAnomaly_UsaFuncional()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: true,
            emitAnomaly: true,
            emitNewEvidence: true,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.NewAnomaly);

        var result = _policy.Resolve(true, operational);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseFunctional,
            result.Mode);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
    }

    [Fact]
    public void Continuation_UsaFuncionalESuprimeNovaAnomalia()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: true,
            emitAnomaly: false,
            emitNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation);

        var result = _policy.Resolve(true, operational);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseFunctional,
            result.Mode);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
    }

    [Fact]
    public void CandidateMismatch_UsaFuncionalEPreservaNovaEvidencia()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: true,
            emitAnomaly: false,
            emitNewEvidence: true,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.CandidateMismatch);

        var result = _policy.Resolve(true, operational);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseFunctional,
            result.Mode);

        Assert.False(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
    }

    [Fact]
    public void LegacyNormal_UsaFuncionalSemInventarEvento()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: false,
            emitAnomaly: false,
            emitNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.LegacyNormal);

        var result = _policy.Resolve(false, operational);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseFunctional,
            result.Mode);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
    }


    [Fact]
    public void CanaryForaDoEscopo_ComLegacyAnomalo_UsaLegacy()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: true,
            emitAnomaly: false,
            emitNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation);

        var result = _policy.Resolve(
            legacyFinalAnomaly: true,
            operationalDecision: operational,
            canaryInScope: false);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseLegacyFallback,
            result.Mode);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Contains("CanaryOutOfScope", result.Reasons);
        Assert.Contains("LegacyRollback", result.Reasons);
    }

    [Fact]
    public void CanaryForaDoEscopo_ComLegacyNormal_NaoInventaAnomalia()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: false,
            emitAnomaly: false,
            emitNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.LegacyNormal);

        var result = _policy.Resolve(
            legacyFinalAnomaly: false,
            operationalDecision: operational,
            canaryInScope: false);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseLegacyFallback,
            result.Mode);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Contains("CanaryOutOfScope", result.Reasons);
    }

    [Fact]
    public void IncreasingNoiseContinuation_UsaRolloutFuncional()
    {
        var operational = CreateOperational(
            legacyFinalAnomaly: false,
            emitAnomaly: true,
            emitNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation);

        var result = _policy.Resolve(
            legacyFinalAnomaly: false,
            operationalDecision: operational);

        Assert.Equal(
            FunctionalOperationalRolloutMode.UseFunctional,
            result.Mode);

        Assert.True(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation,
            result.SourceDecision);
    }
    private static FunctionalOperationalAnomalyDecision CreateOperational(
        bool legacyFinalAnomaly,
        bool emitAnomaly,
        bool emitNewEvidence,
        ExperimentalAnomalyDecisionKind sourceDecision,
        bool usedLegacyFallback = false)
    {
        return new FunctionalOperationalAnomalyDecision(
            LegacyFinalAnomaly: legacyFinalAnomaly,
            EmitAnomaly: emitAnomaly,
            EmitNewEvidence: emitNewEvidence,
            SourceDecision: sourceDecision,
            UsedLegacyFallback: usedLegacyFallback,
            Reasons: ["Test"]);
    }
}