using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IAApi.IntelligenceLab.Evaluation;

namespace IAApi.Tests.IntelligenceLab;

public sealed class SyntheticGradualDriftScenarioTests
{
    [Fact]
    public void Scenario_IsDeterministic_AndDeclaresDriftRecoveryAndNormalBoundaries()
    {
        var first = SyntheticGradualDriftScenario.Create();
        var second = SyntheticGradualDriftScenario.Create();

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(120, first.Training.Count);
        Assert.Equal(120, first.Evaluation.Count);
        Assert.All(first.Training, reading => Assert.InRange(reading.Valor, 49f, 51f));
        Assert.All(first.Evaluation.Take(30), reading => Assert.InRange(reading.Valor, 49f, 51f));
        Assert.InRange(first.Evaluation[30].Valor, 49.849f, 49.851f);
        Assert.InRange(first.Evaluation[74].Valor, 34.449f, 34.451f);
        Assert.All(first.Evaluation.Skip(75).Take(15), reading => Assert.Equal(34.1f, reading.Valor));
        Assert.InRange(first.Evaluation[90].Valor, 35.139f, 35.141f);
        Assert.InRange(first.Evaluation[104].Valor, 49.699f, 49.701f);
        Assert.All(first.Evaluation.Skip(105), reading => Assert.InRange(reading.Valor, 49f, 51f));
        Assert.Equal("2026-10-02T09:59:00", first.Training[^1].DataHora);
        Assert.Equal("2026-10-02T10:30:00", first.Evaluation[30].DataHora);
        Assert.Equal("2026-10-02T11:30:00", first.Evaluation[90].DataHora);
        Assert.Equal("2026-10-02T11:45:00", first.Evaluation[105].DataHora);
        Assert.Equal(SyntheticGradualDriftScenario.RecoveryStartIndex,
            first.Metadata.RecoveryStartIndex);
        Assert.Equal(SyntheticGradualDriftScenario.RecoveryEndExclusiveIndex,
            first.Metadata.RecoveryEndExclusiveIndex);
    }

    [Fact]
    public void Labels_AreIndependent_AndRecoveryIsNotScoredAsNormalOrIncident()
    {
        var dataset = SyntheticGradualDriftScenario.Create();
        var labels = dataset.IndependentLabelsTemplate;
        var incident = Assert.Single(labels.Events);

        Assert.Equal("evt-gradual-flow-drift-001", incident.EventId);
        Assert.Equal("GradualFlowDeterioration", incident.FaultType);
        Assert.Equal("ProcessVariableDrift", incident.FaultClass);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 13, 30, 0, TimeSpan.Zero), incident.StartedAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 14, 30, 0, TimeSpan.Zero), incident.EndedAtUtc);
        Assert.Equal(incident.StartedAtUtc.AddMinutes(30), incident.DetectionWindowClosesAtUtc);
        Assert.Equal(incident.EndedAtUtc, incident.LateDetectionClosesAtUtc);
        Assert.Equal(EventLabelOriginKind.Simulator, incident.LabelOrigin.Kind);
        Assert.Equal("PENDING_REPLAY_RUN_ID", labels.ReplayRunId);
        Assert.Equal("PENDING_REPLAY_CONTENT_SHA256", labels.ReplayContentSha256);

        var readings = dataset.Evaluation.Select((reading, index) =>
            new AiReadingEvaluationResult(
                IndustrialEvaluationSchemas.ReadingV1, "contract-only-run", labels.ScenarioId,
                labels.TestClientId, $"reading-{index:000}", reading.TagName,
                ToUtc(reading.DataHora), null,
                index is 95 or 100, null, "ContractValidationOnly")).ToArray();
        var report = new IndustrialAlertEvaluationService().Evaluate(new IndustrialEvaluationRun(
            IndustrialEvaluationSchemas.RunV1, "contract-only-run", labels.ScenarioId,
            labels.TestClientId, TimeSpan.FromSeconds(labels.AlertEpisodeCadenceSeconds),
            labels.MinimumEventCoverageReadings, labels.ExecutionIdentity, labels.Events,
            labels.NormalIntervals, readings));

        Assert.Equal(45, report.ReadingMetrics.ReadingsInExplicitNormalIntervals);
        Assert.Equal(0, report.ReadingMetrics.FinalAlertsInExplicitNormalIntervals);
        Assert.Equal(2, report.ReadingMetrics.FinalAlertsOutsideExplicitNormalIntervals);
        Assert.Equal(0, report.EpisodeMetrics.FalsePositiveEpisodes);
        Assert.Equal(2, report.EpisodeMetrics.NonScorableEpisodes);
        Assert.Equal(EventEvaluationClassification.Missed, Assert.Single(report.Events).Classification);
    }

    [Fact]
    public async Task GeneratorCli_GradualDriftOption_WritesDeterministicScenarioArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "IAApi-M42A-", Guid.NewGuid().ToString("N"));
        try
        {
            var first = Path.Combine(root, "first");
            var second = Path.Combine(root, "second");
            Assert.Equal(0, (await RunGeneratorAsync(first)).ExitCode);
            Assert.Equal(0, (await RunGeneratorAsync(second)).ExitCode);

            var names = new[] { "treino.json", "avaliacao.json",
                "intelligence-lab-independent-labels.template.json",
                "intelligence-lab-scenario-definition.json" };
            foreach (var name in names)
                Assert.Equal(File.ReadAllBytes(Path.Combine(first, name)),
                    File.ReadAllBytes(Path.Combine(second, name)));

            using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, names[3])));
            Assert.Equal("gradual-flow-drift-v1",
                metadata.RootElement.GetProperty("scenarioDefinitionId").GetString());
            Assert.Equal(90, metadata.RootElement.GetProperty("recoveryStartIndex").GetInt32());
            Assert.Equal(105, metadata.RootElement.GetProperty("recoveryEndExclusiveIndex").GetInt32());
            Assert.Equal(Hash(Path.Combine(first, names[0])),
                metadata.RootElement.GetProperty("trainingSha256").GetString());
            Assert.Equal(Hash(Path.Combine(first, names[1])),
                metadata.RootElement.GetProperty("evaluationSha256").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static DateTimeOffset ToUtc(string value) =>
        new DateTimeOffset(DateTime.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss",
            CultureInfo.InvariantCulture), TimeSpan.FromHours(-3)).ToUniversalTime();

    private static string Hash(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunGeneratorAsync(string folder)
    {
        var project = Path.Combine(FindRepositoryRoot(), "tools", "IntelligenceLabScenarioGenerator",
            "IAApi.IntelligenceLabScenarioGenerator.csproj");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = FindRepositoryRoot(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "run", "--no-build", "--project", project, "--",
                     "--scenario", "gradual-drift", folder })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Gerador indisponivel.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "IAApi.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Checkout IAApi nao encontrado.");
    }
}
