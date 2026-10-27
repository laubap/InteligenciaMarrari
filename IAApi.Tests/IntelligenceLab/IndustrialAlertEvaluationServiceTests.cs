using IAApi.IntelligenceLab.Evaluation;

namespace IAApi.Tests.IntelligenceLab;

public sealed class IndustrialAlertEvaluationServiceTests
{
    private const string RunId = "run-pump-stop-v1";
    private const string ScenarioId = "pump-stop-v1";
    private const string ClientId = "SIM-IL-001";
    private const string PumpFlow = "P101.FLOW";
    private const string PumpPressure = "P101.PRESSURE";

    private readonly IndustrialAlertEvaluationService _service = new();

    [Fact]
    public void EventoDetectadoNoPrazo_ContaUmTruePositivePorEvento()
    {
        var start = At(10, 0);
        var report = _service.Evaluate(Run(
            events: [Event("evt-stop", [PumpFlow], start)],
            readings: [Reading("r1", PumpFlow, start.AddMinutes(1), alert: true)]));

        var result = Assert.Single(report.Events);
        Assert.Equal(EventEvaluationClassification.DetectedOnTime, result.Classification);
        Assert.Equal(1, report.EventMetrics.DetectedOnTime);
        Assert.Equal(1, report.EventMetrics.EvaluableEvents);
        Assert.Equal(1, report.EventMetrics.StrictRecall);
        Assert.Equal(AlertEpisodeClassification.PrimaryOnTimeDetection,
            Assert.Single(report.Episodes).Classification);
    }

    [Fact]
    public void EventoDetectadoComAtraso_NaoAumentaRecallEstrito()
    {
        var start = At(10, 0);
        var report = _service.Evaluate(Run(
            events: [Event("evt-stop", [PumpFlow], start)],
            readings:
            [
                Reading("r1", PumpFlow, start.AddMinutes(1), alert: false),
                Reading("r2", PumpFlow, start.AddMinutes(5), alert: true)
            ]));

        var result = Assert.Single(report.Events);
        Assert.Equal(EventEvaluationClassification.DetectedLate, result.Classification);
        Assert.Equal(1, report.EventMetrics.DetectedLate);
        Assert.Equal(0, report.EventMetrics.StrictRecall);
        Assert.Equal(AlertEpisodeClassification.LateDetection,
            Assert.Single(report.Episodes).Classification);
    }

    [Fact]
    public void EventoSemAlertaElegivel_EhClassificadoComoPerdido()
    {
        var start = At(10, 0);
        var report = _service.Evaluate(Run(
            events: [Event("evt-stop", [PumpFlow], start)],
            readings: [Reading("r1", PumpFlow, start.AddMinutes(1), alert: false)]));

        Assert.Equal(EventEvaluationClassification.Missed,
            Assert.Single(report.Events).Classification);
        Assert.Equal(1, report.EventMetrics.Missed);
        Assert.Empty(report.Episodes);
    }

    [Fact]
    public void AlertaEmIntervaloNormalExplicito_EhFalsoPositivoPorEpisodio()
    {
        var start = At(10, 0);
        var report = _service.Evaluate(Run(
            events: [Event("evt-stop", [PumpFlow], start)],
            normalIntervals: [Normal("normal-before", PumpFlow, At(9, 0), start)],
            readings:
            [
                Reading("r1", PumpFlow, At(9, 30), alert: true),
                Reading("r2", PumpFlow, start.AddMinutes(1), alert: false)
            ]));

        var episode = Assert.Single(report.Episodes);
        Assert.Equal(AlertEpisodeClassification.FalsePositive, episode.Classification);
        Assert.Equal(1, report.EpisodeMetrics.FalsePositiveEpisodes);
        Assert.Equal(EventEvaluationClassification.Missed,
            Assert.Single(report.Events).Classification);
    }

