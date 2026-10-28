
using IAApi.IntelligenceLab.Observability;
using IAApi.Services.AnomalyDetection;
using Xunit;

namespace IAApi.Tests.Detection;

public class EwmaDetectorTests
{
    [Fact]
    public void Detectar_HistoricoInsuficiente_DeveSerNaoAplicavel()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 9).ToArray(),
            150);

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(DetectorDiagnosticReasonCodes.InsufficientHistory,
            resultado.NonApplicabilityReasonCode);
    }

    [Fact]
    public void Detectar_CadenciaTemporalIndisponivel_RegistraMotivo()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 10).ToArray(),
            110);

        foreach (var ponto in input.Historico)
            ponto.DataHora = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.Equal(DetectorDiagnosticReasonCodes.TemporalCadenceUnavailable,
            resultado.NonApplicabilityReasonCode);
    }

    [Fact]
    public void Detectar_EscalaBaseInvalida_RegistraMotivo()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 10).ToArray(),
            110);
        input.MediaHistorica = double.NaN;

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.Equal(DetectorDiagnosticReasonCodes.EffectiveScaleUnavailable,
            resultado.NonApplicabilityReasonCode);
    }

    [Fact]
    public void Detectar_DesvioEwmaInvalido_RegistraMotivo()
    {
        var detector = new EwmaDetector(new AnomalyDetectionOptions
        {
            EwmaLambda = double.NaN
        });
        var input = CriarInput(
            Enumerable.Repeat(100.0, 10).ToArray(),
            110);

        var resultado = detector.Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.Equal(DetectorDiagnosticReasonCodes.EwmaDeviationUnavailable,
            resultado.NonApplicabilityReasonCode);
    }

    [Fact]
    public void Detectar_SensorEstavel_NaoDeveDetectarAnomalia()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            100);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score, precision: 10);
        Assert.Equal("EWMA", resultado.Detector);
        Assert.Null(resultado.NonApplicabilityReasonCode);
    }

    [Fact]
    public void Detectar_DesvioBrusco_DeveDetectarAnomalia()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.True(resultado.Score >= 3);
    }

    [Fact]
    public void Detectar_DeslocamentoGradual_DeveDetectarAnomalia()
    {
        var historico = Enumerable.Range(0, 30)
            .Select(i => i < 20
                ? 100.0
                : 100.0 + (i - 19) * 2.0)
            .ToArray();

        var input = CriarInput(historico, 125);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.True(resultado.Score >= 3);
    }

    [Fact]
    public void Detectar_DentroDeadband_DeveZerarScore()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            104);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Score);
        Assert.Equal(1, resultado.Metricas["dentroDeadband"]);
    }

    [Fact]
    public void Detectar_IntervaloMaior_DeveAumentarAlphaEfetivo()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            110,
            intervaloAtualMinutos: 5);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(
            resultado.Metricas["lambdaAtualEfetivo"] >
            resultado.Metricas["lambdaBase"]);
    }

    [Fact]
    public void Detectar_IntervaloMenor_DeveReduzirAlphaEfetivo()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            110,
            intervaloAtualMinutos: 0.5);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(
            resultado.Metricas["lambdaAtualEfetivo"] <
            resultado.Metricas["lambdaBase"]);
    }

    [Fact]
    public void Detectar_IntervaloAtualInvalido_UsaCadenciaMedianaSemMotivo()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            110,
            intervaloAtualMinutos: 0);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.Null(resultado.NonApplicabilityReasonCode);
    }

    [Fact]
    public void Detectar_LambdaAcimaDoMaximo_DeveSerLimitado()
    {
        var options = new AnomalyDetectionOptions
        {
            EwmaLambda = 2.0
        };

        var detector = new EwmaDetector(options);

        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            110);

        var resultado = detector.Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.Equal(1.0, resultado.Metricas["lambdaBase"]);
        Assert.Equal(1.0, resultado.Metricas["lambdaAtualEfetivo"]);
    }

    [Fact]
    public void Detectar_DesvioPadraoZero_DeveUsarEscalaProtegida()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            110);

        input.DesvioPadraoHistorico = 0;

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(double.IsFinite(resultado.DesvioPadrao));
        Assert.True(resultado.DesvioPadrao > 0);
    }

    [Fact]
    public void Detectar_InputNulo_DeveLancarExcecao()
    {
        Assert.Throws<ArgumentNullException>(
            () => CriarDetector().Detectar(null!));
    }

    private static EwmaDetector CriarDetector()
    {
        return new EwmaDetector(
            new AnomalyDetectionOptions());
    }

    private static DetectorInput CriarInput(
        double[] valores,
        double valorAtual,
        double intervaloAtualMinutos = 1)
    {
        var dataBase = new DateTime(
            2026, 10, 1, 12, 0, 0,
            DateTimeKind.Utc);

        var dataUltimaLeitura =
            dataBase.AddMinutes(valores.Length - 1);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "Analogica",
            ValorAtual = valorAtual,

            DataHoraAtual = dataUltimaLeitura.AddMinutes(
                intervaloAtualMinutos),

            MediaHistorica = 100,
            DesvioPadraoHistorico = 10,

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
