using Xunit;

public class TagProfileStatisticsCalculatorTests
{
    [Fact]
    public void Calculate_ComputesExpectedStatisticsForSimpleSeries()
    {
        var calculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var history =
            CreateHistory(
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10);

        var result =
            calculator.Calculate(
                history);

        Assert.Equal(
            5.5,
            result.Media,
            10);

        Assert.Equal(
            1,
            result.Minimo,
            10);

        Assert.Equal(
            10,
            result.Maximo,
            10);

        Assert.Equal(
            9,
            result.Amplitude,
            10);

        /*
         * Variância populacional de 1..10:
         *
         * 8.25
         *
         * Desvio:
         *
         * sqrt(8.25)
         */
        Assert.Equal(
            Math.Sqrt(8.25),
            result.DesvioPadrao,
            10);

        Assert.Equal(
            0,
            result.PercentualZeros,
            10);

        /*
         * Todas as variações consecutivas são iguais a 1.
         */
        Assert.Equal(
            1,
            result.VariacaoMedia,
            10);

        Assert.Equal(
            1,
            result.DeadbandConservador,
            10);

        Assert.Equal(
            1,
            result.DeadbandRecomendado,
            10);

        Assert.Equal(
            1,
            result.DeadbandAgressivo,
            10);

        /*
         * 9 variações:
         *
         * confiança base = 65
         *
         * Como todas as variações são iguais,
         * diversidade aproximada <= 2:
         *
         * -15 pontos.
         *
         * Resultado esperado = 50.
         */
        Assert.Equal(
            50,
            result.ConfiancaDeadband,
            10);

        Assert.Equal(
            0,
            result.QuantidadePicos);

        Assert.Equal(
            10,
            result.TotalRegistrosHistorico);

        Assert.Equal(
            10,
            result.TotalRegistrosUsados);

        Assert.Equal(
            0,
            result.TotalOutliersRemovidos);

        Assert.Equal(
            10,
            result.HistoricoLimpo.Count);
    }

    [Fact]
    public void Calculate_RemovesInvalidValueAndIqrOutlier()
    {
        var calculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        /*
         * Temos:
         *
         * - 10 leituras normais iguais a 1;
         * - 100 como outlier;
         * - NaN como valor inválido.
         *
         * Resultado esperado após limpeza:
         *
         * 10 leituras iguais a 1.
         */
        var history =
            CreateHistory(
                1,
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
                float.NaN);

        var result =
            calculator.Calculate(
                history);

        Assert.Equal(
            12,
            result.TotalRegistrosHistorico);

        Assert.Equal(
            10,
            result.TotalRegistrosUsados);

        Assert.Equal(
            2,
            result.TotalOutliersRemovidos);

        Assert.Equal(
            1,
            result.Media,
            10);

        Assert.Equal(
            1,
            result.Minimo,
            10);

        Assert.Equal(
            1,
            result.Maximo,
            10);

        Assert.Equal(
            0,
            result.DesvioPadrao,
            10);

        Assert.Equal(
            0,
            result.Amplitude,
            10);

        Assert.Equal(
            0,
            result.VariacaoMedia,
            10);

        Assert.Equal(
            0,
            result.DeadbandRecomendado,
            10);

        Assert.Equal(
            0,
            result.ConfiancaDeadband,
            10);

        Assert.Equal(
            10,
            result.HistoricoLimpo.Count);

        Assert.All(
            result.HistoricoLimpo,
            leitura =>
                Assert.Equal(
                    1f,
                    leitura.Valor));
    }

    [Fact]
    public void Calculate_ComputesZeroPercentageAndChronologicalVariation()
    {
        var calculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        /*
         * Série alternada:
         *
         * 0,1,0,1,0,1,0,1,0,1
         *
         * Metade das leituras é zero.
         *
         * Todas as variações consecutivas são 1.
         */
        var history =
            CreateHistory(
                0,
                1,
                0,
                1,
                0,
                1,
                0,
                1,
                0,
                1);

        var result =
            calculator.Calculate(
                history);

        Assert.Equal(
            0.5,
            result.Media,
            10);

        Assert.Equal(
            0.5,
            result.DesvioPadrao,
            10);

        Assert.Equal(
            50,
            result.PercentualZeros,
            10);

        Assert.Equal(
            1,
            result.VariacaoMedia,
            10);

        Assert.Equal(
            1,
            result.DeadbandRecomendado,
            10);

        Assert.Equal(
            10,
            result.TotalRegistrosUsados);
    }

    [Fact]
    public void Calculate_ThrowsWhenHistoryHasLessThanTenSamples()
    {
        var calculator =
            new TagProfileStatisticsCalculator(
                new HistoricoLimpezaService());

        var history =
            CreateHistory(
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9);

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    calculator.Calculate(
                        history));

        Assert.Contains(
            "Histórico insuficiente",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static List<LeituraBruta> CreateHistory(
        params float[] values)
    {
        var start =
            new DateTime(
                2026,
                9,
                11,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var history =
            new List<LeituraBruta>(
                values.Length);

        for (var index = 0;
             index < values.Length;
             index++)
        {
            history.Add(
                new LeituraBruta
                {
                    DataHora =
                        start.AddMinutes(
                            index),

                    Valor =
                        values[index]
                });
        }

        return history;
    }
}