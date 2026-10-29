using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IAApi.IntelligenceLab.Evaluation;
using Xunit;

namespace IAApi.Tests.IntelligenceLab;

/// <summary>
/// Testes do CLI real do Intelligence Lab. O executavel e compilado pela referencia
/// de projeto em IAApi.Tests.csproj; cada processo usa SQLite e modelo IF isolados.
/// Nao depende dos dados em Downloads, de banco de cliente ou do Bridge.
/// </summary>
public sealed class IntelligenceLabReplayModesIntegrationTests
{
    private static readonly JsonSerializerOptions ReplayInputJsonOptions =
        new(JsonSerializerDefaults.Web);
    private const string DiagnosticsName = "intelligence-lab-replay-diagnostics.jsonl";
    private const string ManifestName = "intelligence-lab-run-manifest.json";
    private const string ReportName = "intelligence-lab-replay-report.json";
    private const string ModelName = "intelligence-lab-isolation-forest-model.json";

    [Fact]
    public async Task StatisticalMode_PreservesSixDetectorReplayAndManifest()
    {
        using var fixture = new ReplayFixture();
        var output = Path.Combine(fixture.Root, "statistical");
        await RunReplayAsync(fixture, output, fullEnsemble: false);

        AssertReplay(output, expectedDetectorCount: 6, fullEnsemble: false);
        var report = ReadJson(Path.Combine(output, ReportName));
        Assert.Equal(5, report.GetProperty("On").GetProperty("AnomalyCount").GetInt32());
        Assert.False(File.Exists(Path.Combine(output, ModelName)));
    }

    [Fact]
    public async Task FullMode_UsesSevenDetectorsAndReproducesModelAndScores()
    {
        using var fixture = new ReplayFixture();
        var first = Path.Combine(fixture.Root, "full-1");
        var second = Path.Combine(fixture.Root, "full-2");
        await RunReplayAsync(fixture, first, fullEnsemble: true);
        await RunReplayAsync(fixture, second, fullEnsemble: true);

        AssertReplay(first, expectedDetectorCount: 7, fullEnsemble: true);
        AssertReplay(second, expectedDetectorCount: 7, fullEnsemble: true);

        var firstHash = FileHash(Path.Combine(first, ModelName));
        var secondHash = FileHash(Path.Combine(second, ModelName));
        Assert.Equal(firstHash, secondHash);

        var firstManifest = ReadJson(Path.Combine(first, ManifestName));
        var secondManifest = ReadJson(Path.Combine(second, ManifestName));
        Assert.Equal(
            firstManifest.GetProperty("configuration").GetProperty("fingerprintSha256").GetString(),
            secondManifest.GetProperty("configuration").GetProperty("fingerprintSha256").GetString());
        Assert.Equal(
            firstManifest.GetProperty("trainingInput").GetProperty("sha256").GetString(),
            secondManifest.GetProperty("trainingInput").GetProperty("sha256").GetString());

        var firstReport = ReadJson(Path.Combine(first, ReportName));
        var secondReport = ReadJson(Path.Combine(second, ReportName));
        var firstReadings = firstReport.GetProperty("Readings").EnumerateArray().ToArray();
        var secondReadings = secondReport.GetProperty("Readings").EnumerateArray().ToArray();
        Assert.Equal(20, firstReadings.Length);
        Assert.Equal(firstReadings.Length, secondReadings.Length);
        for (var i = 0; i < firstReadings.Length; i++)
        {
            Assert.Equal(firstReadings[i].GetProperty("DataHora").GetString(),
                secondReadings[i].GetProperty("DataHora").GetString());
            Assert.Equal(firstReadings[i].GetProperty("EhAnomalia").GetBoolean(),
                secondReadings[i].GetProperty("EhAnomalia").GetBoolean());
            Assert.InRange(Math.Abs(firstReadings[i].GetProperty("Score").GetDouble() -
                secondReadings[i].GetProperty("Score").GetDouble()), 0, 1e-6);
        }
    }

