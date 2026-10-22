
using IAApi.Services.AnomalyDetection;
using Xunit;

namespace IAApi.Tests.Detection;

public class PersistenceDetectorTests
{
    private static readonly DateTime BaseTime =
        new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Detectar_HistoricoInsuficiente_DeveSerNaoAplicavel()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 9).ToArray(),
            150);

        var resultado = CriarDetector().Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
    }

    [Fact]
    public void Detectar_LeituraNormal_DeveSerNotObserved()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            100);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(
            TemporalConfirmationStatus.NotObserved,
            resultado.TemporalConfirmation!.Status);
        Assert.Equal(0, resultado.Metricas["pontosConsecutivos"]);
    }

    [Fact]
    public void Detectar_DesvioIsolado_DeveFicarPendente()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);

        Assert.Equal(
            TemporalConfirmationStatus.Pending,
            resultado.TemporalConfirmation!.Status);

        Assert.Equal(1, resultado.Metricas["pontosConsecutivos"]);
    }

    [Fact]
    public void Detectar_CincoLeiturasAnormais_DeveConfirmar()
    {
        var historico =
            Enumerable.Repeat(100.0, 25)
                .Concat(Enumerable.Repeat(150.0, 4))
                .ToArray();

        var input = CriarInput(historico, 150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);

        Assert.Equal(
            TemporalConfirmationStatus.Confirmed,
            resultado.TemporalConfirmation!.Status);

        Assert.Equal(5, resultado.Metricas["pontosConsecutivos"]);
        Assert.Equal(4, resultado.Metricas["duracaoMinutos"]);
    }

    [Fact]
    public void Detectar_QuatroLeiturasAnormais_NaoDeveConfirmar()
    {
        var historico =
            Enumerable.Repeat(100.0, 26)
                .Concat(Enumerable.Repeat(150.0, 3))
                .ToArray();

        var input = CriarInput(historico, 150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);

        Assert.Equal(
            TemporalConfirmationStatus.Pending,
            resultado.TemporalConfirmation!.Status);

        Assert.Equal(4, resultado.Metricas["pontosConsecutivos"]);
    }

    [Fact]
    public void Detectar_GapCritico_DeveInterromperConfirmacao()
    {
        var historico =
            Enumerable.Repeat(100.0, 25)
                .Concat(Enumerable.Repeat(150.0, 4))
                .ToArray();

        var input = CriarInput(
            historico,
            150,
            intervaloAtualMinutos: 10);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);

        Assert.Equal(
            TemporalConfirmationStatus.InterruptedByGap,
            resultado.TemporalConfirmation!.Status);

        Assert.Equal(1, resultado.Metricas["sequenciaQuebradaPorGap"]);
    }

    [Fact]
    public void Detectar_MadZero_DeveConfirmarPorDeadband()
    {
        var historico =
            Enumerable.Repeat(100.0, 25)
                .Concat(Enumerable.Repeat(150.0, 4))
                .ToArray();

        var input = CriarInput(historico, 150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(0, resultado.Metricas["madBaseline"]);

        Assert.Equal(
            UnderlyingEvidenceKind.DeadbandDeviation,
            resultado.UnderlyingEvidenceKind);
    }

    [Fact]
    public void Detectar_NoLimiteDeadband_NaoDeveDetectar()
    {
        var input = CriarInput(
            Enumerable.Repeat(100.0, 30).ToArray(),
            105);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);

        Assert.Equal(
            TemporalConfirmationStatus.NotObserved,
            resultado.TemporalConfirmation!.Status);
    }

    [Fact]
    public void Detectar_MadPositivo_DeveClassificarComoTemporal()
    {
        var historico = Enumerable.Range(0, 30)
            .Select(i => i % 2 == 0 ? 99.0 : 101.0)
            .ToArray();

        var input = CriarInput(historico, 150);

        var resultado = CriarDetector().Detectar(input);

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.Metricas["madBaseline"] > 0);

        Assert.Equal(
            DetectorEvidenceRole.TemporalConfirmation,
            resultado.EvidenceRole);

        Assert.Equal(
            UnderlyingEvidenceKind.Mad,
            resultado.UnderlyingEvidenceKind);
    }

    [Fact]
    public void Detectar_InputNulo_DeveLancarExcecao()
    {
        Assert.Throws<ArgumentNullException>(
            () => CriarDetector().Detectar(null!));
    }

    private static PersistenceDetector CriarDetector()
    {
        return new PersistenceDetector(
            new AnomalyDetectionOptions());
    }

    private static DetectorInput CriarInput(
        double[] valores,
        double valorAtual,
        double intervaloAtualMinutos = 1)
    {
        var ultimaData =
            BaseTime.AddMinutes(valores.Length - 1);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "Analogica",
            ValorAtual = valorAtual,
            DataHoraAtual = ultimaData.AddMinutes(
                intervaloAtualMinutos),

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
