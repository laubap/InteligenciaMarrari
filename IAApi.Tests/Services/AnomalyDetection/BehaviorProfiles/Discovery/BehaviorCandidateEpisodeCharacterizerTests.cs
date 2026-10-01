using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorCandidateEpisodeCharacterizerTests
{
    [Fact]
    public void Characterize_EpisodioValido_DeveGerarCaracteristicasEstatisticas()
    {
        var episode =
            CreateEpisode(
                Enumerable
                    .Range(1, 20)
                    .Select(value =>
                        (float)value)
                    .ToArray());

        var characterizer =
            CreateCharacterizer();

        var result =
            characterizer.Characterize(
                episode);

        Assert.True(
            result.IsCharacterizable);

        Assert.Equal(
            20,
            result.OriginalSampleCount);

        Assert.Equal(
            20,
            result.UsedSampleCount);

        Assert.Equal(
            0,
            result.RemovedOutliers);

        Assert.Equal(
            10.5,
            result.Media,
            precision: 5);

        Assert.Equal(
            10.5,
            result.Mediana,
            precision: 5);

        Assert.Equal(
            1,
            result.Minimo,
            precision: 5);

        Assert.Equal(
            20,
            result.Maximo,
            precision: 5);

        Assert.Equal(
            19,
            result.Amplitude,
            precision: 5);

        Assert.True(
            result.DesvioPadrao > 0);

        Assert.True(
            result.IntervaloInterquartil > 0);
    }

    [Fact]
    public void Characterize_EpisodioCurto_DeveRetornarNaoCaracterizavel()
    {
        var episode =
            CreateEpisode(
                Enumerable
                    .Repeat(40f, 9)
                    .ToArray());

        var characterizer =
            CreateCharacterizer();

        var result =
            characterizer.Characterize(
                episode);

        Assert.False(
            result.IsCharacterizable);

        Assert.Equal(
            9,
            result.OriginalSampleCount);

        Assert.Equal(
            0,
            result.UsedSampleCount);

        Assert.Contains(
            "mínimo necessário",
            result.Reason);
    }

    [Fact]
    public void Characterize_DeveAplicarIqrDentroDoEpisodio()
    {
        var values =
            Enumerable
                .Repeat(40f, 19)
                .Append(100f)
                .ToArray();

        var episode =
            CreateEpisode(
                values);

        var characterizer =
            CreateCharacterizer();

        var result =
            characterizer.Characterize(
                episode);

        Assert.True(
            result.IsCharacterizable);

        Assert.Equal(
            20,
            result.OriginalSampleCount);

        Assert.Equal(
            19,
            result.UsedSampleCount);

        Assert.Equal(
            1,
            result.RemovedOutliers);

        Assert.Equal(
            40,
            result.Media,
            precision: 5);

        Assert.Equal(
            40,
            result.Mediana,
            precision: 5);

        Assert.Equal(
            40,
            result.Minimo,
            precision: 5);

        Assert.Equal(
            40,
            result.Maximo,
            precision: 5);

        Assert.DoesNotContain(
            result.HistoricoLimpo,
            reading =>
                reading.Valor == 100f);
    }

    [Fact]
    public void Characterize_QuandoIqrDeixaPoucasAmostras_DeveRetornarNaoCaracterizavel()
    {
        var values =
            Enumerable
                .Repeat(40f, 9)
                .Append(100f)
                .ToArray();

        var episode =
            CreateEpisode(
                values);

        var characterizer =
            CreateCharacterizer();

        var result =
            characterizer.Characterize(
                episode);

        Assert.False(
            result.IsCharacterizable);

        Assert.Equal(
            10,
            result.OriginalSampleCount);

        Assert.Contains(
            "insuficiente",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Characterize_DevePreservarIdentidadeEDuracaoDoEpisodio()
    {
        var episode =
            CreateEpisode(
                Enumerable
                    .Repeat(75f, 20)
                    .ToArray(),
                sourceSegmentIndex: 3,
                episodeIndex: 7);

        var characterizer =
            CreateCharacterizer();

        var result =
            characterizer.Characterize(
                episode);

        Assert.True(
            result.IsCharacterizable);

        Assert.Equal(
            3,
            result.SourceSegmentIndex);

        Assert.Equal(
            7,
            result.EpisodeIndex);

        Assert.Equal(
            episode.Inicio,
            result.Inicio);

        Assert.Equal(
            episode.Fim,
            result.Fim);

        Assert.Equal(
            19,
            result.DuracaoMinutos,
            precision: 5);

        Assert.Equal(
            75,
            result.Media,
            precision: 5);

        Assert.Equal(
            75,
            result.Mediana,
            precision: 5);
    }

    private static BehaviorCandidateEpisodeCharacterizer
        CreateCharacterizer()
    {
        var statisticsCalculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        return new BehaviorCandidateEpisodeCharacterizer(
            statisticsCalculator);
    }

    private static BehaviorCandidateEpisode CreateEpisode(
        IReadOnlyList<float> values,
        int sourceSegmentIndex = 0,
        int episodeIndex = 0)
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

        return new BehaviorCandidateEpisode
        {
            SourceSegmentIndex =
                sourceSegmentIndex,

            EpisodeIndex =
                episodeIndex,

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
    }
}