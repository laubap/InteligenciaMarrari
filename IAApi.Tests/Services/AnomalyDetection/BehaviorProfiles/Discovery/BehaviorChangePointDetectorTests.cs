using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorChangePointDetectorTests
{
    [Fact]
    public void Detect_SemMudanca_DeveRetornarNenhumCandidato()
    {
        var segment =
            CreateSegment(
                Enumerable
                    .Repeat(40f, 80)
                    .ToArray());

        var detector =
            CreateDetector();

        var result =
            detector.Detect(segment);

        Assert.Empty(result);
    }

    [Fact]
    public void Detect_MudancaPersistenteDe40Para75_DeveEncontrarUmCandidato()
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
            CreateSegment(values);

        var detector =
            CreateDetector();

        var result =
            detector.Detect(segment);

        var candidate =
            Assert.Single(result);

        Assert.InRange(
            candidate.ReadingIndex,
            27,
            33);

        Assert.Equal(
            35,
            candidate.AbsoluteShift,
            precision: 5);

        Assert.True(
            candidate.RobustEffect >= 4);

        Assert.True(
            candidate.RangeFraction >= 0.10);

        Assert.Contains(
            "Mudança persistente de nível",
            candidate.Reason);
    }

    [Fact]
    public void Detect_RuidoPequeno_DeveRetornarNenhumCandidato()
    {
        var values =
            Enumerable
                .Range(0, 100)
                .Select(index =>
                    index % 2 == 0
                        ? 40f
                        : 41f)
                .ToArray();

        var segment =
            CreateSegment(values);

        var detector =
            CreateDetector();

        var result =
            detector.Detect(segment);

        Assert.Empty(result);
    }

    [Fact]
    public void Detect_DuasMudancasPersistentes_DeveEncontrarDoisCandidatos()
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
            CreateSegment(values);

        var detector =
            CreateDetector();

        var result =
            detector.Detect(segment);

        Assert.Equal(
            2,
            result.Count);

        Assert.InRange(
            result[0].ReadingIndex,
            22,
            28);

        Assert.InRange(
            result[1].ReadingIndex,
            47,
            53);

        Assert.True(
            result[0].MedianAfter >
            result[0].MedianBefore);

        Assert.True(
            result[1].MedianAfter <
            result[1].MedianBefore);
    }

    [Fact]
    public void Detect_SegmentoCurto_DeveRetornarNenhumCandidato()
    {
        var segment =
            CreateSegment(
                Enumerable
                    .Repeat(40f, 15)
                    .ToArray());

        var detector =
            CreateDetector();

        var result =
            detector.Detect(segment);

        Assert.Empty(result);
    }

    private static BehaviorChangePointDetector CreateDetector()
    {
        return new BehaviorChangePointDetector(
            new BehaviorChangePointOptions
            {
                WindowSize =
                    10,

                MinimumRobustEffect =
                    4,

                MinimumRangeFraction =
                    0.10,

                SuppressionWindow =
                    12
            });
    }

    private static BehaviorHistorySegment CreateSegment(
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
                    (value, index) =>
                        new LeituraBruta
                        {
                            DataHora =
                                start.AddMinutes(index),

                            Valor =
                                value
                        })
                .ToArray();

        return new BehaviorHistorySegment
        {
            Index =
                0,

            Inicio =
                history[0].DataHora,

            Fim =
                history[^1].DataHora,

            Historico =
                history
        };
    }
}