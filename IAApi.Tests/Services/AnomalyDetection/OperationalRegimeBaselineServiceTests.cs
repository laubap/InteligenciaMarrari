using IAApi.Services.AnomalyDetection;
using Xunit;

public class OperationalRegimeBaselineServiceTests
{
    [Fact]
    public void Resolve_BuildsOffBaselineFromBimodalHistory()
    {
        var service =
            CreateService();

        var configuration =
            CreateConfiguration();

        var targetHistory =
            CreateBimodalTargetHistory();

        var regimeHistory =
            CreateBimodalRegimeHistory();

        /*
         * Target atual: 00:12
         * Lag = 1
         *
         * Estado consultado:
         * 00:11 = OFF
         */
        var currentTimestamp =
            Utc(0, 12);

        var result =
            service.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(
            result.Applied);

        Assert.NotNull(
            result.Statistics);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            0,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Equal(
            24,
            result.OriginalTargetSamples);

        Assert.Equal(
            24,
            result.AlignedSamples);

        Assert.Equal(
            12,
            result.SelectedSamples);

        Assert.Equal(
            12,
            result.History.Count);

        /*
         * OFF:
         *
         * 0.02, 0.03, 0.04
         * repetidos quatro vezes.
         *
         * Média esperada = 0.03.
         */
        Assert.Equal(
            0.03,
            result.Statistics!.Media,
            5);

        Assert.True(
            result.Statistics.DesvioPadrao <
            0.02);

        Assert.All(
            result.History,
            point =>
                Assert.True(
                    point.Valor < 1));
    }

    [Fact]
    public void Resolve_BuildsOnBaselineFromSameBimodalHistory()
    {
        var service =
            CreateService();

        var configuration =
            CreateConfiguration();

        var targetHistory =
            CreateBimodalTargetHistory();

        var regimeHistory =
            CreateBimodalRegimeHistory();

        /*
         * Target atual: 00:24
         * Lag = 1
         *
         * Estado consultado:
         * 00:23 = ON
         */
        var currentTimestamp =
            Utc(0, 24);

        var result =
            service.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(
            result.Applied);

        Assert.NotNull(
            result.Statistics);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            1,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Equal(
            12,
            result.SelectedSamples);

        Assert.Equal(
            12,
            result.History.Count);

        /*
         * ON:
         *
         * 21.5, 21.6, 21.7
         * repetidos quatro vezes.
         *
         * Média esperada = 21.6.
         */
        Assert.Equal(
            21.6,
            result.Statistics!.Media,
            5);

        Assert.True(
            result.Statistics.DesvioPadrao <
            0.2);

        Assert.All(
            result.History,
            point =>
                Assert.True(
                    point.Valor > 20));
    }