    [Fact]
    public async Task GradualDriftScenario_ReplaysIdenticalInputsAndKeepsRecoveryUnlabeled()
    {
        var scenario = SyntheticGradualDriftScenario.Create();
        using var fixture = new ReplayFixture(scenario);
        var sixOutput = Path.Combine(fixture.Root, "gradual-six");
        var sevenOutput = Path.Combine(fixture.Root, "gradual-seven");

        await RunReplayAsync(fixture, sixOutput, fullEnsemble: false, evaluationLimit: null);
        await RunReplayAsync(fixture, sevenOutput, fullEnsemble: true, evaluationLimit: null);

        AssertReplay(sixOutput, expectedDetectorCount: 6, fullEnsemble: false, expectedEvaluationCount: 120);
        AssertReplay(sevenOutput, expectedDetectorCount: 7, fullEnsemble: true, expectedEvaluationCount: 120);
        var sixLabels = BindLabels(sixOutput, scenario.IndependentLabelsTemplate);
        var sevenLabels = BindLabels(sevenOutput, scenario.IndependentLabelsTemplate);
        var comparison = ReplayModeComparisonService.Compare(
            File.ReadAllText(Path.Combine(sixOutput, "intelligence-lab-replay-evaluation-readings.json")),
            sixLabels,
            File.ReadAllText(Path.Combine(sevenOutput, "intelligence-lab-replay-evaluation-readings.json")),
            sevenLabels);

        Assert.Equal(120, comparison.AlignedReadings);
        Assert.Equal(15, comparison.SixDetectors.UnlabeledReadings);
        Assert.Equal(15, comparison.SevenDetectors.UnlabeledReadings);
        AssertScorableReadingCounts(comparison.SixDetectors);
        AssertScorableReadingCounts(comparison.SevenDetectors);
        Assert.InRange(comparison.SixDetectors.ReadingRecall!.Value, 0d, 1d);
        Assert.InRange(comparison.SevenDetectors.ReadingRecall!.Value, 0d, 1d);
        Assert.All(comparison.Phases, phase =>
        {
            Assert.True(phase.Readings > 0);
            Assert.True(double.IsFinite(phase.MeanScoreSix!.Value));
            Assert.True(double.IsFinite(phase.MeanScoreSeven!.Value));
        });
        Assert.Equal(30, comparison.Phases.Single(phase => phase.Phase == "Before").Readings);
        Assert.Equal(60, comparison.Phases.Single(phase => phase.Phase == "During").Readings);
        Assert.Equal(15, comparison.Phases.Single(phase => phase.Phase == "Unlabeled").Readings);
        Assert.Equal(15, comparison.Phases.Single(phase => phase.Phase == "After").Readings);

        var sixManifest = ReadJson(Path.Combine(sixOutput, ManifestName));
        var sevenManifest = ReadJson(Path.Combine(sevenOutput, ManifestName));
        Assert.Equal(sixManifest.GetProperty("trainingInput").GetProperty("sha256").GetString(),
            sevenManifest.GetProperty("trainingInput").GetProperty("sha256").GetString());
        Assert.Equal(sixManifest.GetProperty("evaluationInput").GetProperty("sha256").GetString(),
            sevenManifest.GetProperty("evaluationInput").GetProperty("sha256").GetString());
    }

