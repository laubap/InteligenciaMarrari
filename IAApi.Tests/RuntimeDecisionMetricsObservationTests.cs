using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeDecisionMetricsObservationTests
{
    [Fact]
    public void Snapshot_SemDecisoes_NaoTemJanelaTemporal()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        var snapshot =
            metrics.Snapshot();

        Assert.Null(
            snapshot.FirstDecisionAtUtc);

        Assert.Null(
            snapshot.LastDecisionAtUtc);
    }

    [Fact]
    public void Record_PreenchePrimeiraEUltimaDecisao()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            Decision("LegacyNormal"));

        var first =
            metrics.Snapshot();

        Thread.Sleep(5);

        metrics.Record(
            Decision("NewAnomaly"));

        var second =
            metrics.Snapshot();

        Assert.NotNull(
            second.FirstDecisionAtUtc);

        Assert.NotNull(
            second.LastDecisionAtUtc);

        Assert.Equal(
            first.FirstDecisionAtUtc,
            second.FirstDecisionAtUtc);

        Assert.True(
            second.LastDecisionAtUtc >=
            second.FirstDecisionAtUtc);
    }

    private static RuntimeAnomalyDecision Decision(
        string source)
    {
        return new RuntimeAnomalyDecision(
            LegacyFinalAnomaly: false,
            EmitAnomaly: false,
            EmitNewEvidence: false,
            UsedLegacyFallback: false,
            Source: source,
            Reasons: ["Test"]);
    }
}