    [Fact]
    public void AlertasConsecutivos_FormamUmUnicoEpisodio()
    {
        var start = At(10, 0);
        var run = Run(
            events: [Event("evt-stop", [PumpFlow], start)],
            readings:
            [
                Reading("r1", PumpFlow, start, alert: true),
                Reading("r2", PumpFlow, start.AddMinutes(1), alert: true),
                Reading("r3", PumpFlow, start.AddMinutes(2), alert: true)
            ]);
        var report = _service.Evaluate(run);

        var episode = Assert.Single(
            AlertEpisodeNormalizer.Normalize(run.Readings, TimeSpan.FromMinutes(1)));
        Assert.Equal(3, episode.AlertReadingCount);
        Assert.Equal(1, report.EventMetrics.DetectedOnTime);
        Assert.Equal(1, report.EpisodeMetrics.TotalEpisodes);
    }

    [Fact]
    public void LeituraNegativaEntreAlertas_SeparaEpisodiosMesmoDentroDaCadencia()
    {
        var start = At(10, 0);
        var readings = new[]
        {
            Reading("r1", PumpFlow, start, alert: true),
            Reading("r2", PumpFlow, start.AddMinutes(1), alert: false),
            Reading("r3", PumpFlow, start.AddMinutes(2), alert: true)
        };

        var episodes = AlertEpisodeNormalizer.Normalize(readings, TimeSpan.FromMinutes(2));

        Assert.Equal(2, episodes.Count);
        Assert.All(episodes, episode => Assert.Equal(1, episode.AlertReadingCount));
        Assert.Equal(start, episodes[0].StartedAtUtc);
        Assert.Equal(start.AddMinutes(2), episodes[1].StartedAtUtc);
    }

    [Fact]
    public void EpisodioIniciadoAntesDoEvento_NaoRecebeCreditoAutomatico()
    {
        var start = At(10, 0);
        var report = _service.Evaluate(Run(
            cadence: TimeSpan.FromMinutes(2),
            events: [Event("evt-stop", [PumpFlow], start)],
            normalIntervals: [Normal("normal-before", PumpFlow, At(9, 0), start)],
            readings:
            [
                Reading("r1", PumpFlow, start.AddMinutes(-1), alert: true),
                Reading("r2", PumpFlow, start.AddMinutes(1), alert: true)
            ]));

        Assert.Equal(EventEvaluationClassification.Missed,
            Assert.Single(report.Events).Classification);
        var episode = Assert.Single(report.Episodes);
        Assert.Equal(AlertEpisodeClassification.FalsePositive, episode.Classification);
        Assert.Equal("StartedBeforeAllowedEventWindow", episode.Reason);
    }

    [Fact]
    public void EventoSemCoberturaDeLeituras_NaoEntraNoDenominador()
    {
        var start = At(10, 0);
        var report = _service.Evaluate(Run(
            minimumCoverage: 2,
            events: [Event("evt-stop", [PumpFlow], start)],
            readings: [Reading("r1", PumpFlow, start.AddMinutes(1), alert: true)]));

        var result = Assert.Single(report.Events);
        Assert.Equal(EventEvaluationClassification.NotEvaluable, result.Classification);
        Assert.Equal(0, report.EventMetrics.EvaluableEvents);
        Assert.Null(report.EventMetrics.StrictRecall);
        Assert.Equal(AlertEpisodeClassification.NonScorable,
            Assert.Single(report.Episodes).Classification);
    }

    [Fact]
    public void EventosSimultaneosEmTagsDistintas_SaoAssociadosDeterministicamente()
    {
        var start = At(10, 0);
        var report = _service.Evaluate(Run(
            events:
            [
                Event("evt-flow", [PumpFlow], start),
                Event("evt-pressure", [PumpPressure], start)
            ],
            readings:
            [
                Reading("r1", PumpFlow, start.AddMinutes(1), alert: true),
                Reading("r2", PumpPressure, start.AddMinutes(1), alert: true)
            ]));

        Assert.Equal(2, report.EventMetrics.DetectedOnTime);
        Assert.All(report.Events, item =>
            Assert.Equal(EventEvaluationClassification.DetectedOnTime, item.Classification));
        Assert.Equal(2, report.EpisodeMetrics.PrimaryOnTimeDetections);
    }

