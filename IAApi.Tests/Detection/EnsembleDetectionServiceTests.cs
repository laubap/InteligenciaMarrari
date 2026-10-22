
using IAApi.Services.AnomalyDetection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests.Detection;

public class EnsembleDetectionServiceTests
{
    [Fact]
    public void Detectar_PesosDiferentes_DeveCalcularMediaPonderada()
    {
        var detectorA = CriarDetector("A", 100, 3);
        var detectorB = CriarDetector("B", 0, 1);

        var resultado = CriarEnsemble(detectorA, detectorB)
            .Detectar(CriarInput());

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(75, resultado.ScoreNormalizado, 10);
        Assert.Equal(4, resultado.Metricas["somaPesosAplicaveis"]);
        Assert.Equal(2, resultado.Metricas["quantidadeDetectoresAplicaveis"]);
    }

    [Fact]
    public void Detectar_ScoreExatamenteNoLimite_DeveDetectarAnomalia()
    {
        var detector = CriarDetector("A", 60, 1);

        var resultado = CriarEnsemble(detector)
            .Detectar(CriarInput());

        Assert.True(resultado.EhAnomalia);
        Assert.Equal(60, resultado.ScoreNormalizado, 10);
    }

    [Fact]
    public void Detectar_ScoreAbaixoDoLimite_NaoDeveDetectar()
    {
        var detector = CriarDetector("A", 59.9, 1);

        var resultado = CriarEnsemble(detector)
            .Detectar(CriarInput());

        Assert.True(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(59.9, resultado.ScoreNormalizado, 10);
    }

    [Fact]
    public void Detectar_DetectorNaoAplicavel_DeveExcluirSeuPeso()
    {
        var detectorA = CriarDetector("A", 100, 1);

        var detectorB = new FakeDetector(
            "B",
            9,
            _ => new DetectorResult
            {
                Detector = "B",
                Aplicavel = false,
                EhAnomalia = false,
                ScoreNormalizado = 0,
                Peso = 9
            });

        var resultado = CriarEnsemble(detectorA, detectorB)
            .Detectar(CriarInput());

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(100, resultado.ScoreNormalizado, 10);
        Assert.Equal(1, resultado.Metricas["somaPesosAplicaveis"]);
        Assert.Equal(1, resultado.Metricas["quantidadeDetectoresAplicaveis"]);
        Assert.Equal(2, resultado.DetalhesDetectores.Count);
    }

    [Fact]
    public void Detectar_DetectorComExcecao_DeveContinuarAgregacao()
    {
        var detectorComErro = new FakeDetector(
            "DetectorComErro",
            1,
            _ => throw new InvalidOperationException("Erro simulado"));

        var detectorNormal = CriarDetector("Normal", 80, 2);

        var resultado = CriarEnsemble(
            detectorComErro,
            detectorNormal).Detectar(CriarInput());

        Assert.True(resultado.Aplicavel);
        Assert.True(resultado.EhAnomalia);
        Assert.Equal(80, resultado.ScoreNormalizado, 10);
        Assert.Equal(1, resultado.Metricas["quantidadeDetectoresAplicaveis"]);

        Assert.False(resultado.DetalhesDetectores[0].Aplicavel);
        Assert.Contains(
            "Erro simulado",
            resultado.DetalhesDetectores[0].Explicacao);
    }

    [Fact]
    public void Detectar_TodosNaoAplicaveis_DeveRetornarNaoAplicavel()
    {
        var detector = new FakeDetector(
            "NaoAplicavel",
            1,
            _ => new DetectorResult
            {
                Detector = "NaoAplicavel",
                Aplicavel = false,
                Peso = 1
            });

        var resultado = CriarEnsemble(detector)
            .Detectar(CriarInput());

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, resultado.ScoreNormalizado);
        Assert.Equal(0, resultado.Metricas["quantidadeDetectoresAplicaveis"]);
    }

    [Fact]
    public void Detectar_EntradaInvalida_NaoDeveExecutarDetectores()
    {
        var detector = CriarDetector("A", 100, 1);

        var input = CriarInput();
        input.ClienteId = "";

        var resultado = CriarEnsemble(detector)
            .Detectar(input);

        Assert.False(resultado.Aplicavel);
        Assert.False(resultado.EhAnomalia);
        Assert.Equal(0, detector.Chamadas);
        Assert.Equal("Validação de entrada", resultado.Detector);
        Assert.Contains(
            "identificador do cliente",
            resultado.Explicacao);
    }

