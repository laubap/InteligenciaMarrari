using System.Text.Json;
using IAApi.Benchmark.ExperimentalAnomalyDecisions;
using Xunit;

namespace IAApi.Tests;

public sealed class FunctionalOraclePreservationEvaluatorTests
{
    [Fact]
    public async Task OperationalDetectionPreservaTruth()
    {
        var result =
            await EvaluateAsync(
                legacy: [100],
                operational: [100],
                newEvidence: []);

        Assert.Equal(1, result.DetectorEvaluableTruths);
        Assert.Equal(1, result.FunctionalTruePositives);
        Assert.Equal(0, result.FunctionalFalseNegatives);
        Assert.Equal(1.0, result.FunctionalRecall);
        Assert.Equal(0, result.DelayedTruths);
    }

    [Fact]
    public async Task CandidateMismatchNoNewEvidencePreservaTruthSemEmitirAnomalia()
    {
        var result =
            await EvaluateAsync(
                legacy: [100],
                operational: [],
                newEvidence: [100]);

        Assert.Equal(1, result.FunctionalTruePositives);
        Assert.Equal(0, result.FunctionalFalseNegatives);
        Assert.Equal(0, result.TruthLost);
        Assert.Equal(1.0, result.FunctionalRecall);
    }

    [Fact]
    public async Task ContinuationSemNovaEvidenciaPodePerderTruth()
    {
        var result =
            await EvaluateAsync(
                legacy: [100],
                operational: [],
                newEvidence: []);

        Assert.Equal(1, result.DetectorEvaluableTruths);
        Assert.Equal(0, result.FunctionalTruePositives);
        Assert.Equal(1, result.FunctionalFalseNegatives);
        Assert.Equal(1, result.TruthLost);
    }

    [Fact]
    public async Task EvidenciaFuncionalPosteriorMarcaAtraso()
    {
        var result =
            await EvaluateAsync(
                legacy: [100],
                operational: [103],
                newEvidence: []);

        Assert.Equal(1, result.FunctionalTruePositives);
        Assert.Equal(1, result.DelayedTruths);
        Assert.Equal(3, result.MaxDelayMinutes);
    }

    [Fact]
    public async Task SemLegacyNaoEntraComoDetectorEvaluable()
    {
        var result =
            await EvaluateAsync(
                legacy: [],
                operational: [100],
                newEvidence: [100]);

        Assert.Equal(0, result.DetectorEvaluableTruths);
        Assert.Equal(0, result.FunctionalTruePositives);
        Assert.Equal(0, result.FunctionalFalseNegatives);
    }

    private static async Task<FunctionalOraclePreservationSummary>
        EvaluateAsync(
            long[] legacy,
            long[] operational,
            long[] newEvidence)
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "IAApiTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        var oraclePath =
            Path.Combine(root, "oracle-expanded.json");

        var legacyPath =
            Path.Combine(root, "legacy.jsonl");

        var operationalPath =
            Path.Combine(root, "operational.jsonl");

        var newEvidencePath =
            Path.Combine(root, "new-evidence.jsonl");

        await File.WriteAllTextAsync(
            oraclePath,
            JsonSerializer.Serialize(
                new
                {
                    conditions =
                        new[]
                        {
                            new
                            {
                                number = 1,
                                oracleId = "TEST",
                                title = "Test",
                                tags =
                                    new[]
                                    {
                                        "TEST.TAG"
                                    },
                                expectedStartMinuteIndex = 90,
                                expectedEndMinuteIndex = 110
                            }
                        }
                }));

        await File.WriteAllLinesAsync(
            legacyPath,
            legacy.Select(
                minute =>
                    JsonSerializer.Serialize(
                        new
                        {
                            tagName = "TEST.TAG",
                            minuteIndex = minute,
                            ehAnomalia = true
                        })));

        await File.WriteAllLinesAsync(
            operationalPath,
            operational.Select(
                minute =>
                    JsonSerializer.Serialize(
                        new
                        {
                            tagName = "TEST.TAG",
                            minuteIndex = minute,
                            ehAnomalia = true
                        })));

        await File.WriteAllLinesAsync(
            newEvidencePath,
            newEvidence.Select(
                minute =>
                    JsonSerializer.Serialize(
                        new
                        {
                            tag = "TEST.TAG",
                            minuteIndex = minute,
                            emitNewEvidence = true,
                            sourceDecision = 3,
                            usedLegacyFallback = false
                        })));

        try
        {
            var evaluator =
                new FunctionalOraclePreservationEvaluator();

            return await evaluator.EvaluateAsync(
                oraclePath,
                legacyPath,
                operationalPath,
                newEvidencePath,
                root);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}