
using IAApi.Services.AnomalyDetection;
using Xunit;

namespace IAApi.Tests.Detection;

public class StatisticalDetectorTests
{
    [Fact]
    public void Detectar_ValorIgualMedia_NaoDeveDetectarAnomalia()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 100);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(0, resultado.ScoreNormalizado);
        Assert.Equal("Z-Score", resultado.Detector);
    }

    [Fact]
    public void Detectar_DesvioElevado_DeveDetectarAnomalia()
    {
        var options = new AnomalyDetectionOptions();

        var detector = new StatisticalDetector(options);

        var input = CriarInput(valorAtual: 150);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.True(resultado.Score >= options.ZScoreThreshold);
        Assert.True(resultado.ScoreNormalizado > 0);
        Assert.Equal(options.ZScoreWeight, resultado.Peso);
    }

    [Fact]
    public void Detectar_ValorDentroDeadband_DeveZerarScore()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 104);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(0, resultado.ScoreNormalizado);
        Assert.Equal(1, resultado.Metricas["dentroDeadband"]);
        Assert.True(
            resultado.Metricas["zScoreAntesDeadband"] > 0);
    }

    [Fact]
    public void Detectar_InputNulo_DeveLancarExcecao()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        Assert.Throws<ArgumentNullException>(
            () => detector.Detectar(null!));
    }

    
    [Fact]
    public void Detectar_NoThresholdExato_DeveDetectarAnomalia()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 130);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(3.0, resultado.Score, precision: 10);
        Assert.Equal(100, resultado.ScoreNormalizado);
    }

    [Fact]
    public void Detectar_AbaixoDoThreshold_NaoDeveDetectarAnomalia()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 129);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(2.9, resultado.Score, precision: 10);
    }

    [Fact]
    public void Detectar_DesvioPadraoZero_DeveUsarEscalaProtegida()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 110);

        input.DesvioPadraoHistorico = 0;

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);

        Assert.Equal(
            5.0 / 3.0,
            resultado.Metricas["escalaEfetiva"],
            precision: 10);

        Assert.Equal(6.0, resultado.Score, precision: 10);
    }

    [Fact]
    public void Detectar_NoLimiteDoDeadband_DeveSuprimirScore()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 105);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(1, resultado.Metricas["dentroDeadband"]);
    }

    [Fact]
    public void Detectar_SemDeadbandExplicito_DeveUsarP95()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 110);

        input.VariacaoMinimaRelevante = 0;

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.Equal(20, resultado.Metricas["deadband"]);
        Assert.Equal(20, resultado.Metricas["deadbandAutomaticoP95"]);
        Assert.Equal(1, resultado.Metricas["dentroDeadband"]);
        Assert.Equal(0, resultado.Score);
    }

    [Fact]
    public void Detectar_ConfiancaPercentual_DeveNormalizarParaZeroAUm()
    {
        var detector = new StatisticalDetector(
            new AnomalyDetectionOptions());

        var input = CriarInput(valorAtual: 100);

        input.ConfiancaVariacaoMinima = 94;

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);

        Assert.Equal(
            0.94,
            resultado.Metricas["confiancaDeadband"],
            precision: 10);
    }


    private static DetectorInput CriarInput(double valorAtual)
    {
        var dataBase = new DateTime(
            2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "Analogica",

            ValorAtual = valorAtual,
            DataHoraAtual = dataBase.AddMinutes(30),

            MediaHistorica = 100,
            DesvioPadraoHistorico = 10,

            VariacaoMinimaRelevante = 5,
            ConfiancaVariacaoMinima = 1,
            VariacaoMinimaDefinidaPeloCliente = true,

            Historico = Enumerable.Range(0, 30)
                .Select(i => new HistoricoDetectorPoint
                {
                    DataHora = dataBase.AddMinutes(i),
                    Valor = 100 + (i % 2 == 0 ? -10 : 10)
                })
                .ToArray()
        };
    }
}
