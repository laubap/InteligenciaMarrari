using IAApi.Benchmark.ExperimentalAnomalyDecisions;

namespace IAApi.Tests;

public class FunctionalExperimentalAnomalyDecisionResolverTests
{
    private readonly FunctionalExperimentalAnomalyDecisionResolver _resolver = new();

    [Fact]
    public void MissingExperimentalDecision_ShouldFallbackToLegacyAnomaly()
    {
        var result = _resolver.Resolve(
            legacyFinalAnomaly: true,
            experimentalDecision: null);

        Assert.True(result.LegacyFinalAnomaly);
        Assert.True(result.FunctionalFinalAnomaly);
        Assert.True(result.CountsAsNewEvidence);
        Assert.True(result.UsedLegacyFallback);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.NewAnomaly,
            result.SourceDecision);

        Assert.Contains("LegacyFallback", result.Reasons);
    }

    [Fact]
    public void MissingExperimentalDecision_ShouldFallbackToLegacyNormal()
    {
        var result = _resolver.Resolve(
            legacyFinalAnomaly: false,
            experimentalDecision: null);

        Assert.False(result.LegacyFinalAnomaly);
        Assert.False(result.FunctionalFinalAnomaly);
        Assert.False(result.CountsAsNewEvidence);
        Assert.True(result.UsedLegacyFallback);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.LegacyNormal,
            result.SourceDecision);
    }

    [Fact]
    public void NewAnomaly_ShouldRemainFunctionalAnomaly()
    {
        var decision = CreateDecision(
            ExperimentalAnomalyDecisionKind.NewAnomaly,
            countsAsNewAnomaly: true,
            countsAsNewEvidence: true);

        var result = _resolver.Resolve(
            legacyFinalAnomaly: true,
            experimentalDecision: decision);

        Assert.True(result.FunctionalFinalAnomaly);
        Assert.True(result.CountsAsNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void StableContinuation_ShouldNotBecomeFunctionalNewAnomaly()
    {
        var decision = CreateDecision(
            ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation,
            countsAsNewAnomaly: false,
            countsAsNewEvidence: false);

        var result = _resolver.Resolve(
            legacyFinalAnomaly: true,
            experimentalDecision: decision);

        Assert.True(result.LegacyFinalAnomaly);
        Assert.False(result.FunctionalFinalAnomaly);
        Assert.False(result.CountsAsNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void CandidateMismatch_ShouldRemainEvidenceWithoutNewAnomaly()
    {
        var decision = CreateDecision(
            ExperimentalAnomalyDecisionKind.CandidateMismatch,
            countsAsNewAnomaly: false,
            countsAsNewEvidence: true);

        var result = _resolver.Resolve(
            legacyFinalAnomaly: false,
            experimentalDecision: decision);

        Assert.False(result.FunctionalFinalAnomaly);
        Assert.True(result.CountsAsNewEvidence);
        Assert.False(result.UsedLegacyFallback);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.CandidateMismatch,
            result.SourceDecision);
    }

    [Fact]
    public void DataQualityUncertain_ShouldPreserveLegacyAnomaly()
    {
        var decision = CreateDecision(
            ExperimentalAnomalyDecisionKind.DataQualityUncertain,
            countsAsNewAnomaly: true,
            countsAsNewEvidence: true);

        var result = _resolver.Resolve(
            legacyFinalAnomaly: true,
            experimentalDecision: decision);

        Assert.True(result.LegacyFinalAnomaly);
        Assert.True(result.FunctionalFinalAnomaly);
        Assert.True(result.CountsAsNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void DataQualityUncertain_WithLegacyNormal_ShouldRemainNormal()
    {
        var decision = CreateDecision(
            ExperimentalAnomalyDecisionKind.DataQualityUncertain,
            countsAsNewAnomaly: true,
            countsAsNewEvidence: true);

        var result = _resolver.Resolve(
            legacyFinalAnomaly: false,
            experimentalDecision: decision);

        Assert.False(result.LegacyFinalAnomaly);
        Assert.False(result.FunctionalFinalAnomaly);
        Assert.False(result.CountsAsNewEvidence);
        Assert.False(result.UsedLegacyFallback);
    }

    [Fact]
    public void IncreasingNoiseContinuation_ShouldRemainFunctionalAnomaly()
    {
        var decision = CreateDecision(
            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation,
            countsAsNewAnomaly: false,
            countsAsNewEvidence: false);

        var result = _resolver.Resolve(
            legacyFinalAnomaly: false,
            experimentalDecision: decision);

        Assert.False(result.LegacyFinalAnomaly);
        Assert.True(result.FunctionalFinalAnomaly);
        Assert.False(result.CountsAsNewEvidence);
        Assert.False(result.UsedLegacyFallback);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation,
            result.SourceDecision);
    }
    private static ExperimentalAnomalyDecision CreateDecision(
        ExperimentalAnomalyDecisionKind kind,
        bool countsAsNewAnomaly,
        bool countsAsNewEvidence)
    {
        return new ExperimentalAnomalyDecision(
            Kind: kind,
            CountsAsNewAnomaly: countsAsNewAnomaly,
            CountsAsNewEvidence: countsAsNewEvidence,
            Reasons: new[]
            {
                "TestDecision"
            });
    }
}