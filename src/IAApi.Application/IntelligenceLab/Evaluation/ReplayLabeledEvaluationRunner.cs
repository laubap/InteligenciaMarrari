using System.Text.Json;
using System.Text.Json.Serialization;

namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// Envelope do replay 3B.1: somente decisoes observadas, sem rotulos.
/// </summary>
public sealed record UnlabeledReplayReadingsExport(
    string SchemaVersion,
    string RunId,
    string ScenarioId,
    string TestClientId,
    bool DatasetLabeled,
    bool EffectivenessEvaluated,
    bool DecisionEquivalenceVerified,
    string ReplayContentSha256,
    bool ExecutionIdentityComplete,
    IReadOnlyList<AiReadingEvaluationResult> Readings);

/// <summary>
/// Gabarito separado, declarando os eventos sem consultar as decisoes da IA.
/// A identidade UNVERIFIED deixa claro que o replay 3B.1 nao permite ainda
/// reproduzir exatamente a configuracao/modelos do runtime.
/// </summary>
public sealed record ReplayIndependentLabels(
    string SchemaVersion,
    string ReplayRunId,
    string ReplayContentSha256,
    string ScenarioId,
    string TestClientId,
    int AlertEpisodeCadenceSeconds,
    int MinimumEventCoverageReadings,
    EvaluationExecutionIdentity ExecutionIdentity,
    IReadOnlyList<IndustrialLabeledEvent> Events,
    IReadOnlyList<ExplicitNormalInterval> NormalIntervals);

public static class ReplayLabeledEvaluationRunner
{
    public const string LabelsSchemaV1 = "industrial-replay-independent-labels/v1";
    public const string Unverified = "UNVERIFIED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static IndustrialEvaluationReport Evaluate(string replayExportJson, string labelsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayExportJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelsJson);

        var replay = JsonSerializer.Deserialize<UnlabeledReplayReadingsExport>(
            replayExportJson, JsonOptions) ??
            throw new ArgumentException("Exportacao do replay nao foi encontrada.");
        var labels = JsonSerializer.Deserialize<ReplayIndependentLabels>(
            labelsJson, JsonOptions) ??
            throw new ArgumentException("Gabarito independente nao foi encontrado.");

        if (replay.SchemaVersion != "industrial-replay-readings-export/v1" ||
            replay.DatasetLabeled || replay.EffectivenessEvaluated ||
            !replay.DecisionEquivalenceVerified || replay.ExecutionIdentityComplete)
        {
            throw new ArgumentException("O arquivo nao e uma exportacao 3B.1 sem rotulos, com equivalencia confirmada e identidade incompleta.");
        }

        if (labels.SchemaVersion != LabelsSchemaV1 ||
            string.IsNullOrWhiteSpace(replay.RunId) ||
            replay.RunId != labels.ReplayRunId ||
            string.IsNullOrWhiteSpace(replay.ScenarioId) ||
            replay.ScenarioId != labels.ScenarioId ||
            string.IsNullOrWhiteSpace(replay.TestClientId) ||
            replay.TestClientId != labels.TestClientId ||
            string.IsNullOrWhiteSpace(replay.ReplayContentSha256) ||
            replay.ReplayContentSha256.Length != 64 ||
            !replay.ReplayContentSha256.All(Uri.IsHexDigit) ||
            !string.Equals(replay.ReplayContentSha256, labels.ReplayContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Gabarito nao corresponde ao runId, cenario, cliente ou SHA256 do replay.");
        }

        var identity = labels.ExecutionIdentity;
        if (identity is null || identity.CodeCommit != Unverified ||
            identity.ApiVersion != Unverified ||
            identity.AlgorithmConfigurationVersion != Unverified ||
            identity.ConfigurationFingerprint != Unverified ||
            identity.Models is null || identity.Models.Count != 0)
        {
            throw new ArgumentException("Na fase 3B.2, o replay exige identidade declarada UNVERIFIED e sem modelos. Nao inventar commit/configuracao.");
        }

        if (labels.AlertEpisodeCadenceSeconds <= 0 ||
            labels.MinimumEventCoverageReadings < 1 ||
            labels.Events is null || labels.Events.Count == 0 ||
            labels.NormalIntervals is null || replay.Readings is null || replay.Readings.Count == 0)
        {
            throw new ArgumentException("Gabarito ou leituras vazios, ou parametros de matching invalidos.");
        }

        var run = new IndustrialEvaluationRun(
            IndustrialEvaluationSchemas.RunV1,
            replay.RunId,
            replay.ScenarioId,
            replay.TestClientId,
            TimeSpan.FromSeconds(labels.AlertEpisodeCadenceSeconds),
            labels.MinimumEventCoverageReadings,
            identity,
            labels.Events,
            labels.NormalIntervals,
            replay.Readings);

        return new IndustrialAlertEvaluationService().Evaluate(run);
    }
}
