using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using Xunit;

namespace IAApi.Tests;

public sealed class FunctionalOperationalAnomalyDecisionResolverTests
{
    private readonly FunctionalOperationalAnomalyDecisionResolver _resolver = new();

    [Fact]
    public void LegacyNormal_NaoEmiteAnomaliaNemNovaEvidencia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: false,
            functionalFinalAnomaly: false,
            countsAsNewEvidence: false,
            sourceDecision: ExperimentalAnomalyDecisionKind.LegacyNormal);

        var result = _resolver.Resolve(input);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void NewAnomaly_EmiteAnomaliaENovaEvidencia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: true,
            functionalFinalAnomaly: true,
            countsAsNewEvidence: true,
            sourceDecision: ExperimentalAnomalyDecisionKind.NewAnomaly);

        var result = _resolver.Resolve(input);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void StableUnresolvedContinuation_NaoEmiteNovaAnomaliaNemNovaEvidencia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: true,
            functionalFinalAnomaly: false,
            countsAsNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation);

        var result = _resolver.Resolve(input);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void CandidateMismatch_PreservaNovaEvidenciaSemEmitirNovaAnomalia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: true,
            functionalFinalAnomaly: false,
            countsAsNewEvidence: true,
            sourceDecision: ExperimentalAnomalyDecisionKind.CandidateMismatch);

        var result = _resolver.Resolve(input);

        Assert.False(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void CandidateMismatch_ComLegacyNormal_TambemPreservaNovaEvidencia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: false,
            functionalFinalAnomaly: false,
            countsAsNewEvidence: true,
            sourceDecision: ExperimentalAnomalyDecisionKind.CandidateMismatch);

        var result = _resolver.Resolve(input);

        Assert.False(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void DataQualityUncertain_ComLegacyAnomalo_PreservaAnomaliaENovaEvidencia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: true,
            functionalFinalAnomaly: true,
            countsAsNewEvidence: true,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.DataQualityUncertain);

        var result = _resolver.Resolve(input);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void DataQualityUncertain_ComLegacyNormal_NaoInventaAnomalia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: false,
            functionalFinalAnomaly: false,
            countsAsNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.DataQualityUncertain);

        var result = _resolver.Resolve(input);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void LegacyFallback_ComLegacyAnomalo_PreservaLegacy()
    {
        var input = new FunctionalExperimentalAnomalyDecision(
            LegacyFinalAnomaly: true,
            FunctionalFinalAnomaly: true,
            CountsAsNewEvidence: true,
            SourceDecision: ExperimentalAnomalyDecisionKind.NewAnomaly,
            UsedLegacyFallback: true,
            Reasons: ["ExperimentalDecisionUnavailable", "LegacyFallback"]);

        var result = _resolver.Resolve(input);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);

        Assert.Contains(
            "OperationalLegacyFallback",
            result.Reasons);
    }

    [Fact]
    public void LegacyFallback_ComLegacyNormal_PreservaNormal()
    {
        var input = new FunctionalExperimentalAnomalyDecision(
            LegacyFinalAnomaly: false,
            FunctionalFinalAnomaly: false,
            CountsAsNewEvidence: false,
            SourceDecision: ExperimentalAnomalyDecisionKind.LegacyNormal,
            UsedLegacyFallback: true,
            Reasons: ["ExperimentalDecisionUnavailable", "LegacyFallback"]);

        var result = _resolver.Resolve(input);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);
    }

    [Fact]
    public void IncreasingNoiseContinuation_MantemAnomaliaSemNovaEvidencia()
    {
        var input = CreateFunctionalDecision(
            legacyFinalAnomaly: false,
            functionalFinalAnomaly: true,
            countsAsNewEvidence: false,
            sourceDecision:
                ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation);

        var result = _resolver.Resolve(input);

        Assert.True(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation,
            result.SourceDecision);
    }
    private static FunctionalExperimentalAnomalyDecision CreateFunctionalDecision(
        bool legacyFinalAnomaly,
        bool functionalFinalAnomaly,
        bool countsAsNewEvidence,
        ExperimentalAnomalyDecisionKind sourceDecision)
    {
        return new FunctionalExperimentalAnomalyDecision(
            LegacyFinalAnomaly: legacyFinalAnomaly,
            FunctionalFinalAnomaly: functionalFinalAnomaly,
            CountsAsNewEvidence: countsAsNewEvidence,
            SourceDecision: sourceDecision,
            UsedLegacyFallback: false,
            Reasons: ["Test"]);
    }
}