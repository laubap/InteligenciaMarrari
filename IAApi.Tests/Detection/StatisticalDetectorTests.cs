
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
