
using IAApi.Services.AnomalyDetection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests.Detection;

public class EnsembleIndustrialScenarioTests
{
    private static readonly DateTime BaseTime =
        new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ParadaSimulada_DeveRegistrarEvolucaoDosDetectores()
    {
        var options = new AnomalyDetectionOptions();

        IAnomalyDetector[] detectores =
        {
            new StatisticalDetector(options),
            new MadDetector(options),
            new EwmaDetector(options),
            new RateOfChangeDetector(options),
            new PersistenceDetector(options),
            new LevelShiftDetector(options)
        };

        var ensemble = new EnsembleDetectionService(
            detectores,
            Options.Create(options),
            new TagBehaviorProfileResolver(),
            new DetectorApplicabilityPolicy());

        var valoresNormais = Enumerable.Range(0, 30)
            .Select(i => i % 2 == 0 ? 99.0 : 101.0)
            .ToArray();

        var entradaNormal = CriarEntrada(
            valoresNormais,
            valorAtual: 100);

        var entradaParada = CriarEntrada(
            valoresNormais,
            valorAtual: 0);

        var historicoParadaPersistente = valoresNormais
            .Concat(Enumerable.Repeat(0.0, 4))
            .ToArray();

        var entradaParadaPersistente = CriarEntrada(
            historicoParadaPersistente,
            valorAtual: 0);

        var normal = ensemble.Detectar(entradaNormal);
        var parada = ensemble.Detectar(entradaParada);
        var persistente = ensemble.Detectar(entradaParadaPersistente);

        Registrar("Operacao normal", normal);
        Registrar("Primeira leitura em zero", parada);
        Registrar("Quinta leitura em zero", persistente);

        Assert.True(normal.Aplicavel);
        Assert.False(normal.EhAnomalia);

        Assert.True(parada.Aplicavel);
        Assert.True(persistente.Aplicavel);

        Assert.Equal(6, normal.DetalhesDetectores.Count);
        Assert.Equal(6, parada.DetalhesDetectores.Count);
        Assert.Equal(6, persistente.DetalhesDetectores.Count);

        Assert.True(double.IsFinite(normal.ScoreNormalizado));
        Assert.True(double.IsFinite(parada.ScoreNormalizado));
        Assert.True(double.IsFinite(persistente.ScoreNormalizado));

        Assert.InRange(normal.ScoreNormalizado, 0, 100);
        Assert.InRange(parada.ScoreNormalizado, 0, 100);
        Assert.InRange(persistente.ScoreNormalizado, 0, 100);

        Assert.Equal(
    6,
    normal.Metricas["quantidadeDetectoresAplicaveis"]);

Assert.Equal(
    6,
    parada.Metricas["quantidadeDetectoresAplicaveis"]);

Assert.True(
    parada.Metricas["quantidadeAlertas"] > 0);

// Operação normal não deve produzir alerta final.
Assert.False(normal.EhAnomalia);

// A queda abrupta deve produzir alerta imediatamente.
Assert.True(parada.EhAnomalia);

// A condição sustentada deve continuar produzindo alerta.
Assert.True(persistente.EhAnomalia);

// Os scores devem permanecer acima do threshold.
Assert.True(
    parada.ScoreNormalizado >= options.EnsembleThreshold);

Assert.True(
    persistente.ScoreNormalizado >= options.EnsembleThreshold);

// A persistência deve confirmar a condição após cinco leituras.
var detectorPersistencia = persistente.DetalhesDetectores
    .Single(x => x.Nome == "Persistência");

Assert.True(detectorPersistencia.VotouAnomalia);

Assert.Equal(
    TemporalConfirmationStatus.Confirmed,
    detectorPersistencia.TemporalConfirmation!.Status);

// A taxa de variação deve reconhecer a primeira queda.
var taxaPrimeiraLeitura = parada.DetalhesDetectores
    .Single(x => x.Nome == "Taxa de variação");

Assert.True(taxaPrimeiraLeitura.VotouAnomalia);    
    }

    private static DetectorInput CriarEntrada(
        double[] historico,
        double valorAtual)
    {
        return new DetectorInput
        {
            ClienteId = "CLIENTE_SIMULADO",
            TagName = "BOMBA_VAZAO",
            TipoTag = "continuous",

            ValorAtual = valorAtual,
            DataHoraAtual = BaseTime.AddMinutes(historico.Length),

            MediaHistorica = 100,
            DesvioPadraoHistorico = 1,

            VariacaoMinimaRelevante = 5,
            ConfiancaVariacaoMinima = 1,
            VariacaoMinimaDefinidaPeloCliente = true,

            Historico = historico
                .Select((valor, i) => new HistoricoDetectorPoint
                {
                    DataHora = BaseTime.AddMinutes(i),
                    Valor = valor
                })
                .ToArray()
        };
    }

    private static void Registrar(
        string etapa,
        DetectorResult resultado)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {etapa} ===");
        Console.WriteLine(
            $"Ensemble: aplicavel={resultado.Aplicavel}, " +
            $"anomalia={resultado.EhAnomalia}, " +
            $"score={resultado.ScoreNormalizado:F4}");

foreach (var detector in resultado.DetalhesDetectores)
{
    Console.WriteLine(
        $"{detector.Nome}: " +
        $"aplicavel={detector.Aplicavel}, " +
        $"anomalia={detector.VotouAnomalia}, " +
        $"score={detector.ScoreNormalizado:F4}");

    Console.WriteLine(
        $"  Motivo: {detector.Explicacao}");
}

foreach (var metrica in resultado.Metricas)
{
    Console.WriteLine(
        $"METRICA {metrica.Key}: {metrica.Value}");
}
        Console.WriteLine(
            $"Detectores aplicaveis: " +
            $"{resultado.Metricas["quantidadeDetectoresAplicaveis"]}");

        Console.WriteLine(
            $"Alertas individuais: " +
            $"{resultado.Metricas["quantidadeAlertas"]}");
    }

    
}
