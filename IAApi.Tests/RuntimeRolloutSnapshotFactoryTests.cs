using System.Text.Json;
using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeRolloutSnapshotFactoryTests
{
    private readonly RuntimeRolloutSnapshotFactory
        _sut =
            new(
                new RuntimeRolloutReadinessEvaluator());

    [Fact]
    public void Create_PreservaConfiguracaoEResultadoDoReadiness()
    {
        var generatedAt =
            DateTimeOffset.Parse(
                "2026-08-24T20:00:00+00:00");

        var snapshot =
            _sut.Create(
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = true,
                    CanaryOnly = true,
                    AllowedClientIds =
                        [" PK7 ", ""]
                },
                Metrics(
                    total: 100,
                    observationMinutes: 60),
                Options(),
                generatedAt);

        Assert.Equal(
            generatedAt,
            snapshot.GeneratedAtUtc);

        Assert.True(
            snapshot.RuntimeDecisionEnabled);

        Assert.True(
            snapshot.CanaryOnly);

        Assert.Equal(
            "Healthy",
            snapshot.Status);

        Assert.True(
            snapshot.ReadyToExpand);

        var clients =
            JsonSerializer.Deserialize<string[]>(
                snapshot.AllowedClientIdsJson);

        Assert.Equal(
            ["PK7"],
            clients);
    }

    [Fact]
    public void Create_AmostraInsuficiente_PersisteWarningEMotivos()
    {
        var snapshot =
            _sut.Create(
                new RuntimeAnomalyDecisionOptions
                {
                    Enabled = true,
                    CanaryOnly = true,
                    AllowedClientIds = ["PK7"]
                },
                Metrics(
                    total: 1,
                    observationMinutes: 0),
                Options(),
                DateTimeOffset.UtcNow);

        Assert.Equal(
            "Warning",
            snapshot.Status);

        Assert.False(
            snapshot.ReadyToExpand);

        var reasons =
            JsonSerializer.Deserialize<string[]>(
                snapshot.ReasonsJson)
            ?? Array.Empty<string>();

        Assert.Contains(
            "InsufficientDecisionSample",
            reasons);

        Assert.Contains(
            "InsufficientObservationWindow",
            reasons);
    }

    [Fact]
    public void Create_NaoAlteraContadoresDoSnapshotDeMetricas()
    {
        var metrics =
            Metrics(
                total: 25,
                observationMinutes: 45);

        var before =
            metrics.TotalDecisions;

        _sut.Create(
            new RuntimeAnomalyDecisionOptions
            {
                Enabled = true,
                CanaryOnly = true,
                AllowedClientIds = ["PK7"]
            },
            metrics,
            Options(),
            DateTimeOffset.UtcNow);

        Assert.Equal(
            before,
            metrics.TotalDecisions);
    }

    private static RuntimeRolloutReadinessOptions Options()
    {
        return new RuntimeRolloutReadinessOptions
        {
            MinimumDecisions = 20,
            MinimumObservationMinutes = 30,
            MaximumLegacyFallbackRate = 0.05,
            MaximumDataQualityUncertainRate = 0.20,
            MaximumCandidateMismatchRate = 0.25,
            MaximumStableUnresolvedContinuationRate = 0.60
        };
    }

    private static RuntimeDecisionMetricsSnapshot Metrics(
        long total,
        double observationMinutes)
    {
        var first =
            DateTimeOffset.Parse(
                "2026-08-24T18:00:00+00:00");

        var last =
            first.AddMinutes(
                observationMinutes);

        return new RuntimeDecisionMetricsSnapshot(
            TotalDecisions: total,
            EmitAnomalyTrue: 0,
            EmitAnomalyFalse: total,
            EmitNewEvidenceTrue: 0,
            EmitNewEvidenceFalse: total,
            UsedLegacyFallbackTrue: 0,
            UsedLegacyFallbackFalse: total,
            BySource:
                new Dictionary<string, long>(
                    StringComparer.OrdinalIgnoreCase),
            FirstDecisionAtUtc: first,
            LastDecisionAtUtc: last);
    }
}