    private static void AssertReplay(string folder, int expectedDetectorCount, bool fullEnsemble,
        int expectedEvaluationCount = 20)
    {
        var report = ReadJson(Path.Combine(folder, ReportName));
        Assert.True(report.GetProperty("EquivalentDecisions").GetBoolean());
        Assert.Empty(report.GetProperty("Mismatches").EnumerateArray());
        Assert.Equal(fullEnsemble, report.GetProperty("IsolationForestEnabled").GetBoolean());
        Assert.False(report.GetProperty("DatasetLabeled").GetBoolean());
        Assert.Equal(expectedEvaluationCount, report.GetProperty("EvaluationCount").GetInt32());
        var on = report.GetProperty("On");
        Assert.Equal(expectedEvaluationCount, on.GetProperty("DiagnosticCount").GetInt32());
        Assert.Equal(expectedEvaluationCount, on.GetProperty("StoredTotal").GetInt32());
        Assert.Equal(0, on.GetProperty("CaptureFailures").GetInt32());
        Assert.Equal(0, on.GetProperty("EvictedTotal").GetInt32());

        var diagnostics = File.ReadAllLines(Path.Combine(folder, DiagnosticsName))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ReadJsonText)
            .ToArray();
        Assert.Equal(expectedEvaluationCount, diagnostics.Length);
        foreach (var entry in diagnostics)
        {
            var ensemble = entry.GetProperty("diagnostic").GetProperty("ensemble");
            var contributions = ensemble.GetProperty("contributions").EnumerateArray().ToArray();
            Assert.Equal(expectedDetectorCount, contributions.Length);
            var denominator = ensemble.GetProperty("effectiveWeightSum").GetDouble();
            Assert.True(double.IsFinite(denominator) && denominator > 0);
            var numerator = contributions.Sum(contribution =>
                contribution.GetProperty("weightedNumeratorContribution").GetDouble());
            Assert.InRange(Math.Abs(numerator / denominator -
                ensemble.GetProperty("normalizedScore").GetDouble()), 0, 1e-6);

            var ifContributions = contributions.Where(contribution =>
                contribution.GetProperty("detector").GetString() == "Isolation Forest").ToArray();
            if (fullEnsemble)
            {
                var ifContribution = Assert.Single(ifContributions);
                Assert.True(ifContribution.GetProperty("executed").GetBoolean());
                Assert.True(ifContribution.GetProperty("executionCompleted").GetBoolean());
                Assert.True(ifContribution.GetProperty("applicable").GetBoolean());
                Assert.True(ifContribution.GetProperty("participatedInAggregation").GetBoolean());
                Assert.True(ifContribution.GetProperty("effectiveWeight").GetDouble() > 0);
            }
            else
            {
                Assert.Empty(ifContributions);
            }
        }

        var manifest = ReadJson(Path.Combine(folder, ManifestName));
        Assert.Equal("intelligence-lab-run-manifest/v1",
            manifest.GetProperty("schemaVersion").GetString());
        Assert.Equal("Partial", manifest.GetProperty("reproducibilityStatus").GetString());
        var config = manifest.GetProperty("configuration");
        Assert.Equal(fullEnsemble
                ? "OfflineIsolatedHistoricalReplayFullEnsemble"
                : "OfflineIsolatedHistoricalReplay",
            config.GetProperty("mode").GetString());
        Assert.Equal(expectedDetectorCount,
            config.GetProperty("enabledDetectors").GetArrayLength());
        var resources = manifest.GetProperty("resources").EnumerateArray().ToArray();
        var ifResource = Assert.Single(resources.Where(resource =>
            resource.GetProperty("component").GetString() == "IsolationForest"));
        Assert.Equal(fullEnsemble ? "Executed" : "NotExecuted",
            ifResource.GetProperty("executionStatus").GetString());
        if (fullEnsemble)
        {
            var hash = FileHash(Path.Combine(folder, ModelName));
            Assert.Equal(hash, ifResource.GetProperty("sha256").GetString());
            Assert.Equal(hash, config.GetProperty("effectiveSettings")
                .GetProperty("isolationForestModelSha256").GetString());
        }

