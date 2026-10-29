using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IAApi.IntelligenceLab.Evaluation;
using Xunit;

namespace IAApi.Tests.IntelligenceLab;

public sealed class SyntheticFlowDropScenarioTests
{
    [Fact]
    public void Scenario_IsDeterministic_AndHasExactFailureAndRecoveryBoundaries()
    {
        var first = SyntheticFlowDropScenario.Create();
        var second = SyntheticFlowDropScenario.Create();
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));

        Assert.Equal(120, first.Training.Count);
        Assert.Equal(120, first.Evaluation.Count);
        Assert.All(first.Training, reading => Assert.InRange(reading.Valor, 49f, 51f));
        Assert.All(first.Evaluation.Take(60), reading => Assert.InRange(reading.Valor, 49f, 51f));
        Assert.All(first.Evaluation.Skip(60).Take(30), reading => Assert.Equal(0f, reading.Valor));
        Assert.All(first.Evaluation.Skip(90), reading => Assert.InRange(reading.Valor, 49f, 51f));
        Assert.All(first.Training.Concat(first.Evaluation), reading =>
        {
            Assert.Equal("PK7.TAG1", reading.TagName);
            Assert.Equal("L/s", reading.Unidade);
            Assert.Matches("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}$", reading.DataHora);
        });
        Assert.Equal("2026-10-01T09:59:00", first.Training[^1].DataHora);
        Assert.Equal("2026-10-01T10:00:00", first.Evaluation[0].DataHora);
        Assert.Equal("2026-10-01T11:00:00", first.Evaluation[60].DataHora);
        Assert.Equal("2026-10-01T11:29:00", first.Evaluation[89].DataHora);
        Assert.Equal("2026-10-01T11:30:00", first.Evaluation[90].DataHora);
        Assert.Equal("2026-10-01T11:59:00", first.Evaluation[^1].DataHora);
    }

    [Fact]
    public void Labels_UseExistingContracts_UTC_AndDoNotInventReplayIdentity()
    {
        var dataset = SyntheticFlowDropScenario.Create();
        var labels = dataset.IndependentLabelsTemplate;
        Assert.Equal(ReplayLabeledEvaluationRunner.LabelsSchemaV1, labels.SchemaVersion);
        Assert.Equal(SyntheticFlowDropScenario.PendingRunId, labels.ReplayRunId);
        Assert.Equal(SyntheticFlowDropScenario.PendingContentSha256, labels.ReplayContentSha256);
        Assert.Equal(ReplayLabeledEvaluationRunner.Unverified, labels.ExecutionIdentity.CodeCommit);
        Assert.Empty(labels.ExecutionIdentity.Models);
        Assert.Equal("unlabeled-historical-replay-pk7", labels.ScenarioId);
        Assert.Equal("SIM-IL-REPLAY-PK7", labels.TestClientId);
        Assert.Equal(60, labels.AlertEpisodeCadenceSeconds);
        Assert.Equal(3, labels.MinimumEventCoverageReadings);

        var fault = Assert.Single(labels.Events);
        Assert.Equal(EventLabelOriginKind.Simulator, fault.LabelOrigin.Kind);
        Assert.Equal("SustainedFlowDropToZero", fault.FaultType);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero), fault.StartedAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 14, 30, 0, TimeSpan.Zero), fault.EndedAtUtc);
        Assert.Equal(fault.StartedAtUtc, fault.DetectionWindowOpensAtUtc);
        Assert.Equal(fault.StartedAtUtc.AddMinutes(10), fault.DetectionWindowClosesAtUtc);
        Assert.Equal(fault.EndedAtUtc, fault.LateDetectionClosesAtUtc);
        Assert.All(labels.NormalIntervals, interval =>
            Assert.Equal(TimeSpan.Zero, interval.StartsAtUtc.Offset));
        Assert.Equal(2, labels.NormalIntervals.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero),
            labels.NormalIntervals[0].StartsAtUtc);
        Assert.Equal(fault.StartedAtUtc, labels.NormalIntervals[0].EndsAtUtc);
        Assert.Equal(fault.EndedAtUtc, labels.NormalIntervals[1].StartsAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero),
            labels.NormalIntervals[1].EndsAtUtc);

        // Valida o gabarito com o validador existente; nenhuma decisao da IA e
        // gerada pelo gerador. As leituras neutras abaixo sao apenas teste de contrato.
        var runId = "synthetic-contract-test-only";
        var readings = dataset.Evaluation.Select((reading, i) =>
            new AiReadingEvaluationResult(
                IndustrialEvaluationSchemas.ReadingV1, runId,
                labels.ScenarioId, labels.TestClientId, $"reading-{i:000}",
                SyntheticFlowDropScenario.TagName,
                new DateTimeOffset(DateTime.ParseExact(reading.DataHora,
                    "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                    TimeSpan.FromHours(-3)).ToUniversalTime(),
                null, false, null, "ContractValidationOnly")).ToArray();
        var run = new IndustrialEvaluationRun(
            IndustrialEvaluationSchemas.RunV1, runId, labels.ScenarioId,
            labels.TestClientId, TimeSpan.FromSeconds(labels.AlertEpisodeCadenceSeconds),
            labels.MinimumEventCoverageReadings, labels.ExecutionIdentity,
            labels.Events, labels.NormalIntervals, readings);
        IndustrialEvaluationValidator.Validate(run);
    }

    [Fact]
    public async Task GeneratorCli_WritesDeterministicFiles_AndRefusesOverwrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "IAApi-M41A-", Guid.NewGuid().ToString("N"));
        try
        {
            var first = Path.Combine(root, "first");
            var second = Path.Combine(root, "second");
            var firstRun = await RunGeneratorAsync(first);
            Assert.Equal(0, firstRun.ExitCode);
            var secondRun = await RunGeneratorAsync(second);
            Assert.Equal(0, secondRun.ExitCode);

            var names = new[] { "treino.json", "avaliacao.json",
                "intelligence-lab-independent-labels.template.json",
                "intelligence-lab-scenario-definition.json" };
            foreach (var name in names)
                Assert.Equal(File.ReadAllBytes(Path.Combine(first, name)),
                    File.ReadAllBytes(Path.Combine(second, name)));

            using var training = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, names[0])));
            using var evaluation = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, names[1])));
            using var labels = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, names[2])));
            using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, names[3])));
            Assert.Equal(120, training.RootElement.GetArrayLength());
            Assert.Equal(120, evaluation.RootElement.GetArrayLength());
            Assert.Equal("PENDING_REPLAY_RUN_ID",
                labels.RootElement.GetProperty("replayRunId").GetString());
            Assert.Equal("Simulator", labels.RootElement.GetProperty("events")[0]
                .GetProperty("labelOrigin").GetProperty("kind").GetString());
            Assert.Equal("flow-drop-sustained-v1",
                metadata.RootElement.GetProperty("scenarioDefinitionId").GetString());
            Assert.Equal(Hash(Path.Combine(first, names[0])),
                metadata.RootElement.GetProperty("trainingSha256").GetString());
            Assert.Equal(Hash(Path.Combine(first, names[1])),
                metadata.RootElement.GetProperty("evaluationSha256").GetString());
            Assert.Equal(Hash(Path.Combine(first, names[2])),
                metadata.RootElement.GetProperty("labelsTemplateSha256").GetString());

            var before = names.ToDictionary(name => name,
                name => Hash(Path.Combine(first, name)));
            var refused = await RunGeneratorAsync(first);
            Assert.Equal(1, refused.ExitCode);
            foreach (var name in names)
                Assert.Equal(before[name], Hash(Path.Combine(first, name)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string Hash(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunGeneratorAsync(string folder)
    {
        var repo = FindRepositoryRoot();
        var project = Path.Combine(repo, "tools", "IntelligenceLabScenarioGenerator",
            "IAApi.IntelligenceLabScenarioGenerator.csproj");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "run", "--no-build", "--project", project, "--", folder })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ??
            throw new InvalidOperationException("Nao foi possivel iniciar o gerador.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw new TimeoutException("Gerador excedeu dois minutos.");
        }
        var output = await stdout;
        var error = await stderr;
        Assert.True(process.ExitCode is 0 or 1,
            $"Falha inesperada (exit={process.ExitCode}): {output}\n{error}");
        return (process.ExitCode, output, error);
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "IAApi.csproj")) &&
                File.Exists(Path.Combine(dir.FullName, "tools", "IntelligenceLabScenarioGenerator",
                    "IAApi.IntelligenceLabScenarioGenerator.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Checkout IAApi nao encontrado.");
    }
}
