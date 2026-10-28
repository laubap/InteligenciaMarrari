using System.Text.Json;
using IAApi.IntelligenceLab.Evaluation;

namespace IAApi.Tests.IntelligenceLab;

public sealed class ReplayLabeledEvaluationRunnerTests
{
    private const string RunId = "replay-unlabeled-example";
    private const string Scenario = "unlabeled-historical-replay-pk7";
    private const string Client = "SIM-IL-REPLAY-PK7";
    private const string Tag = "PK7.TAG1";
    private static readonly DateTimeOffset Start =
        new(2026, 10, 15, 13, 0, 0, TimeSpan.Zero);
    private static readonly string Sha = new('a', 64);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void ReplayExportadoMaisGabaritoSeparadoProduzUmEventoEUmEpisodio()
    {
        var report = ReplayLabeledEvaluationRunner.Evaluate(Export(), Labels());
        Assert.Equal(1, report.EventMetrics.DetectedOnTime);
        Assert.Equal(1, report.EventMetrics.EvaluableEvents);
        Assert.Equal(0, report.EventMetrics.Missed);
        Assert.Equal(1, report.EpisodeMetrics.TotalEpisodes);
        Assert.Equal(1, report.EpisodeMetrics.PrimaryOnTimeDetections);
        Assert.Equal(0, report.EpisodeMetrics.FalsePositiveEpisodes);
        Assert.Equal(20, report.ReadingMetrics.TotalReadings);
        Assert.Equal(5, report.ReadingMetrics.FinalAlertReadings);
        Assert.Equal(TimeSpan.Zero, Assert.Single(report.Events).DetectionDelay);
        Assert.Equal(ReplayLabeledEvaluationRunner.Unverified,
            report.ExecutionIdentity.ConfigurationFingerprint);
    }

    [Fact]
    public void NaoAceitaGabaritoPertencenteAOutroReplay()
    {
        var labels = Labels().Replace(Sha, new string('b', 64), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            ReplayLabeledEvaluationRunner.Evaluate(Export(), labels));
    }

    [Fact]
    public void NaoAvaliaReplaySemEquivalenciaConfirmada()
    {
        var export = Export().Replace("\"decisionEquivalenceVerified\":true",
            "\"decisionEquivalenceVerified\":false", StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            ReplayLabeledEvaluationRunner.Evaluate(export, Labels()));
    }

    [Fact]
    public void NaoAceitaCommitInventadoOuModeloNaoVerificado()
    {
        var labels = Labels().Replace("UNVERIFIED", "b99a7f0", StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            ReplayLabeledEvaluationRunner.Evaluate(Export(), labels));
    }

    [Fact]
    public void RejeitaNormalidadeDuranteFalhaFisica()
    {
        var normal = new ExplicitNormalInterval("bad", Scenario, Client,
            [Tag], Start.AddMinutes(16), Start.AddMinutes(17), "invalid-normal");
        Assert.Throws<IndustrialEvaluationValidationException>(() =>
            ReplayLabeledEvaluationRunner.Evaluate(Export(), Labels([normal])));
    }

    private static string Export()
    {
        var readings = Enumerable.Range(0, 20).Select(i =>
            new AiReadingEvaluationResult(
                IndustrialEvaluationSchemas.ReadingV1,
                RunId, Scenario, Client, $"reading-{i:00}", Tag,
                Start.AddMinutes(i), i >= 15 ? 75 : 0,
                i >= 15, Guid.NewGuid(), "LegacyFeatureDisabled")).ToArray();
        return JsonSerializer.Serialize(new
        {
            schemaVersion = "industrial-replay-readings-export/v1",
            runId = RunId, scenarioId = Scenario, testClientId = Client,
            datasetLabeled = false, effectivenessEvaluated = false,
            decisionEquivalenceVerified = true, replayContentSha256 = Sha,
            executionIdentityComplete = false,
            runManifestSchemaVersion = ReplayRunManifest.SchemaVersion,
            runManifestFileName = "intelligence-lab-run-manifest.json",
            readings
        }, JsonOptions);
    }

    private static string Labels(IReadOnlyList<ExplicitNormalInterval>? normalOverride = null)
    {
        var fault = new IndustrialLabeledEvent(
            IndustrialEvaluationSchemas.EventV1, "evt-stop-001", Scenario, Client,
            [Tag], null, "PumpStop", "EquipmentStop", IndustrialSeverity.High,
            Start.AddMinutes(15), Start.AddMinutes(20),
            Start.AddMinutes(15), Start.AddMinutes(17), Start.AddMinutes(19),
            new EventLabelOrigin(EventLabelOriginKind.Simulator, "synthetic-input", "DeclaredFixture"));
        var normal = new ExplicitNormalInterval("normal-001", Scenario, Client,
            [Tag], Start, Start.AddMinutes(15), "synthetic-input");
        return JsonSerializer.Serialize(new ReplayIndependentLabels(
            ReplayLabeledEvaluationRunner.LabelsSchemaV1,
            RunId, Sha, Scenario, Client, 120, 1,
            new EvaluationExecutionIdentity("UNVERIFIED", "UNVERIFIED",
                "UNVERIFIED", "UNVERIFIED", []),
            [fault], normalOverride ?? [normal]), JsonOptions);
    }
}
