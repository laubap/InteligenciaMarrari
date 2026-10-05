using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorCandidateEpisodeProfileFeaturesTests
{
    [Fact]
    public void Characterize_EpisodioValido_DevePreencherProfileFeatures()
    {
        var start =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var values =
            new float[]
            {
                40f, 41f, 40f, 41f, 40f,
                41f, 40f, 41f, 40f, 41f,
                40f, 41f, 40f, 41f, 40f,
                41f, 40f, 41f, 40f, 41f
            };

        var history =
            values
                .Select(
                    (value, index) =>
                        new LeituraBruta
                        {
                            DataHora =
                                start.AddMinutes(index),

                            Valor =
                                value
                        })
                .ToArray();

        var episode =
            new BehaviorCandidateEpisode
            {
                SourceSegmentIndex =
                    0,

                EpisodeIndex =
                    0,

                StartReadingIndex =
                    0,

                EndReadingIndex =
                    history.Length - 1,

                Inicio =
                    history[0].DataHora,

                Fim =
                    history[^1].DataHora,

                Historico =
                    history
            };

        var statisticsCalculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var characterizer =
            new BehaviorCandidateEpisodeCharacterizer(
                statisticsCalculator);

        var result =
            characterizer.Characterize(
                episode);

        Assert.True(
            result.IsCharacterizable);

        Assert.NotNull(
            result.ProfileFeatures);

        var features =
            result.ProfileFeatures!;

        Assert.Equal(
            result.UsedSampleCount,
            features.SampleCount);

        Assert.Equal(
            40.5,
            features.Median,
            precision: 5);

        Assert.Equal(
            1.0,
            features.TypicalAbsoluteStep,
            precision: 5);

        Assert.Equal(
            1.0,
            features.ChangeRatio,
            precision: 5);

        Assert.Equal(
            0.0,
            features.ZeroDeltaRatio,
            precision: 5);

        Assert.True(
            features.RobustScale > 0);

        Assert.True(
            features.ReversalRatio > 0.9);
    }
}
