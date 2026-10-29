using System.Globalization;

namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// M4.2A: deterioracao gradual e deterministica de uma variavel de processo.
/// O gabarito e declarado pelo simulador, antes de qualquer replay ou decisao
/// dos detectores. PK7.TAG1 permanece uma limitacao do replay isolado; nao
/// representa uma afirmacao sobre uma tag fisica de cliente.
/// </summary>
public static class SyntheticGradualDriftScenario
{
    public const string ScenarioDefinitionId = "gradual-flow-drift-v1";
    public const string ReplayScenarioId = SyntheticFlowDropScenario.ReplayScenarioId;
    public const string TestClientId = SyntheticFlowDropScenario.TestClientId;
    public const string TagName = SyntheticFlowDropScenario.TagName;
    public const string Unit = SyntheticFlowDropScenario.Unit;
    public const int TrainingCount = 120;
    public const int EvaluationCount = 120;
    public const int NormalBeforeEndExclusiveIndex = 30;
    public const int EventStartIndex = 30;
    public const int DegradationEndExclusiveIndex = 75;
    public const int EventEndExclusiveIndex = 90;
    public const int RecoveryStartIndex = 90;
    public const int RecoveryEndExclusiveIndex = 105;
    public const int NormalAfterStartIndex = 105;
    public const int CadenceSeconds = 60;
    public const string PendingRunId = SyntheticFlowDropScenario.PendingRunId;
    public const string PendingContentSha256 = SyntheticFlowDropScenario.PendingContentSha256;

    private static readonly DateTimeOffset LocalStart =
        new(2026, 10, 2, 8, 0, 0, TimeSpan.FromHours(-3));
    private static readonly float[] NormalValues =
        [49.4f, 49.6f, 49.8f, 50.0f, 50.2f, 50.4f, 50.6f];

    public static SyntheticReplayDataset Create()
    {
        var training = Enumerable.Range(0, TrainingCount)
            .Select(i => Reading(LocalStart.AddMinutes(i), NormalValues[i % NormalValues.Length]))
            .ToArray();

        var evaluationStart = LocalStart.AddMinutes(TrainingCount);
        var evaluation = Enumerable.Range(0, EvaluationCount)
            .Select(i => Reading(evaluationStart.AddMinutes(i), EvaluationValue(i)))
            .ToArray();

        var eventStart = evaluationStart.AddMinutes(EventStartIndex).ToUniversalTime();
        var eventEnd = evaluationStart.AddMinutes(EventEndExclusiveIndex).ToUniversalTime();
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
            [new IndustrialLabeledEvent(
                IndustrialEvaluationSchemas.EventV1,
                "evt-gradual-flow-drift-001",
                ReplayScenarioId,
                TestClientId,
                [TagName],
                null,
                "GradualFlowDeterioration",
                "ProcessVariableDrift",
                IndustrialSeverity.High,
                eventStart,
                eventEnd,
                eventStart,
                eventStart.AddMinutes(30),
                eventEnd,
                new EventLabelOrigin(EventLabelOriginKind.Simulator,
                    "gradual-flow-drift-v1:deterministic-generator", "DeclaredFixture"))],
            [
                new ExplicitNormalInterval(
                    "normal-before-gradual-drift", ReplayScenarioId, TestClientId, [TagName],
                    evaluationStart.ToUniversalTime(), eventStart, ScenarioDefinitionId),
                new ExplicitNormalInterval(
                    "normal-after-gradual-drift-recovery", ReplayScenarioId, TestClientId, [TagName],
                    evaluationStart.AddMinutes(NormalAfterStartIndex).ToUniversalTime(),
                    evaluationStart.AddMinutes(EvaluationCount).ToUniversalTime(),
                    ScenarioDefinitionId)
            ]);

        return new SyntheticReplayDataset(training, evaluation, labels,
            new SyntheticScenarioMetadata(
                ScenarioDefinitionId, ReplayScenarioId, TestClientId, TagName, Unit,
                TrainingCount, EvaluationCount, EventStartIndex, EventEndExclusiveIndex,
                RecoveryStartIndex, RecoveryEndExclusiveIndex, CadenceSeconds,
                "Gradual flow deterioration; recovery readings are intentionally unlabeled, " +
                "with normality declared only before the drift and after recovery."));
    }

    private static float EvaluationValue(int index)
    {
        if (index < NormalBeforeEndExclusiveIndex || index >= NormalAfterStartIndex)
            return NormalValues[index % NormalValues.Length];

        // 45 leituras de queda lenta, de 49.85 para 34.45 L/s.
        if (index < DegradationEndExclusiveIndex)
            return 50.2f - ((index - EventStartIndex + 1) * 0.35f);

        // Estado degradado sustentado, ainda dentro do incidente declarado.
        if (index < EventEndExclusiveIndex)
            return 34.1f;

        // Recuperacao controlada. Ela nao e normalidade implicita nem incidente
        // rotulado: o avaliador deve mantê-la fora das contagens TP/FP/FN/TN.
        return 34.1f + ((index - RecoveryStartIndex + 1) * 1.04f);
    }

    private static SyntheticReplayReading Reading(DateTimeOffset instant, float value) =>
        new(instant.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            TagName, value, Unit);
}