    [Fact]
    public void MesmaAvaliacao_GeraRelatorioJsonIdentico()
    {
        var start = At(10, 0);
        var run = Run(
            events: [Event("evt-stop", [PumpFlow], start)],
            readings:
            [
                Reading("r1", PumpFlow, start.AddMinutes(1), alert: true),
                Reading("r2", PumpFlow, start.AddMinutes(8), alert: true)
            ]);

        var first = IndustrialEvaluationReportJson.Serialize(_service.Evaluate(run));
        var second = IndustrialEvaluationReportJson.Serialize(_service.Evaluate(run));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ContratosRejeitamClienteNaoSinteticoENormalidadeSobrepostaAEvento()
    {
        var start = At(10, 0);
        var invalid = Run(
            clientId: "CLIENTE-REAL",
            events: [Event("evt-stop", [PumpFlow], start, clientId: "CLIENTE-REAL")],
            normalIntervals: [Normal("invalid-normal", PumpFlow, start, start.AddMinutes(2),
                clientId: "CLIENTE-REAL")],
            readings: [Reading("r1", PumpFlow, start.AddMinutes(1), false,
                clientId: "CLIENTE-REAL")]);

        var exception = Assert.Throws<IndustrialEvaluationValidationException>(
            () => _service.Evaluate(invalid));

        Assert.Contains(exception.Errors, error => error.Contains("prefixo 'SIM-'", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("se sobrepõe à janela de detecção", StringComparison.Ordinal));
    }

    [Fact]
    public void ContratosRejeitamNormalidadeDentroDaJanelaAntecipadaDeDeteccao()
    {
        var start = At(10, 0);
        var earlyWindowEvent = Event("evt-stop", [PumpFlow], start) with
        {
            DetectionWindowOpensAtUtc = start.AddMinutes(-2)
        };
        var invalid = Run(
            events: [earlyWindowEvent],
            normalIntervals: [Normal("normal-early", PumpFlow,
                start.AddMinutes(-1), start)],
            readings: [Reading("r1", PumpFlow, start.AddMinutes(1), alert: false)]);

        var exception = Assert.Throws<IndustrialEvaluationValidationException>(
            () => _service.Evaluate(invalid));

        Assert.Contains(exception.Errors, error =>
            error.Contains("se sobrepõe à janela de detecção", StringComparison.Ordinal));
    }

    [Fact]
    public void ContratosRejeitamNormalidadeDuranteFalhaDepoisDoPrazoTardio()
    {
        var start = At(10, 0);
        var eventWithShortDetectionWindow = Event("evt-stop", [PumpFlow], start) with
        {
            LateDetectionClosesAtUtc = start.AddMinutes(5)
        };
        var invalid = Run(
            events: [eventWithShortDetectionWindow],
            normalIntervals: [Normal("normal-during-fault", PumpFlow,
                start.AddMinutes(6), start.AddMinutes(10))],
            readings: [Reading("r1", PumpFlow, start.AddMinutes(7), alert: true)]);

        var exception = Assert.Throws<IndustrialEvaluationValidationException>(
            () => _service.Evaluate(invalid));

        Assert.Contains(exception.Errors, error =>
            error.Contains("se sobrepõe à duração física do evento", StringComparison.Ordinal));
    }

    [Fact]
    public void ContratosPermitemNormalidadeIniciadaExatamenteNoFimFisicoDoEvento()
    {
        var start = At(10, 0);
        var eventWithShortDetectionWindow = Event("evt-stop", [PumpFlow], start) with
        {
            LateDetectionClosesAtUtc = start.AddMinutes(5)
        };
        var run = Run(
            events: [eventWithShortDetectionWindow],
            normalIntervals: [Normal("normal-after-fault", PumpFlow,
                start.AddMinutes(15), start.AddMinutes(20))],
            readings:
            [
                Reading("r1", PumpFlow, start.AddMinutes(1), alert: false),
                Reading("r2", PumpFlow, start.AddMinutes(16), alert: true)
            ]);

        var report = _service.Evaluate(run);

        Assert.Equal(EventEvaluationClassification.Missed,
            Assert.Single(report.Events).Classification);
        Assert.Equal(AlertEpisodeClassification.FalsePositive,
            Assert.Single(report.Episodes).Classification);
    }

    [Fact]
    public void RelatorioPreservaIdentidadeDaExecucaoEPoliticaDeMatching()
    {
        var start = At(10, 0);
        var run = Run(
            cadence: TimeSpan.FromMinutes(2),
            minimumCoverage: 2,
            events: [Event("evt-stop", [PumpFlow], start)],
            readings:
            [
                Reading("r1", PumpFlow, start, alert: false),
                Reading("r2", PumpFlow, start.AddMinutes(1), alert: true)
            ]);

        var report = _service.Evaluate(run);

        Assert.Equal("1412cc1", report.ExecutionIdentity.CodeCommit);
        Assert.Equal("sha256:test", report.ExecutionIdentity.ConfigurationFingerprint);
        Assert.Equal("model:sim-pump", Assert.Single(report.ExecutionIdentity.Models).ModelId);
        Assert.Equal(TimeSpan.FromMinutes(2), report.MatchingPolicy.AlertEpisodeCadence);
        Assert.Equal(2, report.MatchingPolicy.MinimumEventCoverageReadings);
        Assert.Contains("NegativeReadingEndsEpisode", report.MatchingPolicy.EpisodeBoundaryRule,
            StringComparison.Ordinal);
        Assert.Contains("DoesNotAssessTemporalContinuity", report.MatchingPolicy.CoverageLimitation,
            StringComparison.Ordinal);
    }

    private static IndustrialEvaluationRun Run(
        IReadOnlyList<IndustrialLabeledEvent>? events = null,
        IReadOnlyList<ExplicitNormalInterval>? normalIntervals = null,
        IReadOnlyList<AiReadingEvaluationResult>? readings = null,
        TimeSpan? cadence = null,
        int minimumCoverage = 1,
        string clientId = ClientId) =>
        new(
            IndustrialEvaluationSchemas.RunV1,
            RunId,
            ScenarioId,
            clientId,
            cadence ?? TimeSpan.FromMinutes(1),
            minimumCoverage,
            new EvaluationExecutionIdentity(
                "1412cc1",
                "test-api",
                "pump-stop-v1",
                "sha256:test",
                [new EvaluationModelReference("Ensemble", "model:sim-pump", "1",
                    At(8, 0))]),
            events ?? [],
            normalIntervals ?? [],
            readings ?? []);

    private static IndustrialLabeledEvent Event(
        string id,
        IReadOnlyList<string> tags,
        DateTimeOffset start,
        string clientId = ClientId) =>
        new(
            IndustrialEvaluationSchemas.EventV1,
            id,
            ScenarioId,
            clientId,
            tags,
            null,
            "PumpStop",
            "EquipmentStop",
            IndustrialSeverity.High,
            start,
            start.AddMinutes(15),
            start,
            start.AddMinutes(3),
            start.AddMinutes(15),
            new EventLabelOrigin(EventLabelOriginKind.Simulator, "sim-v1", "Approved"));

    private static ExplicitNormalInterval Normal(
        string id,
        string tag,
        DateTimeOffset start,
        DateTimeOffset end,
        string clientId = ClientId) =>
        new(id, ScenarioId, clientId, [tag], start, end, "sim-v1");

    private static AiReadingEvaluationResult Reading(
        string id,
        string tag,
        DateTimeOffset timestamp,
        bool alert,
        string clientId = ClientId) =>
        new(
            IndustrialEvaluationSchemas.ReadingV1,
            RunId,
            ScenarioId,
            clientId,
            id,
            tag,
            timestamp,
            alert ? 80 : 20,
            alert,
            null,
            "TestDecision");

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 1, 10, hour, minute, 0, TimeSpan.Zero);
}
