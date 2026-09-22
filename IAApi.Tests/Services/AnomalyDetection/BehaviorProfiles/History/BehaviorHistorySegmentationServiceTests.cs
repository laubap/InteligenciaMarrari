using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.History;

public sealed class BehaviorHistorySegmentationServiceTests
{
    [Fact]
    public void Segment_SemGapCritico_DeveManterUmUnicoSegmento()
    {
        var inicio =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var collection =
            CreateCollection(
                thresholdMinutes: 5,
                [
                    Reading(inicio, 10),
                    Reading(inicio.AddMinutes(1), 11),
                    Reading(inicio.AddMinutes(2), 12),
                    Reading(inicio.AddMinutes(3), 13)
                ]);

        var service =
            new BehaviorHistorySegmentationService();

        var result =
            service.Segment(collection);

        Assert.Single(
            result.Segments);

        Assert.False(
            result.FoiSegmentado);

        Assert.Equal(
            4,
            result.TotalLeituras);

        Assert.Equal(
            5,
            result.GapThresholdMinutes);

        var segment =
            result.Segments[0];

        Assert.Equal(
            0,
            segment.Index);

        Assert.Equal(
            inicio,
            segment.Inicio);

        Assert.Equal(
            inicio.AddMinutes(3),
            segment.Fim);

        Assert.Equal(
            4,
            segment.QuantidadeLeituras);

        Assert.Null(
            segment.GapAnteriorMinutos);
    }

    [Fact]
    public void Segment_ComGapCritico_DeveCriarNovoSegmento()
    {
        var inicio =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var collection =
            CreateCollection(
                thresholdMinutes: 5,
                [
                    Reading(inicio, 10),
                    Reading(inicio.AddMinutes(1), 11),
                    Reading(inicio.AddMinutes(2), 12),

                    // Gap de 18 minutos:
                    // 00:02 -> 00:20
                    Reading(inicio.AddMinutes(20), 20),
                    Reading(inicio.AddMinutes(21), 21)
                ]);

        var service =
            new BehaviorHistorySegmentationService();

        var result =
            service.Segment(collection);

        Assert.Equal(
            2,
            result.QuantidadeSegmentos);

        Assert.True(
            result.FoiSegmentado);

        Assert.Equal(
            5,
            result.TotalLeituras);

        var first =
            result.Segments[0];

        Assert.Equal(
            0,
            first.Index);

        Assert.Equal(
            3,
            first.QuantidadeLeituras);

        Assert.Equal(
            inicio,
            first.Inicio);

        Assert.Equal(
            inicio.AddMinutes(2),
            first.Fim);

        Assert.Null(
            first.GapAnteriorMinutos);

        var second =
            result.Segments[1];

        Assert.Equal(
            1,
            second.Index);

        Assert.Equal(
            2,
            second.QuantidadeLeituras);

        Assert.Equal(
            inicio.AddMinutes(20),
            second.Inicio);

        Assert.Equal(
            inicio.AddMinutes(21),
            second.Fim);

        Assert.Equal(
            18,
            second.GapAnteriorMinutos);
    }

    [Fact]
    public void Segment_DeveCriarMultiplosSegmentosQuandoExistiremMultiplosGaps()
    {
        var inicio =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var collection =
            CreateCollection(
                thresholdMinutes: 5,
                [
                    Reading(inicio, 10),
                    Reading(inicio.AddMinutes(1), 11),

                    // Gap: 9 minutos.
                    Reading(inicio.AddMinutes(10), 20),
                    Reading(inicio.AddMinutes(11), 21),

                    // Gap: 19 minutos.
                    Reading(inicio.AddMinutes(30), 30),
                    Reading(inicio.AddMinutes(31), 31)
                ]);

        var service =
            new BehaviorHistorySegmentationService();

        var result =
            service.Segment(collection);

        Assert.Equal(
            3,
            result.QuantidadeSegmentos);

        Assert.Equal(
            6,
            result.TotalLeituras);

        Assert.Equal(
            2,
            result.Segments[0].QuantidadeLeituras);

        Assert.Equal(
            2,
            result.Segments[1].QuantidadeLeituras);

        Assert.Equal(
            2,
            result.Segments[2].QuantidadeLeituras);

        Assert.Equal(
            9,
            result.Segments[1].GapAnteriorMinutos);

        Assert.Equal(
            19,
            result.Segments[2].GapAnteriorMinutos);
    }

    [Fact]
    public void Segment_HistoricoVazio_DeveRetornarResultadoVazio()
    {
        var collection =
            CreateCollection(
                thresholdMinutes: 0,
                []);

        var service =
            new BehaviorHistorySegmentationService();

        var result =
            service.Segment(collection);

        Assert.Empty(
            result.Segments);

        Assert.Equal(
            0,
            result.TotalLeituras);

        Assert.Equal(
            0,
            result.QuantidadeSegmentos);

        Assert.False(
            result.FoiSegmentado);
    }

    [Fact]
    public void Segment_ThresholdZero_NaoDeveInventarQuebraTemporal()
    {
        var inicio =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var collection =
            CreateCollection(
                thresholdMinutes: 0,
                [
                    Reading(inicio, 10),
                    Reading(inicio.AddHours(12), 20)
                ]);

        var service =
            new BehaviorHistorySegmentationService();

        var result =
            service.Segment(collection);

        Assert.Single(
            result.Segments);

        Assert.False(
            result.FoiSegmentado);

        Assert.Equal(
            2,
            result.Segments[0].QuantidadeLeituras);
    }

    private static BehaviorHistoryCollectionResult CreateCollection(
        double thresholdMinutes,
        IReadOnlyList<LeituraBruta> history)
    {
        return new BehaviorHistoryCollectionResult
        {
            ClienteId =
                "PK7",

            TagName =
                "PK7.TAG2",

            PeriodoInicio =
                history.Count > 0
                    ? history[0].DataHora
                    : DateTime.UtcNow,

            PeriodoFim =
                history.Count > 0
                    ? history[^1].DataHora
                    : DateTime.UtcNow,

            Historico =
                history,

            Qualidade =
                new DetectionInputQuality
                {
                    OriginalPoints =
                        history.Count,

                    ValidPoints =
                        history.Count,

                    CriticalGapThresholdMinutes =
                        thresholdMinutes,

                    HasCriticalGap =
                        thresholdMinutes > 0
                }
        };
    }

    private static LeituraBruta Reading(
        DateTime timestamp,
        float value)
    {
        return new LeituraBruta
        {
            DataHora =
                timestamp,

            Valor =
                value
        };
    }
}