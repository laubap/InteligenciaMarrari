using IAApi.IntelligenceLab.Evaluation;

namespace IAApi.Tests.IntelligenceLab;

public sealed class ReplayReadingEvaluationMapperTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 1, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapeiaDecisaoRealEIdDoDiagnosticoSemInventarRotulo()
    {
        var diagnosticId = Guid.NewGuid();
        var readings = ReplayReadingEvaluationMapper.Map(
            "run-1", "pump-stop-v1", "SIM-IL-001", "P101.FLOW",
            [new ReplayObservedPrediction(Start, true, 82.5, diagnosticId,
                "RuntimeFunctionalDecision")]);

        var reading = Assert.Single(readings);
        Assert.Equal(IndustrialEvaluationSchemas.ReadingV1, reading.SchemaVersion);
        Assert.Equal("run-1", reading.RunId);
        Assert.Equal("SIM-IL-001", reading.TestClientId);
        Assert.Equal(Start, reading.TimestampUtc);
        Assert.Equal(diagnosticId, reading.DiagnosticId);
        Assert.Equal(82.5, reading.EnsembleNormalizedScore);
        Assert.True(reading.FinalAlert);
        Assert.Equal("RuntimeFunctionalDecision", reading.DecisionSource);
        Assert.Equal("run-1:P101.FLOW:2026-01-10T10:00:00.0000000+00:00",
            reading.ReadingId);
    }

    [Fact]
    public void RejeitaClienteNaoSinteticoEIdDiagnosticoAusente()
    {
        Assert.Throws<ArgumentException>(() => ReplayReadingEvaluationMapper.Map(
            "run-1", "pump-stop", "CLIENTE-REAL", "P101.FLOW",
            [new ReplayObservedPrediction(Start, false, 10, Guid.NewGuid(), "Legacy")]));

        Assert.Throws<ArgumentException>(() => ReplayReadingEvaluationMapper.Map(
            "run-1", "pump-stop", "SIM-IL-001", "P101.FLOW",
            [new ReplayObservedPrediction(Start, false, 10, Guid.Empty, "Legacy")]));
    }

    [Fact]
    public void RejeitaHorarioNaoUtcOuDuplicadoEValoresInvalidos()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => ReplayReadingEvaluationMapper.Map(
            "run-1", "pump-stop", "SIM-IL-001", "P101.FLOW",
            [new ReplayObservedPrediction(Start.ToOffset(TimeSpan.FromHours(-3)), true, 10,
                id, "Legacy")]));

        Assert.Throws<ArgumentException>(() => ReplayReadingEvaluationMapper.Map(
            "run-1", "pump-stop", "SIM-IL-001", "P101.FLOW",
            [new ReplayObservedPrediction(Start, true, 10, id, "Legacy"),
             new ReplayObservedPrediction(Start, false, 10, Guid.NewGuid(), "Legacy")]));

        Assert.Throws<ArgumentException>(() => ReplayReadingEvaluationMapper.Map(
            "run-1", "pump-stop", "SIM-IL-001", "P101.FLOW",
            [new ReplayObservedPrediction(Start, true, double.NaN, id, "Legacy")]));
    }

    [Fact]
    public void ResultadosMapeadosPodemSerAvaliadosComRotulosIndependentes()
    {
        var time = Start.AddMinutes(1);
        var mapped = ReplayReadingEvaluationMapper.Map(
            "run-1", "pump-stop-v1", "SIM-IL-001", "P101.FLOW",
            [new ReplayObservedPrediction(time, true, 80, Guid.NewGuid(), "Legacy")]);

        // O gabarito abaixo e criado EXCLUSIVAMENTE pelo teste, nao pelo mapper.
        var labeledEvent = new IndustrialLabeledEvent(
            IndustrialEvaluationSchemas.EventV1, "evt-1", "pump-stop-v1",
            "SIM-IL-001", ["P101.FLOW"], null, "PumpStop", "EquipmentStop",
            IndustrialSeverity.High, Start, Start.AddMinutes(15), Start,
            Start.AddMinutes(3), Start.AddMinutes(15),
            new EventLabelOrigin(EventLabelOriginKind.Simulator, "fixture", "Approved"));
        var run = new IndustrialEvaluationRun(
            IndustrialEvaluationSchemas.RunV1, "run-1", "pump-stop-v1", "SIM-IL-001",
            TimeSpan.FromMinutes(1), 1,
            new EvaluationExecutionIdentity("test-commit", "test-api", "test-options",
                "sha256:test", []),
            [labeledEvent], [], mapped);

        var report = new IndustrialAlertEvaluationService().Evaluate(run);

        Assert.Equal(1, report.EventMetrics.DetectedOnTime);
        Assert.Equal(1, report.EpisodeMetrics.PrimaryOnTimeDetections);
    }
}
