using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeDecisionAuditDataTests
{
    [Fact]
    public void From_PreservaLegacyOperacionalEContextoSeparadamente()
    {
        var decision =
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: true,
                EmitAnomaly: false,
                EmitNewEvidence: false,
                UsedLegacyFallback: false,
                Source:
                    RuntimeFunctionalDecisionSources
                        .StableUnresolvedContinuation,
                Reasons:
                [
                    "IncidentAlreadyOpen",
                    "CandidateBaselineReady"
                ]);

        var context =
            new RuntimeFunctionalDecisionContext(
                IncidentId: "incident:test",
                CandidateBaselineReady: true,
                RegimeState:
                    "StableUnresolvedDeviation",
                AgainstActive: "Abnormal",
                MadCandidateVote: false,
                ZCandidateVote: false,
                EwmaCandidateVote: false,
                CriticalGap: false,
                OperationalBaselineChanged: false,
                Promotion: false);

        var audit =
            RuntimeDecisionAuditData.From(
                decision,
                context);

        Assert.True(audit.LegacyFinalAnomaly);
        Assert.False(audit.EmitAnomaly);
        Assert.False(audit.EmitNewEvidence);
        Assert.False(audit.UsedLegacyFallback);
        Assert.Equal(
            RuntimeFunctionalDecisionSources
                .StableUnresolvedContinuation,
            audit.Source);
        Assert.Equal(
            "incident:test",
            audit.IncidentId);
        Assert.Equal(
            "StableUnresolvedDeviation",
            audit.RegimeState);
        Assert.True(
            audit.CandidateBaselineReady);
    }

    [Fact]
    public void From_SemContexto_NaoInventaDadosDeRegime()
    {
        var decision =
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: true,
                EmitAnomaly: true,
                EmitNewEvidence: true,
                UsedLegacyFallback: true,
                Source: "LegacyFallback",
                Reasons:
                    ["FunctionalDecisionUnavailable"]);

        var audit =
            RuntimeDecisionAuditData.From(
                decision,
                context: null);

        Assert.True(audit.LegacyFinalAnomaly);
        Assert.True(audit.EmitAnomaly);
        Assert.True(audit.EmitNewEvidence);
        Assert.True(audit.UsedLegacyFallback);
        Assert.Null(audit.IncidentId);
        Assert.Null(audit.RegimeState);
        Assert.Null(audit.CandidateBaselineReady);
    }

    [Fact]
    public void From_CopiaReasons_ParaSnapshotEstavel()
    {
        var reasons =
            new List<string>
            {
                "ReasonA"
            };

        var decision =
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: true,
                EmitAnomaly: true,
                EmitNewEvidence: true,
                UsedLegacyFallback: false,
                Source: "NewAnomaly",
                Reasons: reasons);

        var audit =
            RuntimeDecisionAuditData.From(
                decision,
                context: null);

        reasons.Add("ReasonB");

        Assert.Single(audit.Reasons);
        Assert.Equal(
            "ReasonA",
            audit.Reasons[0]);
    }
}