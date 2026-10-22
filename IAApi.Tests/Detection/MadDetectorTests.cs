
using IAApi.Services.AnomalyDetection;
using Xunit;

namespace IAApi.Tests.Detection;

public class MadDetectorTests
{
    [Fact]
    public void Detectar_HistoricoInsuficiente_DeveSerNaoAplicavel()
    {
        var detector = CriarDetector();

        var input = CriarInput(
            Enumerable.Repeat(100.0, 9).ToArray(),
            valorAtual: 150);

        var resultado = detector.Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
    }

    [Fact]
    public void Detectar_ValorNaMediana_NaoDeveDetectarAnomalia()
    {
        var detector = CriarDetector();
        var input = CriarInput(CriarHistoricoPadrao(), 100);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal("Mediana e MAD", resultado.Detector);
    }

    [Fact]
    public void Detectar_ValorExtremo_DeveDetectarAnomalia()
    {
        var detector = CriarDetector();
        var input = CriarInput(CriarHistoricoPadrao(), 150);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.True(resultado.Score >= 3.5);
        Assert.Equal(100, resultado.ScoreNormalizado);
    }

    [Fact]
    public void Detectar_VariacaoNormal_NaoDeveDetectarAnomalia()
    {
        var detector = CriarDetector();
        var input = CriarInput(CriarHistoricoPadrao(), 101);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.True(resultado.Score < 3.5);
    }

    [Fact]
    public void Detectar_DentroDeadband_DeveZerarScore()
    {
        var detector = CriarDetector();
        var input = CriarInput(CriarHistoricoPadrao(), 104);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(0, resultado.ScoreNormalizado);
        Assert.Equal(1, resultado.Metricas["dentroDeadband"]);
    }

    [Fact]
    public void Detectar_NoLimiteDeadband_DeveZerarScore()
    {
        var detector = CriarDetector();
        var input = CriarInput(CriarHistoricoPadrao(), 105);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(1, resultado.Metricas["dentroDeadband"]);
    }

    [Fact]
    public void Detectar_HistoricoConstante_DeveUsarEscalaProtegida()
    {
        var detector = CriarDetector();

        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            valorAtual: 110);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Metricas["madOriginal"]);

        Assert.Equal(
            5.0 / 3.5,
            resultado.Metricas["escalaEfetiva"],
            precision: 10);

        Assert.Equal(7.0, resultado.Score, precision: 10);
    }

    [Fact]
    public void Detectar_OutlierHistorico_DevePreservarMediana()
    {
        var detector = CriarDetector();

        var historico = Enumerable.Repeat(100.0, 29)
            .Append(1000.0)
            .ToArray();

        var input = CriarInput(historico, valorAtual: 110);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.Equal(100, resultado.Metricas["mediana"]);
        Assert.Equal(0, resultado.Metricas["madOriginal"]);
    }

    [Fact]
    public void Detectar_SemDeadbandExplicito_DeveUtilizarP95()
    {
        var detector = CriarDetector();

        var historico = Enumerable.Range(0, 30)
            .Select(i => i % 2 == 0 ? 90.0 : 110.0)
            .ToArray();

        var input = CriarInput(historico, valorAtual: 110);
        input.VariacaoMinimaRelevante = 0;

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.Equal(20, resultado.Metricas["deadbandAutomaticoP95"]);
        Assert.Equal(20, resultado.Metricas["deadband"]);
    }

    [Fact]
    public void Detectar_InputNulo_DeveLancarExcecao()
    {
        var detector = CriarDetector();

        Assert.Throws<ArgumentNullException>(
            () => detector.Detectar(null!));
    }

    private static MadDetector CriarDetector()
    {
        return new MadDetector(new AnomalyDetectionOptions());
    }

    private static double[] CriarHistoricoPadrao()
    {
        return Enumerable.Range(0, 30)
            .Select(i => i % 2 == 0 ? 99.0 : 101.0)
            .ToArray();
    }

    private static DetectorInput CriarInput(
        double[] valores,
        double valorAtual)
    {
        var dataBase = new DateTime(
            2026, 10, 1, 12, 0, 0,
            DateTimeKind.Utc);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "Analogica",
            ValorAtual = valorAtual,
            DataHoraAtual = dataBase.AddMinutes(valores.Length),
            VariacaoMinimaRelevante = 5,
            ConfiancaVariacaoMinima = 1,
            VariacaoMinimaDefinidaPeloCliente = true,

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
