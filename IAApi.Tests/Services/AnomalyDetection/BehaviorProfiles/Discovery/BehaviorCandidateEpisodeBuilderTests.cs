using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorCandidateEpisodeBuilderTests
{
    [Fact]
    public void Build_SemChangePoints_DeveCriarUmUnicoEpisodio()
    {
        var segment =
            CreateSegment(
                0,
                Enumerable
                    .Repeat(40f, 20)
                    .ToArray());

        var builder =
            new BehaviorCandidateEpisodeBuilder();

        var result =
            builder.Build(
                segment,
                []);

        var episode =
            Assert.Single(result);

        Assert.Equal(
            0,
            episode.EpisodeIndex);

        Assert.Equal(
            0,
            episode.StartReadingIndex);

        Assert.Equal(
            19,
            episode.EndReadingIndex);

        Assert.Equal(
            20,
            episode.QuantidadeLeituras);

        Assert.Null(
            episode.OpeningChangePoint);

        Assert.Equal(
            segment.Inicio,
            episode.Inicio);

        Assert.Equal(
            segment.Fim,
            episode.Fim);
    }

    [Fact]
    public void Build_UmChangePoint_DeveCriarDoisEpisodios()
    {
        var values =
            Enumerable
                .Repeat(40f, 30)
                .Concat(
                    Enumerable.Repeat(
                        75f,
                        30))
                .ToArray();

        var segment =
            CreateSegment(
                0,
                values);

        var changePoint =
            CreateChangePoint(
                segmentIndex: 0,
                readingIndex: 30,
                segment);

        var builder =
            new BehaviorCandidateEpisodeBuilder();

        var result =
            builder.Build(
                segment,
                [changePoint]);

        Assert.Equal(
            2,
            result.Count);

        var first =
            result[0];

        Assert.Equal(
            0,
            first.StartReadingIndex);

        Assert.Equal(
            29,
            first.EndReadingIndex);

        Assert.Equal(
            30,
            first.QuantidadeLeituras);

        Assert.All(
            first.Historico,
            reading =>
                Assert.Equal(
                    40f,
                    reading.Valor));

        Assert.Null(
            first.OpeningChangePoint);

        var second =
            result[1];

        Assert.Equal(
            30,
            second.StartReadingIndex);

        Assert.Equal(
            59,
            second.EndReadingIndex);

        Assert.Equal(
            30,
            second.QuantidadeLeituras);

        Assert.All(
            second.Historico,
            reading =>
                Assert.Equal(
                    75f,
                    reading.Valor));

        Assert.Same(
            changePoint,
            second.OpeningChangePoint);
    }

    [Fact]
    public void Build_DoisChangePoints_DeveCriarTresEpisodios()
    {
        var values =
            Enumerable
                .Repeat(40f, 25)
                .Concat(
                    Enumerable.Repeat(
                        75f,
                        25))
                .Concat(
                    Enumerable.Repeat(
                        40f,
                        25))
                .ToArray();

        var segment =
            CreateSegment(
                0,
                values);

        var firstChange =
            CreateChangePoint(
                0,
                25,
                segment);

        var secondChange =
            CreateChangePoint(
                0,
                50,
                segment);

        var builder =
            new BehaviorCandidateEpisodeBuilder();

        var result =
            builder.Build(
                segment,
                [
                    firstChange,
                    secondChange
                ]);

        Assert.Equal(
            3,
            result.Count);

        Assert.Equal(
            (0, 24),
            (
                result[0].StartReadingIndex,
                result[0].EndReadingIndex
            ));

        Assert.Equal(
            (25, 49),
            (
                result[1].StartReadingIndex,
                result[1].EndReadingIndex
            ));

        Assert.Equal(
            (50, 74),
            (
                result[2].StartReadingIndex,
                result[2].EndReadingIndex
            ));

        Assert.All(
            result[0].Historico,
            reading =>
                Assert.Equal(
                    40f,
                    reading.Valor));

        Assert.All(
            result[1].Historico,
            reading =>
                Assert.Equal(
                    75f,
                    reading.Valor));

        Assert.All(
            result[2].Historico,
            reading =>
                Assert.Equal(
                    40f,
                    reading.Valor));
    }

    [Fact]
    public void Build_DeveIgnorarChangePointDeOutroSegmento()
    {
        var segment =
            CreateSegment(
                3,
                Enumerable
                    .Repeat(40f, 20)
                    .ToArray());

        var foreignChangePoint =
            CreateChangePoint(
                segmentIndex: 99,
                readingIndex: 10,
                segment);

        var builder =
            new BehaviorCandidateEpisodeBuilder();

        var result =
            builder.Build(
                segment,
                [foreignChangePoint]);

        var episode =
            Assert.Single(result);

        Assert.Equal(
            20,
            episode.QuantidadeLeituras);
    }

    [Fact]
    public void Build_DeveOrdenarChangePointsAntesDeCriarEpisodios()
    {
        var segment =
            CreateSegment(
                0,
                Enumerable
                    .Range(0, 60)
                    .Select(index =>
                        (float)index)
                    .ToArray());

        var changeAt20 =
            CreateChangePoint(
                0,
                20,
                segment);

        var changeAt40 =
            CreateChangePoint(
                0,
                40,
                segment);

        var builder =
            new BehaviorCandidateEpisodeBuilder();

        var result =
            builder.Build(
                segment,
                [
                    changeAt40,
                    changeAt20
                ]);

        Assert.Equal(
            3,
            result.Count);

        Assert.Equal(
            (0, 19),
            (
                result[0].StartReadingIndex,
                result[0].EndReadingIndex
            ));

        Assert.Equal(
            (20, 39),
            (
                result[1].StartReadingIndex,
                result[1].EndReadingIndex
            ));

        Assert.Equal(
            (40, 59),
            (
                result[2].StartReadingIndex,
                result[2].EndReadingIndex
            ));
    }

    [Fact]
    public void Build_ChangePointsDuplicados_DeveManterSomenteUmCorte()
    {
        var segment =
            CreateSegment(
                0,
                Enumerable
                    .Repeat(40f, 40)
                    .ToArray());

        var weak =
            CreateChangePoint(
                0,
                20,
                segment,
                robustEffect: 5,
                boundaryJump: 10);

        var strong =
            CreateChangePoint(
                0,
                20,
                segment,
                robustEffect: 8,
                boundaryJump: 30);

        var builder =
            new BehaviorCandidateEpisodeBuilder();

        var result =
            builder.Build(
                segment,
                [
                    weak,
                    strong
                ]);

        Assert.Equal(
            2,
            result.Count);

        Assert.Same(
            strong,
            result[1].OpeningChangePoint);
    }

    [Fact]
    public void Build_SegmentoVazio_DeveRetornarVazio()
    {
        var segment =
            new BehaviorHistorySegment
            {
                Index =
                    0,

                Inicio =
                    DateTime.UtcNow,

                Fim =
                    DateTime.UtcNow,

                Historico =
                    []
            };

        var builder =
            new BehaviorCandidateEpisodeBuilder();

        var result =
            builder.Build(
                segment,
                []);

        Assert.Empty(
            result);
    }

    private static BehaviorHistorySegment CreateSegment(
        int index,
        IReadOnlyList<float> values)
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
                    (value, readingIndex) =>
                        new LeituraBruta
                        {
                            DataHora =
                                start.AddMinutes(
                                    readingIndex),

                            Valor =
                                value
                        })
                .ToArray();

        return new BehaviorHistorySegment
        {
            Index =
                index,

            Inicio =
                history.Length > 0
                    ? history[0].DataHora
                    : start,

            Fim =
                history.Length > 0
                    ? history[^1].DataHora
                    : start,

            Historico =
                history
        };
    }

    private static BehaviorChangePointCandidate CreateChangePoint(
        int segmentIndex,
        int readingIndex,
        BehaviorHistorySegment segment,
        double robustEffect = 10,
        double boundaryJump = 35)
    {
        return new BehaviorChangePointCandidate
        {
            SegmentIndex =
                segmentIndex,

            ReadingIndex =
                readingIndex,

            Timestamp =
                segment.Historico[
                    readingIndex]
                    .DataHora,

            MedianBefore =
                40,

            MedianAfter =
                75,

            AbsoluteShift =
                35,

            RobustScale =
                1,

            RobustEffect =
                robustEffect,

            RangeFraction =
                1,

            BoundaryJump =
                boundaryJump,

            Reason =
                "Teste"
        };
    }
}