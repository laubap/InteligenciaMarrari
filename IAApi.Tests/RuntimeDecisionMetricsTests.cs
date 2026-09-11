using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeDecisionMetricsTests
{
    [Fact]
    public void Record_IncrementaTotalEContadorDaSource()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            Decision(
                source: "NewAnomaly",
                emitAnomaly: true,
                emitNewEvidence: true,
                fallback: false));

        var snapshot =
            metrics.Snapshot();

        Assert.Equal(
            1,
            snapshot.TotalDecisions);

        Assert.Equal(
            1,
            snapshot.BySource["NewAnomaly"]);
    }

    [Fact]
    public void Record_SeparaEmitAnomalyTrueEFalse()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            Decision(
                "NewAnomaly",
                emitAnomaly: true,
                emitNewEvidence: true,
                fallback: false));

        metrics.Record(
            Decision(
                "StableUnresolvedContinuation",
                emitAnomaly: false,
                emitNewEvidence: false,
                fallback: false));

        var snapshot =
            metrics.Snapshot();

        Assert.Equal(
            1,
            snapshot.EmitAnomalyTrue);

        Assert.Equal(
            1,
            snapshot.EmitAnomalyFalse);
    }

    [Fact]
    public void Record_SeparaEmitNewEvidenceTrueEFalse()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            Decision(
                "CandidateMismatch",
                emitAnomaly: false,
                emitNewEvidence: true,
                fallback: false));

        metrics.Record(
            Decision(
                "StableUnresolvedContinuation",
                emitAnomaly: false,
                emitNewEvidence: false,
                fallback: false));

        var snapshot =
            metrics.Snapshot();

        Assert.Equal(
            1,
            snapshot.EmitNewEvidenceTrue);

        Assert.Equal(
            1,
            snapshot.EmitNewEvidenceFalse);
    }

    [Fact]
    public void Record_SeparaFallbackLegacy()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            Decision(
                "LegacyCanaryExcluded",
                emitAnomaly: true,
                emitNewEvidence: true,
                fallback: true));

        metrics.Record(
            Decision(
                "NewAnomaly",
                emitAnomaly: true,
                emitNewEvidence: true,
                fallback: false));

        var snapshot =
            metrics.Snapshot();

        Assert.Equal(
            1,
            snapshot.UsedLegacyFallbackTrue);

        Assert.Equal(
            1,
            snapshot.UsedLegacyFallbackFalse);
    }

    [Fact]
    public void Snapshot_AgrupaMultiplasDecisoesPorSource()
    {
        var metrics =
            new RuntimeDecisionMetrics();

        metrics.Record(
            Decision(
                "CandidateMismatch",
                emitAnomaly: false,
                emitNewEvidence: true,
                fallback: false));

        metrics.Record(
            Decision(
                "CandidateMismatch",
                emitAnomaly: false,
                emitNewEvidence: true,
                fallback: false));

        metrics.Record(
            Decision(
                "NewAnomaly",
                emitAnomaly: true,
                emitNewEvidence: true,
                fallback: false));

        var snapshot =
            metrics.Snapshot();

        Assert.Equal(
            3,
            snapshot.TotalDecisions);

        Assert.Equal(
            2,
            snapshot.BySource["CandidateMismatch"]);

        Assert.Equal(
            1,
            snapshot.BySource["NewAnomaly"]);
    }

    private static RuntimeAnomalyDecision Decision(
        string source,
        bool emitAnomaly,
        bool emitNewEvidence,
        bool fallback)
    {
        return new RuntimeAnomalyDecision(
            LegacyFinalAnomaly: true,
            EmitAnomaly: emitAnomaly,
            EmitNewEvidence: emitNewEvidence,
            UsedLegacyFallback: fallback,
            Source: source,
            Reasons: ["Test"]);
    }
}