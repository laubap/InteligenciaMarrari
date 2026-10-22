
using IAApi.Services.AnomalyDetection;
using Xunit;

namespace IAApi.Tests.Detection;

public class RateOfChangeDetectorTests
{
    [Fact]
    public void Detectar_HistoricoInsuficiente_DeveSerNaoAplicavel()
    {
        var input = CriarInput(
            new double[] { 100, 101 },
            102);

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
    }


    [Fact]
    public void Detectar_TaxaIgualMediana_NaoDeveDetectarAnomalia()
    {
        var input = CriarInput(
            new double[] { 100, 101, 103, 104, 106 },
            107.5);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);

        Assert.Equal(
            1.5,
            resultado.Metricas["medianaTaxas"],
            precision: 10);

        Assert.Equal(
            1.5,
            resultado.Metricas["taxaAtualPorMinuto"],
            precision: 10);

        Assert.Equal(0, resultado.Score, precision: 10);
    }


    [Fact]
    public void Detectar_AceleracaoBrusca_DeveDetectarAnomalia()
    {
        var input = CriarInput(
            CriarHistoricoVariavel(),
            150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.True(resultado.Score >= 3.5);
        Assert.Equal(100, resultado.ScoreNormalizado);
    }

    [Fact]
    public void Detectar_QuedaBrusca_DeveDetectarAnomalia()
    {
        var input = CriarInput(
            CriarHistoricoVariavel(),
            50);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.True(resultado.Score >= 3.5);
        Assert.True(resultado.Metricas["taxaAtualPorMinuto"] < 0);
    }

    [Fact]
    public void Detectar_VariacaoPequena_NaoDeveDetectarAnomalia()
    {
        var input = CriarInput(
            CriarHistoricoVariavel(),
            106);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.True(resultado.Score < 3.5);
    }

    [Fact]
    public void Detectar_MadPositivo_DeveUsarScoreRobusto()
    {
        var input = CriarInput(
            new double[] { 100, 101, 103, 104, 106 },
            110);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.Metricas["madTaxas"] > 0);
        Assert.Equal(0, resultado.Metricas["fallbackDesvioPadrao"]);

        var esperado = 0.6745 *
            Math.Abs(
                resultado.Metricas["taxaAtualPorMinuto"] -
                resultado.Metricas["medianaTaxas"]) /
            resultado.Metricas["madTaxas"];

        Assert.Equal(esperado, resultado.Score, precision: 10);
    }

    [Fact]
    public void Detectar_MadZero_DeveUsarDesvioPadrao()
    {
        var input = CriarInput(
            new double[] { 100, 101, 102, 103, 113 },
            120);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.Equal(0, resultado.Metricas["madTaxas"]);
        Assert.True(resultado.Metricas["desvioPadraoTaxas"] > 0);
        Assert.Equal(1, resultado.Metricas["fallbackDesvioPadrao"]);

        var esperado =
            Math.Abs(
                resultado.Metricas["taxaAtualPorMinuto"] -
                resultado.Metricas["medianaTaxas"]) /
            resultado.Metricas["desvioPadraoTaxas"];

        Assert.Equal(esperado, resultado.Score, precision: 10);
    }

    [Fact]
    public void Detectar_TaxasHistoricasConstantes_DeveSerNaoAplicavel()
    {
        var input = CriarInput(
            new double[] { 100, 101, 102, 103, 104 },
            150);

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
    }

    [Fact]
    public void Detectar_TimestampAtualRepetido_DeveUsarCadenciaMediana()
    {
        var historico = CriarHistoricoVariavel();

        var input = CriarInput(
            historico,
            110,
            intervaloAtualMinutos: 0);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);

        Assert.Equal(
            110 - historico[^1],
            resultado.Metricas["taxaAtualPorMinuto"],
            precision: 10);
    }

    [Fact]
    public void Detectar_InputNulo_DeveLancarExcecao()
    {
        Assert.Throws<ArgumentNullException>(
            () => CriarDetector().Detectar(null!));
    }

    private static RateOfChangeDetector CriarDetector()
    {
        return new RateOfChangeDetector(
            new AnomalyDetectionOptions());
    }

    private static double[] CriarHistoricoVariavel()
    {
        return new double[]
        {
            100, 101, 103, 104, 106, 105
        };
    }

    private static DetectorInput CriarInput(
        double[] valores,
        double valorAtual,
        double intervaloAtualMinutos = 1)
    {
        var dataBase = new DateTime(
            2026, 10, 1, 12, 0, 0,
            DateTimeKind.Utc);

        var ultimaData = dataBase.AddMinutes(
            valores.Length - 1);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "Analogica",
            ValorAtual = valorAtual,
            DataHoraAtual = ultimaData.AddMinutes(
                intervaloAtualMinutos),

            Historico = valores
                .Select((valor, indice) =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = dataBase.AddMinutes(indice),
                        Valor = valor
                    })
                .ToArray()
        };
    }
}