    [Fact]
    public void Resolve_FallsBackWhenConfigurationIsDisabled()
    {
        var service =
            CreateService();

        var configuration =
            CreateConfiguration();

        configuration.Ativo =
            false;

        var result =
            service.Resolve(
                configuration,
                CreateBimodalTargetHistory(),
                CreateBimodalRegimeHistory(),
                Utc(0, 12));

        Assert.False(
            result.Applied);

        Assert.Null(
            result.Statistics);

        Assert.Empty(
            result.History);

        Assert.Contains(
            "desativada",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_FallsBackWhenIqrCleanupLeavesInsufficientSamples()
    {
        var service =
            CreateService();

        var configuration =
            CreateConfiguration();

        configuration.MinimoAmostrasPorRegime =
            10;

        /*
         * Antes da limpeza:
         *
         * 12 amostras.
         *
         * Depois da limpeza IQR:
         *
         * os três valores 100 são removidos,
         * restando somente 9 valores.
         *
         * O resolver aceita as 12 amostras,
         * mas o calculador estatístico rejeita
         * o histórico após a limpeza.
         *
         * Resultado esperado:
         *
         * fallback para o baseline global.
         */
        var targetHistory =
            CreateHistory(
                new float[]
                {
                    1,
                    1,
                    1,
                    1,
                    1,
                    1,
                    1,
                    1,
                    1,
                    100,
                    100,
                    100
                },
                firstTargetMinute: 1);

        var regimeHistory =
            CreateConstantRegimeHistory(
                state: 0,
                count: 12);

        var result =
            service.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                Utc(0, 12));

        Assert.False(
            result.Applied);

        Assert.Null(
            result.Statistics);

        Assert.Empty(
            result.History);

        /*
         * O resolver encontrou as 12 amostras.
         * O fallback aconteceu depois,
         * durante o cálculo estatístico.
         */
        Assert.Equal(
            12,
            result.SelectedSamples);

        Assert.Contains(
            "baseline do regime operacional",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    // ===== RC-8: sobrecarga com componentes =====

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void ResolveComponentes_SemAdicionaisESemMapa_IgualAAssinaturaAtual(
        int currentMinute)
    {
        var service = CreateService();
        var configuration = CreateConfiguration();

        var atual =
            service.Resolve(
                configuration,
                CreateBimodalTargetHistory(),
                CreateBimodalRegimeHistory(),
                Utc(0, currentMinute));

        var componentes =
            service.Resolve(
                configuration,
                Array.Empty<TagRegimeComponenteIa>(),
                CreateBimodalTargetHistory(),
                new IReadOnlyList<LeituraBruta>[] { CreateBimodalRegimeHistory() },
                Utc(0, currentMinute));

        Assert.True(atual.Applied, atual.Reason);
        Assert.Equal(atual.Applied, componentes.Applied);
        Assert.Equal(atual.Reason, componentes.Reason);
        Assert.Equal(atual.CurrentRegimeValue, componentes.CurrentRegimeValue);
        Assert.Equal(atual.OriginalTargetSamples, componentes.OriginalTargetSamples);
        Assert.Equal(atual.AlignedSamples, componentes.AlignedSamples);
        Assert.Equal(atual.SelectedSamples, componentes.SelectedSamples);
        Assert.Equal(atual.Statistics!.Media, componentes.Statistics!.Media);
        Assert.Equal(atual.Statistics.DesvioPadrao, componentes.Statistics.DesvioPadrao);
        Assert.Equal(atual.Statistics.DeadbandRecomendado, componentes.Statistics.DeadbandRecomendado);
        Assert.Equal(
            atual.History.Select(p => (p.DataHora, p.Valor)),
            componentes.History.Select(p => (p.DataHora, p.Valor)));

        // A chave só existe no caminho com componentes.
        Assert.Null(atual.CurrentRegimeKey);
        Assert.Equal(
            $"TEST.PUMP_RUNNING={(currentMinute == 12 ? 0 : 1)}",
            componentes.CurrentRegimeKey);
    }

    [Fact]
    public void ResolveComponentes_DoisComponentes_SelecionaPelaTuplaECalculaEstatisticas()
    {
        var service = CreateService();

        // Válvula: 0 nos minutos 00–01, 1 de 00:02 em diante.
        var valve =
            CreateHistory(
                Enumerable.Range(0, 24)
                    .Select(minute => minute < 2 ? 0f : 1f)
                    .ToArray(),
                firstTargetMinute: 0);

        var result =
            service.Resolve(
                CreateConfiguration(),
                new[] { Component(2, "TEST.VALVE") },
                CreateBimodalTargetHistory(),
                new IReadOnlyList<LeituraBruta>[] { CreateBimodalRegimeHistory(), valve },
                Utc(0, 12));

        // Estado em 00:11: bomba 0, válvula 1. Targets cujo regime (t − 1)
        // é (0, 1): 00:03..00:12 (o caminho atual pegaria 00:01..00:12).
        Assert.True(result.Applied, result.Reason);
        Assert.Equal("TEST.PUMP_RUNNING=0|TEST.VALVE=1", result.CurrentRegimeKey);
        Assert.Equal(0, result.CurrentRegimeValue!.Value, 3);
        Assert.Equal(24, result.OriginalTargetSamples);
        Assert.Equal(10, result.SelectedSamples);
        Assert.Equal(
            Enumerable.Range(3, 10).Select(minute => Utc(0, minute)),
            result.History.Select(p => p.DataHora));
        Assert.NotNull(result.Statistics);
        Assert.InRange(result.Statistics!.Media, 0.02, 0.04);
    }

    [Fact]
    public void ResolveComponentes_AmostrasInsuficientes_NaoAplicaEMantemAChave()
    {
        var service = CreateService();

        // Válvula 1 até 00:05 e 0 depois: o regime (0, 0) só cobre 6 targets.
        var valve =
            CreateHistory(
                Enumerable.Range(0, 24)
                    .Select(minute => minute <= 5 ? 1f : 0f)
                    .ToArray(),
                firstTargetMinute: 0);

        var result =
            service.Resolve(
                CreateConfiguration(),
                new[] { Component(2, "TEST.VALVE") },
                CreateBimodalTargetHistory(),
                new IReadOnlyList<LeituraBruta>[] { CreateBimodalRegimeHistory(), valve },
                Utc(0, 12));

        Assert.False(result.Applied);
        Assert.Null(result.Statistics);
        Assert.Empty(result.History);
        Assert.Equal(6, result.SelectedSamples);
        Assert.Equal("TEST.PUMP_RUNNING=0|TEST.VALVE=0", result.CurrentRegimeKey);
        Assert.Contains("insuficiente", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveComponentes_NumeroDeSeriesDiferenteDosComponentes_Excecao()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() =>
            service.Resolve(
                CreateConfiguration(),
                new[] { Component(2, "TEST.VALVE") },
                CreateBimodalTargetHistory(),
                new IReadOnlyList<LeituraBruta>[] { CreateBimodalRegimeHistory() },
                Utc(0, 12)));
    }

    [Fact]
    public void ResolveAtual_ResultadoSemChave()
    {
        var result =
            CreateService().Resolve(
                CreateConfiguration(),
                CreateBimodalTargetHistory(),
                CreateConstantRegimeHistory(0, 5),
                Utc(0, 12));

        Assert.Null(result.CurrentRegimeKey);
    }

    private static TagRegimeComponenteIa Component(
        int ordem,
        string tagRegime,
        string? mapa = null) =>
        new()
        {
            Ordem = ordem,
            TagRegime = tagRegime,
            MapaEstados = mapa,
            ToleranciaEstado = 0.001
        };

    private static OperationalRegimeBaselineService CreateService()
    {
        var historyResolver =
            new OperationalRegimeHistoryResolver();

        var statisticsCalculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        return new OperationalRegimeBaselineService(
            historyResolver,
            statisticsCalculator);
    }

    private static TagRegimeOperacionalIa CreateConfiguration()
    {
        return new TagRegimeOperacionalIa
        {
            ClienteId =
                "TEST_CLIENT",

            TagName =
                "TEST.INLET_FLOW",

            TagRegime =
                "TEST.PUMP_RUNNING",

            Ativo =
                true,

            LagMinutos =
                1,

            ToleranciaAlinhamentoMinutos =
                2,

            ToleranciaEstado =
                0.001,

            MinimoAmostrasPorRegime =
                10
        };
    }

    private static List<LeituraBruta> CreateBimodalTargetHistory()
    {
        /*
         * Target:
         *
         * minutos 01..12 -> OFF
         * minutos 13..24 -> ON
         *
         * Com Lag=1:
         *
         * target 01 usa regime 00
         * ...
         * target 12 usa regime 11
         *
         * target 13 usa regime 12
         * ...
         * target 24 usa regime 23
         */
        var offValues =
            new float[]
            {
                0.02f,
                0.03f,
                0.04f,

                0.02f,
                0.03f,
                0.04f,

                0.02f,
                0.03f,
                0.04f,

                0.02f,
                0.03f,
                0.04f
            };

        var onValues =
            new float[]
            {
                21.5f,
                21.6f,
                21.7f,

                21.5f,
                21.6f,
                21.7f,

                21.5f,
                21.6f,
                21.7f,

                21.5f,
                21.6f,
                21.7f
            };

        return CreateHistory(
            offValues
                .Concat(onValues)
                .ToArray(),
            firstTargetMinute: 1);
    }

    private static List<LeituraBruta> CreateBimodalRegimeHistory()
    {
        var history =
            new List<LeituraBruta>();

        /*
         * Regime minutos 00..11 = OFF
         */
        for (var minute = 0;
             minute <= 11;
             minute++)
        {
            history.Add(
                new LeituraBruta
                {
                    DataHora =
                        Utc(
                            0,
                            minute),

                    Valor =
                        0
                });
        }

        /*
         * Regime minutos 12..23 = ON
         */
        for (var minute = 12;
             minute <= 23;
             minute++)
        {
            history.Add(
                new LeituraBruta
                {
                    DataHora =
                        Utc(
                            0,
                            minute),

                    Valor =
                        1
                });
        }

        return history;
    }

    private static List<LeituraBruta> CreateConstantRegimeHistory(
        float state,
        int count)
    {
        var history =
            new List<LeituraBruta>();

        for (var index = 0;
             index < count;
             index++)
        {
            history.Add(
                new LeituraBruta
                {
                    DataHora =
                        Utc(
                            0,
                            index),

                    Valor =
                        state
                });
        }

        return history;
    }

    private static List<LeituraBruta> CreateHistory(
        IReadOnlyList<float> values,
        int firstTargetMinute)
    {
        var history =
            new List<LeituraBruta>(
                values.Count);

        for (var index = 0;
             index < values.Count;
             index++)
        {
            history.Add(
                new LeituraBruta
                {
                    DataHora =
                        Utc(
                            0,
                            firstTargetMinute +
                            index),

                    Valor =
                        values[index]
                });
        }

        return history;
    }

    private static DateTime Utc(
        int hour,
        int minute)
    {
        return new DateTime(
            2026,
            9,
            11,
            hour,
            minute,
            0,
            DateTimeKind.Utc);
    }
}