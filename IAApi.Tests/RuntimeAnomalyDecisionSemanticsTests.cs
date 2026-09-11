using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeAnomalyDecisionSemanticsTests
{
    [Fact]
    public void NovaAnomalia_RequerNovaInterpretacao()
    {
        var decision = Decision(
            emitAnomaly: true,
            emitNewEvidence: true);

        Assert.True(decision.RequiresNewInterpretation);
        Assert.False(decision.IsAnomalyContinuation);
    }

    [Fact]
    public void ContinuacaoAnomala_NaoRequerNovaInterpretacao()
    {
        var decision = Decision(
            emitAnomaly: true,
            emitNewEvidence: false);

        Assert.False(decision.RequiresNewInterpretation);
        Assert.True(decision.IsAnomalyContinuation);
    }

    [Fact]
    public void Normal_NaoEhInterpretacaoNemContinuacao()
    {
        var decision = Decision(
            emitAnomaly: false,
            emitNewEvidence: false);

        Assert.False(decision.RequiresNewInterpretation);
        Assert.False(decision.IsAnomalyContinuation);
    }

    [Fact]
    public void EvidenciaSemEmissao_NaoRequerInterpretacao()
    {
        var decision = Decision(
            emitAnomaly: false,
            emitNewEvidence: true);

        Assert.False(decision.RequiresNewInterpretation);
        Assert.False(decision.IsAnomalyContinuation);
    }

    private static RuntimeAnomalyDecision Decision(
        bool emitAnomaly,
        bool emitNewEvidence)
    {
        return new RuntimeAnomalyDecision(
            LegacyFinalAnomaly: false,
            EmitAnomaly: emitAnomaly,
            EmitNewEvidence: emitNewEvidence,
            UsedLegacyFallback: false,
            Source: "TEST",
            Reasons: []);
    }
}
