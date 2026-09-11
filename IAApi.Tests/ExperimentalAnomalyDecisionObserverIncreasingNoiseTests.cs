using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Tests;

public sealed class ExperimentalAnomalyDecisionObserverIncreasingNoiseTests
{
    [Fact]
    public async Task UmMissEntreEvidencias_NaoAbreNovoEpisodio()
    {
        var directory =
            Path.Combine(
                Path.GetTempPath(),
                "IAApi.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        await using (var observer =
            new ExperimentalAnomalyDecisionObserver(
                new ExperimentalAnomalyDecisionOptions
                {
                    Enabled = true
                },
                directory,
                "noise-hysteresis"))
        {
            var original =
                new DetectorResult
                {
                    EhAnomalia = false,
                    Score = 0
                };

            var started =
                await observer.ObserveAsync(
                    Row(84, increasingNoise: true),
                    original,
                    default,
                    "episode-noise",
                    null);

            Assert.NotNull(started);
            Assert.Equal(
                ExperimentalAnomalyDecisionKind.NewAnomaly,
                started.Kind);

            var miss =
                await observer.ObserveAsync(
                    Row(85, increasingNoise: false),
                    original,
                    default,
                    "episode-noise",
                    null);

            Assert.NotNull(miss);

            var recovered =
                await observer.ObserveAsync(
                    Row(86, increasingNoise: true),
                    original,
                    default,
                    "episode-noise",
                    null);

            Assert.NotNull(recovered);

            Assert.Equal(
                ExperimentalAnomalyDecisionKind.IncreasingNoiseContinuation,
                recovered.Kind);

            Assert.False(
                recovered.CountsAsNewAnomaly);

            Assert.False(
                recovered.CountsAsNewEvidence);
        }

        Directory.Delete(
            directory,
            recursive: true);
    }

    [Fact]
    public async Task DoisMissesEncerramEpisodioENovoTrueAbreNovoInicio()
    {
        var directory =
            Path.Combine(
                Path.GetTempPath(),
                "IAApi.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        await using (var observer =
            new ExperimentalAnomalyDecisionObserver(
                new ExperimentalAnomalyDecisionOptions
                {
                    Enabled = true
                },
                directory,
                "noise-hysteresis-end"))
        {
            var original =
                new DetectorResult
                {
                    EhAnomalia = false,
                    Score = 0
                };

            var started =
                await observer.ObserveAsync(
                    Row(84, increasingNoise: true),
                    original,
                    default,
                    "episode-noise",
                    null);

            Assert.NotNull(started);

            _ = await observer.ObserveAsync(
                Row(85, increasingNoise: false),
                original,
                default,
                "episode-noise",
                null);

            _ = await observer.ObserveAsync(
                Row(86, increasingNoise: false),
                original,
                default,
                "episode-noise",
                null);

            var restarted =
                await observer.ObserveAsync(
                    Row(87, increasingNoise: true),
                    original,
                    default,
                    "episode-noise",
                    null);

            Assert.NotNull(restarted);

            Assert.Equal(
                ExperimentalAnomalyDecisionKind.NewAnomaly,
                restarted.Kind);

            Assert.True(
                restarted.CountsAsNewAnomaly);

            Assert.True(
                restarted.CountsAsNewEvidence);
        }

        Directory.Delete(
            directory,
            recursive: true);
    }
    private static DualBaselineTraceRecord Row(
        long minute,
        bool increasingNoise)
    {
        return new DualBaselineTraceRecord
        {
            FastWindowIndex = 1,
            MinuteIndex = minute,
            Timestamp =
                new DateTime(
                    2024, 12, 31, 22, 0, 0,
                    DateTimeKind.Utc)
                .AddMinutes(minute),
            Tag = "TAG-NOISE",
            CurrentValue = 40,
            RegimeState = "StableKnownRegime",
            IncreasingNoiseEvidence =
                increasingNoise
        };
    }
}