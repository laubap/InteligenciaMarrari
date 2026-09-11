using IAApi.Benchmark.ExperimentalAnomalyDecisions;

namespace IAApi.Tests;

public class ExperimentalAnomalyDecisionEngineTests
{
    private readonly ExperimentalAnomalyDecisionEngine _engine = new();

    [Fact]
    public void LegacyNormal_ShouldRemainLegacyNormal()
    {
        var input = CreateInput(
            legacyFinalAnomaly: false);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.LegacyNormal,
            result.Kind);

        Assert.False(result.CountsAsNewAnomaly);
        Assert.False(result.CountsAsNewEvidence);

        Assert.Contains(
            "LegacyFinalAnomalyFalse",
            result.Reasons);
    }

    [Fact]
    public void LegacyAnomaly_WithoutSafeContinuationConditions_ShouldRemainNewAnomaly()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: null,
            candidateBaselineReady: false,
            regimeState: "TransitionSuspected",
            againstActive: "Abnormal");

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.NewAnomaly,
            result.Kind);

        Assert.True(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "LegacyFinalAnomalyPreserved",
            result.Reasons);

        Assert.Contains(
            "NoOpenIncident",
            result.Reasons);

        Assert.Contains(
            "CandidateNotReady",
            result.Reasons);
    }

    [Fact]
    public void StableKnownDeviation_ShouldBecomeContinuation()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: false,
            zCandidateVote: false,
            ewmaCandidateVote: false,
            criticalGap: false,
            operationalBaselineChanged: false,
            promotion: false);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation,
            result.Kind);

        Assert.False(result.CountsAsNewAnomaly);
        Assert.False(result.CountsAsNewEvidence);

        Assert.Contains(
            "IncidentAlreadyOpen",
            result.Reasons);

        Assert.Contains(
            "CandidateBaselineReady",
            result.Reasons);

        Assert.Contains(
            "CandidateNotPromoted",
            result.Reasons);
    }

    [Fact]
    public void EwmaOnlyMismatch_ShouldStillAllowContinuation()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: false,
            zCandidateVote: false,
            ewmaCandidateVote: true,
            criticalGap: false,
            operationalBaselineChanged: false,
            promotion: false);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.StableUnresolvedContinuation,
            result.Kind);

        Assert.False(result.CountsAsNewAnomaly);
        Assert.False(result.CountsAsNewEvidence);

        Assert.Contains(
            "EwmaOnlyTemporalMismatch",
            result.Reasons);
    }

    [Fact]
    public void MadCandidateMismatch_ShouldRemainVisibleAsNewEvidence()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: true,
            zCandidateVote: false);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.CandidateMismatch,
            result.Kind);

        Assert.False(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "CandidateBaselineMismatch",
            result.Reasons);

        Assert.Contains(
            "MadAgainstCandidateAbnormal",
            result.Reasons);
    }

    [Fact]
    public void ZCandidateMismatch_ShouldRemainVisibleAsNewEvidence()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            madCandidateVote: false,
            zCandidateVote: true);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.CandidateMismatch,
            result.Kind);

        Assert.False(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "CandidateBaselineMismatch",
            result.Reasons);

        Assert.Contains(
            "ZAgainstCandidateAbnormal",
            result.Reasons);
    }

    [Fact]
    public void CriticalGap_ShouldPreserveLegacyConservatively()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            criticalGap: true);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.DataQualityUncertain,
            result.Kind);

        Assert.True(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "CriticalDataQualityGap",
            result.Reasons);

        Assert.Contains(
            "LegacyDecisionPreservedConservatively",
            result.Reasons);
    }

    [Fact]
    public void OperationalBaselineChange_ShouldBlockContinuation()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            operationalBaselineChanged: true);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.NewAnomaly,
            result.Kind);

        Assert.True(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "OperationalBaselineChanged",
            result.Reasons);
    }

    [Fact]
    public void Promotion_ShouldBlockContinuation()
    {
        var input = CreateInput(
            legacyFinalAnomaly: true,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Abnormal",
            promotion: true);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.NewAnomaly,
            result.Kind);

        Assert.True(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "CandidatePromoted",
            result.Reasons);
    }

    [Fact]
    public void CandidateMismatch_ShouldRemainEvidence_EvenWhenLegacyIsNormal()
    {
        var input = CreateInput(
            legacyFinalAnomaly: false,
            incidentId: "incident:test",
            candidateBaselineReady: true,
            regimeState: "StableUnresolvedDeviation",
            againstActive: "Normal",
            madCandidateVote: true,
            zCandidateVote: false);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.CandidateMismatch,
            result.Kind);

        Assert.False(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "CandidateBaselineMismatch",
            result.Reasons);

        Assert.Contains(
            "MadAgainstCandidateAbnormal",
            result.Reasons);
    }

    [Fact]
    public void IncreasingNoiseStarted_ShouldBecomeNewAnomaly()
    {
        var input = CreateInput(
            legacyFinalAnomaly: false,
            regimeState: "StableKnownRegime",
            increasingNoise: true,
            increasingNoiseStarted: true);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.NewAnomaly,
            result.Kind);

        Assert.True(result.CountsAsNewAnomaly);
        Assert.True(result.CountsAsNewEvidence);

        Assert.Contains(
            "IncreasingNoiseDetected",
            result.Reasons);

        Assert.Contains(
            "IndependentVarianceExpansionEvidence",
            result.Reasons);
    }

    [Fact]
    public void IncreasingNoiseContinuation_ShouldRemainAnomalousWithoutNewEvidence()
    {
        var input = CreateInput(
            legacyFinalAnomaly: false,
            regimeState: "StableKnownRegime",
            increasingNoise: true,
            increasingNoiseContinuation: true);

        var result = _engine.Evaluate(input);

        Assert.Equal(
            ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation,
            result.Kind);

        Assert.False(result.CountsAsNewAnomaly);
        Assert.False(result.CountsAsNewEvidence);

        Assert.Contains(
            "IncreasingNoiseStillActive",
            result.Reasons);
    }
    private static ExperimentalAnomalyDecisionInput CreateInput(
        bool legacyFinalAnomaly = true,
        string? incidentId = null,
        bool candidateBaselineReady = false,
        string regimeState = "TransitionSuspected",
        string againstActive = "Abnormal",
        bool madCandidateVote = false,
        bool zCandidateVote = false,
        bool ewmaCandidateVote = false,
        bool criticalGap = false,
        bool operationalBaselineChanged = false,
        bool promotion = false,
        bool increasingNoise = false,
        bool increasingNoiseStarted = false,
        bool increasingNoiseContinuation = false)
    {
        return new ExperimentalAnomalyDecisionInput(
            FastWindow: 1,
            MinuteIndex: 100,
            Timestamp: new DateTime(
                2025,
                1,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc),

            Tag: "TEST.TAG",

            LegacyFinalAnomaly: legacyFinalAnomaly,
            LegacyScore: 4.0,

            IncidentId: incidentId,
            RegimeEpisodeId: "dual-episode:test",

            CandidateBaselineReady: candidateBaselineReady,
            RegimeState: regimeState,
            AgainstActive: againstActive,

            MadCandidateVote: madCandidateVote,
            ZCandidateVote: zCandidateVote,
            EwmaCandidateVote: ewmaCandidateVote,

            CriticalGap: criticalGap,
            OperationalBaselineChanged: operationalBaselineChanged,
            Promotion: promotion,

            LegacyAnomalousDetectors: new[]
            {
                "Mediana e MAD",
                "Persistência"
            })
        {
            IncreasingNoise = increasingNoise,
            IncreasingNoiseStarted = increasingNoiseStarted,
            IncreasingNoiseContinuation = increasingNoiseContinuation
        };
    }
}



