
using IAApi.Services.AnomalyDetection;
using Xunit;

namespace IAApi.Tests.Detection;

public class LevelShiftDetectorTests
{
    private static readonly DateTime BaseTime =
        new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Detectar_SemCadenciaValida_DeveSerNaoAplicavel()
    {
        var input = CriarInput(new double[] { 100 }, 150);

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
    }

    [Fact]
    public void Detectar_JanelaInsuficiente_DeveSerNaoAplicavel()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 5).ToArray(),
            150);

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
    }

    [Fact]
    public void Detectar_ProcessoEstavel_NaoDeveDetectarAnomalia()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            100);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(0, resultado.ScoreNormalizado);
    }

    [Fact]
    public void Detectar_MudancaPositiva_DeveDetectarAnomalia()
    {
        var historico = Enumerable.Repeat(100.0, 20)
            .Concat(Enumerable.Repeat(150.0, 10))
            .ToArray();

        var input = CriarInput(historico, 150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(50, resultado.Metricas["diferencaPatamar"]);
        Assert.Equal(1, resultado.Metricas["consistencia"]);
    }

    [Fact]
    public void Detectar_MudancaNegativa_DeveDetectarAnomalia()
    {
        var historico = Enumerable.Repeat(150.0, 20)
            .Concat(Enumerable.Repeat(100.0, 10))
            .ToArray();

        var input = CriarInput(historico, 100);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(-50, resultado.Metricas["diferencaPatamar"]);
    }

    [Fact]
    public void Detectar_DentroDeadband_DeveZerarScore()
    {
        var historico = Enumerable.Repeat(100.0, 20)
            .Concat(Enumerable.Repeat(104.0, 10))
            .ToArray();

        var input = CriarInput(historico, 104);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(0, resultado.ScoreNormalizado);
    }

    [Fact]
    public void Detectar_ConsistenciaInsuficiente_NaoDeveConfirmar()
    {
        var historico = Enumerable.Repeat(100.0, 20)
            .Concat(new double[]
            {
                150, 150, 150, 150, 150,
                150, 100, 100, 100, 100
            })
            .ToArray();

        var input = CriarInput(historico, 150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.True(resultado.Metricas["consistencia"] < 0.80);
    }


    [Fact]
    public void Detectar_GapCritico_NaoDeveConfirmar()
    {
        var historico = Enumerable.Range(0, 20)
            .Select(i => new HistoricoDetectorPoint
            {
                DataHora = BaseTime.AddMinutes(i),
                Valor = 100
            })
            .Concat(
                new[] { 20, 21, 27, 28, 29 }
                    .Select(minuto => new HistoricoDetectorPoint
                    {
                        DataHora = BaseTime.AddMinutes(minuto),
                        Valor = 150
                    }))
            .ToArray();

        var input = new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "Analogica",
            ValorAtual = 150,
            DataHoraAtual = BaseTime.AddMinutes(30),

            VariacaoMinimaRelevante = 5,
            ConfiancaVariacaoMinima = 1,
            VariacaoMinimaDefinidaPeloCliente = true,

            Historico = historico
        };

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);

        Assert.Equal(
            1,
            resultado.Metricas["possuiGapCriticoNasJanelas"]);

        Assert.True(resultado.Metricas["scoreMudanca"] >= 3);
    }


    [Fact]
    public void Detectar_ScoreNormalizado_DeveAplicarPesos()
    {
        var historico = Enumerable.Repeat(100.0, 20)
            .Concat(Enumerable.Repeat(150.0, 10))
            .ToArray();

        var input = CriarInput(historico, 150);

        var resultado = CriarDetector().Detectar(input);

        var componenteDeslocamento = Math.Clamp(
            resultado.Score / 3.0,
            0,
            1);

        var esperado =
            ((componenteDeslocamento * 0.70) +
             (resultado.Metricas["consistencia"] * 0.30)) * 100;

        Assert.Equal(
            esperado,
            resultado.ScoreNormalizado,
            precision: 10);
    }

    [Fact]
    public void Detectar_InputNulo_DeveLancarExcecao()
    {
        Assert.Throws<ArgumentNullException>(
            () => CriarDetector().Detectar(null!));
    }

    private static LevelShiftDetector CriarDetector()
    {
        return new LevelShiftDetector(
            new AnomalyDetectionOptions());
    }

    private static DetectorInput CriarInput(
        double[] valores,
        double valorAtual)
    {
        var ultimaData = BaseTime.AddMinutes(valores.Length - 1);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "Analogica",
            ValorAtual = valorAtual,
            DataHoraAtual = ultimaData.AddMinutes(1),

            VariacaoMinimaRelevante = 5,
            ConfiancaVariacaoMinima = 1,
            VariacaoMinimaDefinidaPeloCliente = true,

            Historico = valores
                .Select((valor, indice) =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = BaseTime.AddMinutes(indice),
                        Valor = valor
                    })
                .ToArray()
        };
    }
}