        Assert.Equal(FileHash(Path.Combine(folder, "..", "treino.json")),
            manifest.GetProperty("trainingInput").GetProperty("sha256").GetString());
        Assert.Equal(FileHash(Path.Combine(folder, "..", "avaliacao.json")),
            manifest.GetProperty("evaluationInput").GetProperty("sha256").GetString());
        foreach (var artifact in manifest.GetProperty("artifacts").EnumerateArray())
        {
            var name = artifact.GetProperty("name").GetString()!;
            Assert.Equal(Path.GetFileName(name), name);
            Assert.Equal(FileHash(Path.Combine(folder, name)),
                artifact.GetProperty("sha256").GetString());
        }
    }

    private static JsonElement ReadJson(string file) => ReadJsonText(File.ReadAllText(file));

    private static JsonElement ReadJsonText(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string FileHash(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    private static void AssertScorableReadingCounts(ReplayClassificationSummary summary)
    {
        Assert.Equal(60, summary.TruePositives + summary.FalseNegatives);
        Assert.Equal(45, summary.FalsePositives + summary.TrueNegatives);
        Assert.InRange(summary.LateDetectedEvents, 0, 1);
        Assert.InRange(summary.OnTimeDetectedEvents, 0, 1);
        Assert.InRange(summary.MissedEvents, 0, 1);
        Assert.Equal(1, summary.OnTimeDetectedEvents + summary.LateDetectedEvents + summary.MissedEvents);
    }

    private static string BindLabels(string output, ReplayIndependentLabels template)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var export = JsonSerializer.Deserialize<UnlabeledReplayReadingsExport>(
            File.ReadAllText(Path.Combine(output, "intelligence-lab-replay-evaluation-readings.json")), options)
            ?? throw new InvalidDataException("Exportacao do replay ausente.");
        return JsonSerializer.Serialize(template with
        {
            ReplayRunId = export.RunId,
            ReplayContentSha256 = export.ReplayContentSha256
        }, options);
    }

    private static async Task RunReplayAsync(ReplayFixture fixture, string output, bool fullEnsemble,
        int? evaluationLimit = 20)
    {
        var repo = FindRepositoryRoot();
        var project = Path.Combine(repo, "tools", "IntelligenceLabReplay",
            "IAApi.IntelligenceLabReplay.csproj");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        var arguments = new List<string> { "run", "--no-build", "--project", project, "--",
            fixture.Training, fixture.Evaluation, output };
        if (evaluationLimit is not null)
            arguments.Add(evaluationLimit.Value.ToString(CultureInfo.InvariantCulture));
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        if (fullEnsemble)
            start.ArgumentList.Add("--full-ensemble");

        using var process = Process.Start(start) ??
            throw new InvalidOperationException("Nao foi possivel iniciar o replay.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw new TimeoutException("Replay nao terminou em tres minutos.");
        }
        var outText = await stdout;
        var errText = await stderr;
        Assert.True(process.ExitCode == 0,
            $"Replay {(fullEnsemble ? "full" : "statistical")} falhou (exit={process.ExitCode})." +
            $"\nSTDOUT:\n{outText}\nSTDERR:\n{errText}");
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IAApi.csproj")) &&
                File.Exists(Path.Combine(dir.FullName, "tools", "IntelligenceLabReplay",
                    "IAApi.IntelligenceLabReplay.csproj")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException("Checkout IAApi nao encontrado para teste do replay.");
    }

    private sealed class ReplayFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "IAApi-M33C-", Guid.NewGuid().ToString("N"));
        public string Training => Path.Combine(Root, "treino.json");
        public string Evaluation => Path.Combine(Root, "avaliacao.json");

        public ReplayFixture(SyntheticReplayDataset? scenario = null)
        {
            Directory.CreateDirectory(Root);
            if (scenario is not null)
            {
                // O replay valida o contrato do Bridge em camelCase (tagName,
                // dataHora, valor). O gerador de cenarios usa a mesma opcao.
                File.WriteAllText(Training, JsonSerializer.Serialize(
                    scenario.Training, ReplayInputJsonOptions));
                File.WriteAllText(Evaluation, JsonSerializer.Serialize(
                    scenario.Evaluation, ReplayInputJsonOptions));
                return;
            }
            var start = new DateTime(2026, 10, 1, 8, 0, 0);
            var values = new[] { 99.4, 99.6, 99.8, 100.0, 100.2, 100.4, 100.6 };
            var training = Enumerable.Range(0, 120).Select(index => new
            {
                dataHora = start.AddMinutes(index).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                tagName = "PK7.TAG1",
                valor = values[index % values.Length],
                unidade = "ºC"
            });
            var evaluation = Enumerable.Range(0, 20).Select(index => new
            {
                dataHora = start.AddMinutes(120 + index).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                tagName = "PK7.TAG1",
                valor = index < 15 ? values[index % values.Length] : 0.0,
                unidade = "ºC"
            });
            File.WriteAllText(Training, JsonSerializer.Serialize(training));
            File.WriteAllText(Evaluation, JsonSerializer.Serialize(evaluation));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
