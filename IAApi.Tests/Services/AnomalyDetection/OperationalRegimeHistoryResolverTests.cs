using IAApi.Services.AnomalyDetection;
using Xunit;

public class OperationalRegimeHistoryResolverTests
{
    [Fact]
    public void Resolve_SelectsOnlyHistoryFromCurrentOffRegime()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        var targetHistory =
            CreateTargetHistory();

        var regimeHistory =
            CreateRegimeHistory();

        /*
         * Leitura atual: 00:07
         * Lag: 1 minuto
         *
         * Estado esperado:
         * 00:06.
         *
         * Como não existe exatamente 00:06,
         * a leitura causal mais recente é 00:05 = 0.
         *
         * Portanto o regime atual é OFF.
         */
        var currentTimestamp =
            Utc(0, 7);

        var result =
            resolver.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(result.Applied);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            0,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Equal(
            6,
            result.OriginalTargetSamples);

        Assert.Equal(
            6,
            result.AlignedSamples);

        Assert.Equal(
            4,
            result.SelectedSamples);

        var values =
            result.History
                .Select(x => x.Valor)
                .ToArray();

        Assert.Equal(
            new[]
            {
                0.05,
                0.04,
                0.03,
                0.02
            },
            values);
    }

    [Fact]
    public void Resolve_SelectsOnlyHistoryFromCurrentOnRegime()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        var targetHistory =
            CreateTargetHistory();

        var regimeHistory =
            CreateRegimeHistory();

        /*
         * Leitura atual: 00:04
         * Lag: 1 minuto
         *
         * Estado esperado:
         * 00:03 = 1.
         *
         * Portanto o regime atual é ON.
         */
        var currentTimestamp =
            Utc(0, 4);

        var result =
            resolver.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(result.Applied);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            1,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Equal(
            2,
            result.SelectedSamples);

        var values =
            result.History
                .Select(x => x.Valor)
                .ToArray();

        Assert.Equal(
            new[]
            {
                21.50,
                21.60
            },
            values);
    }

    [Fact]
    public void Resolve_NeverUsesFutureRegimeReading()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        var targetHistory =
            new[]
            {
                Point(
                    0,
                    1,
                    0.05)
            };

        /*
         * Para INLET em 00:01 e Lag=1,
         * precisamos explicar o valor usando o estado em 00:00.
         *
         * Temos:
         *
         * 23:59 = OFF
         * 00:01 = ON
         *
         * A leitura de 00:01 é futura em relação ao instante esperado
         * 00:00 e nunca pode ser usada.
         *
         * Logo o resolver deve selecionar o estado OFF de 23:59.
         */
        var regimeHistory =
            new[]
            {
                new HistoricoDetectorPoint
                {
                    DataHora =
                        new DateTime(
                            2026,
                            9,
                            10,
                            23,
                            59,
                            0,
                            DateTimeKind.Utc),

                    Valor =
                        0
                },

                Point(
                    0,
                    1,
                    1)
            };

        var currentTimestamp =
            Utc(0, 1);

        var result =
            resolver.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(result.Applied);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            0,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Single(
            result.History);

        Assert.Equal(
            0.05,
            result.History[0].Valor,
            3);
    }

    [Fact]
    public void Resolve_FallsBackWhenCurrentRegimeHasInsufficientHistory()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        /*
         * Nosso histórico contém quatro amostras OFF.
         * Exigimos cinco.
         *
         * Portanto o baseline condicionado NÃO deve ser aplicado.
         */
        var configuration =
            CreateConfiguration(
                minimumSamples: 5);

        var result =
            resolver.Resolve(
                configuration,
                CreateTargetHistory(),
                CreateRegimeHistory(),
                Utc(0, 7));

        Assert.False(
            result.Applied);

        Assert.Equal(
            4,
            result.SelectedSamples);

        Assert.Empty(
            result.History);

        Assert.Contains(
            "Histórico insuficiente",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_FallsBackWhenConfigurationIsDisabled()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        configuration.Ativo =
            false;

        var result =
            resolver.Resolve(
                configuration,
                CreateTargetHistory(),
                CreateRegimeHistory(),
                Utc(0, 7));

        Assert.False(
            result.Applied);

        Assert.Empty(
            result.History);

        Assert.Contains(
            "desativada",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    private static TagRegimeOperacionalIa CreateConfiguration(
        int minimumSamples)
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
                minimumSamples
        };
    }

    private static IReadOnlyList<HistoricoDetectorPoint>
        CreateTargetHistory()
    {
        /*
         * Associação com Lag = 1:
         *
         * Target 00:01 -> Regime 00:00 = OFF
         * Target 00:02 -> Regime 00:01 = OFF
         * Target 00:03 -> Regime 00:02 = ON
         * Target 00:04 -> Regime 00:03 = ON
         * Target 00:05 -> Regime 00:04 = OFF
         * Target 00:06 -> Regime 00:05 = OFF
         */
        return new[]
        {
            Point(0, 1, 0.05),
            Point(0, 2, 0.04),

            Point(0, 3, 21.50),
            Point(0, 4, 21.60),

            Point(0, 5, 0.03),
            Point(0, 6, 0.02)
        };
    }

    private static IReadOnlyList<HistoricoDetectorPoint>
        CreateRegimeHistory()
    {
        return new[]
        {
            Point(0, 0, 0),
            Point(0, 1, 0),

            Point(0, 2, 1),
            Point(0, 3, 1),

            Point(0, 4, 0),
            Point(0, 5, 0)
        };
    }

    private static HistoricoDetectorPoint Point(
        int hour,
        int minute,
        double value)
    {
        return new HistoricoDetectorPoint
        {
            DataHora =
                Utc(
                    hour,
                    minute),

            Valor =
                value
        };
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