    [Fact]
    public void Detectar_SemDetectores_DeveLancarExcecao()
    {
        var ensemble = CriarEnsemble();

        Assert.Throws<InvalidOperationException>(
            () => ensemble.Detectar(CriarInput()));
    }

    [Fact]
    public void Detectar_DevePreservarDetalhesIndividuais()
    {
        var detectorA = CriarDetector("A", 100, 2);
        var detectorB = CriarDetector("B", 20, 1);

        var resultado = CriarEnsemble(detectorA, detectorB)
            .Detectar(CriarInput());

        Assert.Equal(2, resultado.DetalhesDetectores.Count);

        Assert.Equal("A", resultado.DetalhesDetectores[0].Nome);
        Assert.Equal(100, resultado.DetalhesDetectores[0].ScoreNormalizado);

        Assert.Equal("B", resultado.DetalhesDetectores[1].Nome);
        Assert.Equal(20, resultado.DetalhesDetectores[1].ScoreNormalizado);

        Assert.Equal(2, resultado.Metricas["quantidadeDetectoresRegistrados"]);
    }

    [Fact]
    public void Detectar_ZScore_DevePriorizarLimitesComoReferencia()
    {
        var outroDetector = new FakeDetector(
            "Outro",
            1,
            _ => new DetectorResult
            {
                Detector = "Outro",
                Aplicavel = true,
                Peso = 1,
                LimiteInferior = 10,
                LimiteSuperior = 20,
                ScoreNormalizado = 70
            });

        var zScore = new FakeDetector(
            "Z-Score",
            1,
            _ => new DetectorResult
            {
                Detector = "Z-Score",
                Aplicavel = true,
                Peso = 1,
                LimiteInferior = 80,
                LimiteSuperior = 120,
                ScoreNormalizado = 70
            });

        var resultado = CriarEnsemble(outroDetector, zScore)
            .Detectar(CriarInput());

        Assert.True(resultado.Aplicavel);
        Assert.Equal(80, resultado.LimiteInferior);
        Assert.Equal(120, resultado.LimiteSuperior);
    }

    private static FakeDetector CriarDetector(
        string nome,
        double score,
        double peso)
    {
        return new FakeDetector(
            nome,
            peso,
            _ => new DetectorResult
            {
                Detector = nome,
                Aplicavel = true,
                EhAnomalia = score >= 60,
                Score = score / 20,
                ScoreNormalizado = score,
                Peso = peso,
                Explicacao = $"Resultado simulado de {nome}"
            });
    }

    private static EnsembleDetectionService CriarEnsemble(
        params IAnomalyDetector[] detectores)
    {
        return new EnsembleDetectionService(
            detectores,
            Options.Create(new AnomalyDetectionOptions()),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());
    }

    private static DetectorInput CriarInput()
    {
        var inicio = new DateTime(
            2026, 10, 1, 12, 0, 0,
            DateTimeKind.Utc);

        return new DetectorInput
        {
            ClienteId = "CLIENTE_TESTE",
            TagName = "TEMPERATURA_TESTE",
            TipoTag = "",
            ValorAtual = 101,
            DataHoraAtual = inicio.AddMinutes(5),
            MediaHistorica = 100,
            DesvioPadraoHistorico = 2,

            Historico = Enumerable.Range(0, 5)
                .Select(i => new HistoricoDetectorPoint
                {
                    DataHora = inicio.AddMinutes(i),
                    Valor = 99.5 + i * 0.4
                })
                .ToArray()
        };
    }

    private sealed class FakeDetector : IAnomalyDetector
    {
        private readonly Func<DetectorInput, DetectorResult> _executar;

        public FakeDetector(
            string nome,
            double peso,
            Func<DetectorInput, DetectorResult> executar)
        {
            Nome = nome;
            Peso = peso;
            _executar = executar;
        }

        public string Nome { get; }
        public double Peso { get; }
        public int Chamadas { get; private set; }

        public DetectorResult Detectar(DetectorInput input)
        {
            Chamadas++;
            return _executar(input);
        }
    }
}
