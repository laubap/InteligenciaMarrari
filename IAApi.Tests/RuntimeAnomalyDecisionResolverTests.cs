using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeAnomalyDecisionResolverTests
{
    private readonly RuntimeAnomalyDecisionResolver _resolver =
        new();

    [Fact]
    public void FlagOff_LegacyAnomalo_PreservaExatamenteLegacy()
    {
        var legacy =
            new DetectorResult
            {
                EhAnomalia = true
            };

        var result =
            _resolver.Resolve(
                legacy,
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = false
                });

        Assert.True(result.LegacyFinalAnomaly);
        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);
        Assert.Equal("LegacyFeatureDisabled", result.Source);
        Assert.Contains("LegacyPreserved", result.Reasons);
    }

    [Fact]
    public void FlagOff_LegacyNormal_NaoInventaAnomalia()
    {
        var legacy =
            new DetectorResult
            {
                EhAnomalia = false
            };

        var result =
            _resolver.Resolve(
                legacy,
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = false
                });

        Assert.False(result.LegacyFinalAnomaly);
        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);
    }

    [Fact]
    public void FlagOn_SemDecisaoFuncional_LegacyAnomaloFazFallback()
    {
        var legacy =
            new DetectorResult
            {
                EhAnomalia = true
            };

        var result =
            _resolver.Resolve(
                legacy,
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = true
                },
                functionalCandidate: null);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);
        Assert.Equal("LegacyFallback", result.Source);
        Assert.Contains(
            "FunctionalDecisionUnavailable",
            result.Reasons);
    }

    [Fact]
    public void FlagOn_SemDecisaoFuncional_LegacyNormalPermaneceNormal()
    {
        var legacy =
            new DetectorResult
            {
                EhAnomalia = false
            };

        var result =
            _resolver.Resolve(
                legacy,
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = true
                },
                functionalCandidate: null);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.True(result.UsedLegacyFallback);
    }

    [Fact]
    public void FlagOn_ComCandidatoFuncional_UsaDecisaoFuncional()
    {
        var legacy =
            new DetectorResult
            {
                EhAnomalia = true
            };

        var candidate =
            new RuntimeFunctionalDecisionCandidate(
                EmitAnomaly: false,
                EmitNewEvidence: true,
                Source: "CandidateMismatch",
                Reasons:
                    ["CandidateBaselineMismatch"]);

        var result =
            _resolver.Resolve(
                legacy,
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = true
                },
                candidate);

        Assert.True(result.LegacyFinalAnomaly);
        Assert.False(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.False(result.UsedLegacyFallback);
        Assert.Equal("CandidateMismatch", result.Source);
        Assert.Contains(
            "RuntimeFunctionalDecisionAccepted",
            result.Reasons);
    }

    [Fact]
    public void CandidatoFuncional_NaoMutaDetectorResultLegacy()
    {
        var legacy =
            new DetectorResult
            {
                EhAnomalia = true
            };

        var candidate =
            new RuntimeFunctionalDecisionCandidate(
                EmitAnomaly: false,
                EmitNewEvidence: false,
                Source: "Continuation",
                Reasons:
                    ["StableUnresolvedContinuation"]);

        _resolver.Resolve(
            legacy,
            new RuntimeAnomalyDecisionOptions
            {
                Enabled = true
            },
            candidate);

        Assert.True(legacy.EhAnomalia);
    }
}