using System.Globalization;

namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// Leitura no formato de entrada do CLI IntelligenceLabReplay. DataHora nao tem
/// offset porque o replay existente interpreta o relogio como UTC-03:00.
/// </summary>
public sealed record SyntheticReplayReading(
    string DataHora,
    string TagName,
    float Valor,
    string Unidade);

/// <summary>
/// Artefatos de entrada e gabarito independente compartilhados por cenarios
/// sinteticos do Intelligence Lab. Nenhum campo contem uma decisao da IA.
/// </summary>
public sealed record SyntheticReplayDataset(
    IReadOnlyList<SyntheticReplayReading> Training,
    IReadOnlyList<SyntheticReplayReading> Evaluation,
    ReplayIndependentLabels IndependentLabelsTemplate,
    SyntheticScenarioMetadata Metadata);

public sealed record SyntheticScenarioMetadata(
    string ScenarioDefinitionId,
    string ReplayScenarioId,
    string TestClientId,
    string TagName,
    string Unit,
    int TrainingCount,
    int EvaluationCount,
    int EventStartIndex,
    int EventEndExclusiveIndex,
    int? RecoveryStartIndex,
    int? RecoveryEndExclusiveIndex,
    int CadenceSeconds,
    string GroundTruthDescription);

/// <summary>
/// M4.1A: verdade-terreno criada independentemente das decisoes da IA.
/// O nome PK7.TAG1 e uma limitacao do replay isolado atual, nao uma afirmacao
/// de que a tag fisica PK7.TAG1 mede vazao; nao usar para cliente real.
/// </summary>
public static class SyntheticFlowDropScenario
{
    public const string ScenarioDefinitionId = "flow-drop-sustained-v1";
    public const string ReplayScenarioId = "unlabeled-historical-replay-pk7";
    public const string TestClientId = "SIM-IL-REPLAY-PK7";
    public const string TagName = "PK7.TAG1";
    public const string Unit = "L/s";
    public const int TrainingCount = 120;
    public const int EvaluationCount = 120;
    public const int EventStartIndex = 60;
    public const int EventEndExclusiveIndex = 90;
    public const int CadenceSeconds = 60;
    public const string PendingRunId = "PENDING_REPLAY_RUN_ID";
    public const string PendingContentSha256 = "PENDING_REPLAY_CONTENT_SHA256";

    private static readonly DateTimeOffset LocalStart =
        new(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(-3));
    private static readonly float[] NormalValues =
        [49.4f, 49.6f, 49.8f, 50.0f, 50.2f, 50.4f, 50.6f];

    public static SyntheticReplayDataset Create()
    {
        // A serie de treinamento nao contem incidente nem dados da avaliacao.
        var training = Enumerable.Range(0, TrainingCount)
            .Select(i => Reading(LocalStart.AddMinutes(i), NormalValues[i % NormalValues.Length]))
            .ToArray();

        var evaluationStart = LocalStart.AddMinutes(TrainingCount);
        var evaluation = Enumerable.Range(0, EvaluationCount)
            .Select(i => Reading(evaluationStart.AddMinutes(i),
                i >= EventStartIndex && i < EventEndExclusiveIndex
                    ? 0f
                    : NormalValues[i % NormalValues.Length]))
            .ToArray();

        // Todos os instantes do gabarito sao UTC. Intervalos seguem [inicio, fim).
        var eventStart = evaluationStart.AddMinutes(EventStartIndex).ToUniversalTime();
        var eventEnd = evaluationStart.AddMinutes(EventEndExclusiveIndex).ToUniversalTime();
        var labeledEvent = new IndustrialLabeledEvent(
            IndustrialEvaluationSchemas.EventV1,
            "evt-flow-drop-001",
            ReplayScenarioId,
            TestClientId,
            [TagName],
            null,
            "SustainedFlowDropToZero",
            "ProcessVariableDeviation",
            IndustrialSeverity.High,
            eventStart,
            eventEnd,
            eventStart,
            eventStart.AddMinutes(10),
            eventEnd,
            new EventLabelOrigin(EventLabelOriginKind.Simulator,
                "flow-drop-sustained-v1:deterministic-generator", "DeclaredFixture"));

        var normals = new ExplicitNormalInterval[]
        {
            new("normal-before-flow-drop", ReplayScenarioId, TestClientId, [TagName],
                evaluationStart.ToUniversalTime(), eventStart, ScenarioDefinitionId),
            new("normal-after-recovery", ReplayScenarioId, TestClientId, [TagName],
                eventEnd, evaluationStart.AddMinutes(EvaluationCount).ToUniversalTime(),
                ScenarioDefinitionId)
        };

        // Os dois campos PENDING sao intencionais. Somente apos o replay M4.1B
        // pode vincular runId e hash da exportacao observada, sem criar rotulos.
        var labels = new ReplayIndependentLabels(
            ReplayLabeledEvaluationRunner.LabelsSchemaV1,
            PendingRunId,
            PendingContentSha256,
            ReplayScenarioId,
            TestClientId,
            CadenceSeconds,
            3,
            new EvaluationExecutionIdentity(
                ReplayLabeledEvaluationRunner.Unverified,
                ReplayLabeledEvaluationRunner.Unverified,
                ReplayLabeledEvaluationRunner.Unverified,
                ReplayLabeledEvaluationRunner.Unverified,
                []),
            [labeledEvent],
            normals);

        return new SyntheticReplayDataset(training, evaluation, labels,
            new SyntheticScenarioMetadata(
                ScenarioDefinitionId, ReplayScenarioId, TestClientId, TagName, Unit,
                TrainingCount, EvaluationCount, EventStartIndex, EventEndExclusiveIndex,
                null, null, CadenceSeconds,
                "Sustained zero-flow incident with normal periods before and after recovery."));
    }

    private static SyntheticReplayReading Reading(DateTimeOffset instant, float value) =>
        new(instant.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            TagName, value, Unit);
}
