using System.Text.Json;
using System.Text.Json.Serialization;
using IAApi.IntelligenceLab.Evaluation;
using Xunit;

namespace IAApi.Tests.IntelligenceLab;

public sealed class ReplayModeComparisonServiceTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void MesmoGabaritoEAlertas_MedeDiferencasDeScoreSemInventarFalsosPositivos()
    {
        var fixture = CreateFixture();
        var result = ReplayModeComparisonService.Compare(
            fixture.ExportSix, fixture.LabelsSix, fixture.ExportSeven, fixture.LabelsSeven);

        Assert.Equal(120, result.AlignedReadings);
        Assert.Equal(0, result.AlertDisagreements);
        Assert.Equal("UNVERIFIED", result.ExecutionIdentityStatus);
        Assert.Equal(30, result.SixDetectors.TruePositives);
        Assert.Equal(30, result.SevenDetectors.TruePositives);
        Assert.Equal(90, result.SixDetectors.TrueNegatives);
        Assert.Equal(90, result.SevenDetectors.TrueNegatives);
        Assert.Equal(0, result.SevenDetectors.FalsePositives);
        Assert.Equal(0, result.SevenDetectors.FalseNegatives);
        Assert.Equal(0d, result.SevenDetectors.DetectionDelayMinutes!.Value);
        Assert.Equal(1d, result.SevenDetectors.StrictEventRecall!.Value);
        Assert.Equal(0, result.SevenDetectors.FalsePositiveEpisodes);

        var before = Assert.Single(result.Phases.Where(x => x.Phase == "Before"));
        var during = Assert.Single(result.Phases.Where(x => x.Phase == "During"));
        var after = Assert.Single(result.Phases.Where(x => x.Phase == "After"));
        Assert.Equal(60, before.Readings);
        Assert.Equal(30, during.Readings);
        Assert.Equal(30, after.Readings);
        Assert.Equal(2d, before.MeanScoreSix!.Value);
        Assert.Equal(12d, before.MeanScoreSeven!.Value);
        Assert.Equal(80d, during.MeanScoreSix!.Value);
        Assert.Equal(75d, during.MeanScoreSeven!.Value);
        Assert.Equal(18d, after.MeanScoreSix!.Value);
        Assert.Equal(25d, after.MeanScoreSeven!.Value);
        Assert.Equal(10d, before.MeanDifference!.Value);
        Assert.Equal(-5d, during.MeanDifference!.Value);
        Assert.Equal(7d, after.MeanDifference!.Value);
    }

    [Fact]
    public void DivergenciasDeDecisaoSaoMedidasComoFpFnEAtrasoNoModoSete()
    {
        var fixture = CreateFixture();
        var changed = fixture.Seven with
        {
            Readings = fixture.Seven.Readings.Select((r, i) =>
                i == 10 ? r with { FinalAlert = true } :
                i == 60 ? r with { FinalAlert = false } : r).ToArray()
        };
        var result = ReplayModeComparisonService.Compare(
            fixture.ExportSix, fixture.LabelsSix,
            JsonSerializer.Serialize(changed, Options), fixture.LabelsSeven);

        Assert.Equal(2, result.AlertDisagreements);
        Assert.Equal(29, result.SevenDetectors.TruePositives);
        Assert.Equal(1, result.SevenDetectors.FalseNegatives);
        Assert.Equal(1, result.SevenDetectors.FalsePositives);
        Assert.Equal(89, result.SevenDetectors.TrueNegatives);
        Assert.Equal(1, result.SevenDetectors.FalsePositiveEpisodes);
        Assert.Equal(1d, result.SevenDetectors.DetectionDelayMinutes!.Value);
    }

    [Fact]
    public void RecusaReplaysDesalinhadosMesmoComMesmoNumeroDeLeituras()
    {
        var fixture = CreateFixture();
        var broken = fixture.Seven with
        {
            Readings = fixture.Seven.Readings.Select((r, i) =>
                i == 10 ? r with { TimestampUtc = r.TimestampUtc.AddSeconds(10) } : r).ToArray()
        };
        Assert.Throws<ArgumentException>(() => ReplayModeComparisonService.Compare(
            fixture.ExportSix, fixture.LabelsSix, JsonSerializer.Serialize(broken, Options), fixture.LabelsSeven));
    }

    [Fact]
    public void RecusaGabaritosComNormalidadeDiferenteEntreModos()
    {
        var fixture = CreateFixture();
        var different = fixture.SevenLabels with
        {
            NormalIntervals = fixture.SevenLabels.NormalIntervals.Select(n =>
                n with { LabelReference = "other-truth" }).ToArray()
        };
        Assert.Throws<ArgumentException>(() => ReplayModeComparisonService.Compare(
            fixture.ExportSix, fixture.LabelsSix, fixture.ExportSeven,
            JsonSerializer.Serialize(different, Options)));
    }

    [Fact]
    public void SemNormalidadeExplicitamenteRotulada_NaoContaTNouFP()
    {
        var fixture = CreateFixture();
        var six = fixture.SixLabels with { NormalIntervals = [] };
        var seven = fixture.SevenLabels with { NormalIntervals = [] };
        var result = ReplayModeComparisonService.Compare(fixture.ExportSix,
            JsonSerializer.Serialize(six, Options), fixture.ExportSeven,
            JsonSerializer.Serialize(seven, Options));

        Assert.Equal(90, result.SixDetectors.UnlabeledReadings);
        Assert.Equal(0, result.SixDetectors.TrueNegatives);
        Assert.Equal(0, result.SixDetectors.FalsePositives);
        Assert.Equal(30, result.SixDetectors.TruePositives);
        Assert.Equal(90, Assert.Single(result.Phases.Where(p => p.Phase == "Unlabeled")).Readings);
    }

    private static Fixture CreateFixture()
    {
        var scenario = SyntheticFlowDropScenario.Create();
        var first = scenario.IndependentLabelsTemplate;
        var start = first.NormalIntervals[0].StartsAtUtc;
        const string run6 = "replay-unlabeled-six-actual";
        const string run7 = "replay-unlabeled-seven-actual";
        var readings6 = ReplayReadingEvaluationMapper.Map(run6, first.ScenarioId, first.TestClientId,
            SyntheticFlowDropScenario.TagName, Enumerable.Range(0, 120).Select(i =>
                new ReplayObservedPrediction(start.AddMinutes(i), i is >= 60 and < 90,
                    i < 60 ? 2 : i < 90 ? 80 : 18, Guid.NewGuid(), "SyntheticTest")).ToArray());
        var readings7 = ReplayReadingEvaluationMapper.Map(run7, first.ScenarioId, first.TestClientId,
            SyntheticFlowDropScenario.TagName, Enumerable.Range(0, 120).Select(i =>
                new ReplayObservedPrediction(start.AddMinutes(i), i is >= 60 and < 90,
                    i < 60 ? 12 : i < 90 ? 75 : 25, Guid.NewGuid(), "SyntheticTest")).ToArray());
        var sixExport = Export(run6, new string('a', 64), readings6);
        var sevenExport = Export(run7, new string('b', 64), readings7);
        var sixLabels = first with { ReplayRunId = run6, ReplayContentSha256 = new string('a', 64) };
        var sevenLabels = first with { ReplayRunId = run7, ReplayContentSha256 = new string('b', 64) };
        return new Fixture(sixExport, sevenExport, sixLabels, sevenLabels);
    }

    private static UnlabeledReplayReadingsExport Export(string runId, string sha,
        IReadOnlyList<AiReadingEvaluationResult> readings) => new(
            "industrial-replay-readings-export/v1", runId, SyntheticFlowDropScenario.ReplayScenarioId,
            SyntheticFlowDropScenario.TestClientId, false, false, true, sha, false, readings);

    private sealed record Fixture(
        UnlabeledReplayReadingsExport Six, UnlabeledReplayReadingsExport Seven,
        ReplayIndependentLabels SixLabels, ReplayIndependentLabels SevenLabels)
    {
        public string ExportSix => JsonSerializer.Serialize(Six, Options);
        public string ExportSeven => JsonSerializer.Serialize(Seven, Options);
        public string LabelsSix => JsonSerializer.Serialize(SixLabels, Options);
        public string LabelsSeven => JsonSerializer.Serialize(SevenLabels, Options);
    }
}
