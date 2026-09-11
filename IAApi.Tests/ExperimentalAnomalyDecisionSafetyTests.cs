using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using Xunit;

namespace IAApi.Tests;

public sealed class ExperimentalAnomalyDecisionSafetyTests
{
    private readonly ExperimentalAnomalyDecisionEngine _engine = new();
    private readonly FunctionalExperimentalAnomalyDecisionResolver _resolver = new();

    [Fact]
    public void CriticalGap_ComLegacyAnomalo_DeveTerPrioridadeSobreCandidateMismatch()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: true,
            zCandidateVote: true,
            criticalGap: true,
            incidentId: "incident-1");

        var decision = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.DataQualityUncertain,
            decision.Kind);

        Assert.True(decision.CountsAsNewAnomaly);
        Assert.True(decision.CountsAsNewEvidence);

        Assert.Contains(
            "CriticalDataQualityGap",
            decision.Reasons);

        Assert.Contains(
            "LegacyDecisionPreservedConservatively",
            decision.Reasons);
    }

    [Fact]
    public void CriticalGap_ComLegacyAnomaloECandidateCompativel_PreservaLegacy()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: false,
            zCandidateVote: false,
            criticalGap: true,
            incidentId: "incident-1");

        var decision = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.DataQualityUncertain,
            decision.Kind);

        Assert.True(decision.CountsAsNewAnomaly);
        Assert.True(decision.CountsAsNewEvidence);

        var functional = _resolver.Resolve(
            legacyFinalAnomaly: true,
            experimentalDecision: decision);

        Assert.True(functional.FunctionalFinalAnomaly);
        Assert.True(functional.CountsAsNewEvidence);
        Assert.False(functional.UsedLegacyFallback);
    }

    [Fact]
    public void CandidateMismatch_SemCriticalGap_PreservaNovaEvidenciaBParaC()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: true,
            zCandidateVote: false,
            criticalGap: false,
            incidentId: "incident-1");

        var decision = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.CandidateMismatch,
            decision.Kind);

        Assert.False(decision.CountsAsNewAnomaly);
        Assert.True(decision.CountsAsNewEvidence);

        var functional = _resolver.Resolve(
            legacyFinalAnomaly: true,
            experimentalDecision: decision);

        Assert.False(functional.FunctionalFinalAnomaly);
        Assert.True(functional.CountsAsNewEvidence);
        Assert.False(functional.UsedLegacyFallback);
    }

    [Fact]
    public void CandidateMismatch_ComLegacyNormal_AindaPermaneceComoNovaEvidencia()
    {
        var input = CreateInput(
            legacyFinalAnomaly: false,
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: false,
            zCandidateVote: true,
            criticalGap: false,
            incidentId: "incident-1");

        var decision = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.CandidateMismatch,
            decision.Kind);

        Assert.False(decision.CountsAsNewAnomaly);
        Assert.True(decision.CountsAsNewEvidence);

        var functional = _resolver.Resolve(
            legacyFinalAnomaly: false,
            experimentalDecision: decision);

        Assert.False(functional.FunctionalFinalAnomaly);
        Assert.True(functional.CountsAsNewEvidence);
        Assert.False(functional.UsedLegacyFallback);
    }

    private static ExperimentalAnomalyDecisionInput CreateInput(
        bool legacyFinalAnomaly,
        bool candidateBaselineReady,
        string regimeState,
        string againstActive,
        bool madCandidateVote,
        bool zCandidateVote,
        bool criticalGap,
        string? incidentId)
    {
        return new ExperimentalAnomalyDecisionInput(
            FastWindow: 1,
            MinuteIndex: 12345,
            Timestamp: new DateTime(
                2026,
                8,
                20,
                12,
                0,
                0,
                DateTimeKind.Utc),
            Tag: "TEST.TAG",
            LegacyFinalAnomaly: legacyFinalAnomaly,
            LegacyScore: 4.2,
            IncidentId: incidentId,
            RegimeEpisodeId: "episode-1",
            CandidateBaselineReady: candidateBaselineReady,
            RegimeState: regimeState,
            AgainstActive: againstActive,
            MadCandidateVote: madCandidateVote,
            ZCandidateVote: zCandidateVote,
            EwmaCandidateVote: false,
            CriticalGap: criticalGap,
            OperationalBaselineChanged: false,
            Promotion: false,
            LegacyAnomalousDetectors: ["MAD", "ZScore"]);
    }
}