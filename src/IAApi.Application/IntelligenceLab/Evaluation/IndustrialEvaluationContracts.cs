using System.Text.Json;

namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// Contratos imutáveis do avaliador offline. Eles não pertencem ao pipeline
/// operacional e só aceitam dados sintéticos de ensaio.
/// </summary>
public static class IndustrialEvaluationSchemas
{
    public const string RunV1 = "industrial-evaluation-run/v1";
    public const string EventV1 = "industrial-labeled-event/v1";
    public const string ReadingV1 = "ai-reading-evaluation-result/v1";
    public const string ReportV1 = "industrial-evaluation-report/v1";
}

/// <summary>
/// Identidade declarada da execução. Ela torna verificáveis commit,
/// configuração e modelos usados; hash do dataset, rótulos e ambiente de
/// execução dependerão de um manifesto de avaliação em uma etapa futura.
/// </summary>
public sealed record EvaluationExecutionIdentity(
    string CodeCommit,
    string ApiVersion,
    string AlgorithmConfigurationVersion,
    string ConfigurationFingerprint,
    IReadOnlyList<EvaluationModelReference> Models);

public sealed record EvaluationModelReference(
    string Component,
    string ModelId,
    string ModelVersion,
    DateTimeOffset? TrainedAtUtc);

public sealed record IndustrialEvaluationRun(
    string SchemaVersion,
    string RunId,
    string ScenarioId,
    string TestClientId,
    TimeSpan AlertEpisodeCadence,
    int MinimumEventCoverageReadings,
    EvaluationExecutionIdentity ExecutionIdentity,
    IReadOnlyList<IndustrialLabeledEvent> Events,
    IReadOnlyList<ExplicitNormalInterval> NormalIntervals,
    IReadOnlyList<AiReadingEvaluationResult> Readings);

public sealed record IndustrialLabeledEvent(
    string SchemaVersion,
    string EventId,
    string ScenarioId,
    string TestClientId,
    IReadOnlyList<string> Tags,
    string? GroupName,
    string FaultType,
    string FaultClass,
    IndustrialSeverity Severity,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    DateTimeOffset DetectionWindowOpensAtUtc,
    DateTimeOffset DetectionWindowClosesAtUtc,
    DateTimeOffset LateDetectionClosesAtUtc,
    EventLabelOrigin LabelOrigin,
    string? CorrelationSetId = null);

public sealed record EventLabelOrigin(
    EventLabelOriginKind Kind,
    string Reference,
    string ReviewStatus);

public enum EventLabelOriginKind
{
    Simulator,
    ManualReviewed,
    HistoricalIncident
}

public enum IndustrialSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Normalidade é uma afirmação do dataset, nunca o complemento implícito dos
/// eventos. Somente esses intervalos podem produzir falsos positivos.
/// </summary>
public sealed record ExplicitNormalInterval(
    string IntervalId,
    string ScenarioId,
    string TestClientId,
    IReadOnlyList<string> Tags,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string LabelReference);

public sealed record AiReadingEvaluationResult(
    string SchemaVersion,
    string RunId,
    string ScenarioId,
    string TestClientId,
    string ReadingId,
    string TagName,
    DateTimeOffset TimestampUtc,
    double? EnsembleNormalizedScore,
    bool FinalAlert,
    Guid? DiagnosticId,
    string DecisionSource);

public sealed record AlertEpisode(
    string EpisodeId,
    string TestClientId,
    string TagName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    int AlertReadingCount,
    IReadOnlyList<string> ReadingIds);

public enum EventEvaluationClassification
{
    DetectedOnTime,
    DetectedLate,
    Missed,
    NotEvaluable
}

public enum AlertEpisodeClassification
{
    PrimaryOnTimeDetection,
    LateDetection,
    DuplicateDuringDetectedEvent,
    FalsePositive,
    NonScorable
}

public sealed record EventEvaluationResult(
    string EventId,
    EventEvaluationClassification Classification,
    string? PrimaryEpisodeId,
    TimeSpan? DetectionDelay,
    int ReadingCoverage,
    string Reason);

public sealed record AlertEpisodeEvaluationResult(
    string EpisodeId,
    AlertEpisodeClassification Classification,
    string? AssociatedEventId,
    string Reason);

public sealed record ReadingEvaluationMetrics(
    int TotalReadings,
    int FinalAlertReadings,
    int ReadingsInExplicitNormalIntervals,
    int FinalAlertsInExplicitNormalIntervals,
    int FinalAlertsOutsideExplicitNormalIntervals);

/// <summary>
/// Parâmetros efetivos que definem como o relatório associou alertas e
/// eventos. São derivados exclusivamente do run, sem alterar os dados.
/// </summary>
public sealed record EvaluationMatchingPolicy(
    string PolicyVersion,
    TimeSpan AlertEpisodeCadence,
    int MinimumEventCoverageReadings,
    string EpisodeBoundaryRule,
    string EventAssociationOrdering,
    string NormalityRule,
    string LateDetectionRule,
    string CoverageLimitation);

public sealed record EventEvaluationMetrics(
    int TotalEvents,
    int EvaluableEvents,
    int DetectedOnTime,
    int DetectedLate,
    int Missed,
    int NotEvaluable,
    double? StrictRecall);

public sealed record EpisodeEvaluationMetrics(
    int TotalEpisodes,
    int PrimaryOnTimeDetections,
    int LateDetections,
    int DuplicateEpisodes,
    int FalsePositiveEpisodes,
    int NonScorableEpisodes,
    double? OnTimeEpisodePrecision);

public sealed record IndustrialEvaluationReport(
    string SchemaVersion,
    string RunId,
    string ScenarioId,
    string TestClientId,
    EvaluationExecutionIdentity ExecutionIdentity,
    EvaluationMatchingPolicy MatchingPolicy,
    IReadOnlyList<EventEvaluationResult> Events,
    IReadOnlyList<AlertEpisodeEvaluationResult> Episodes,
    ReadingEvaluationMetrics ReadingMetrics,
    EventEvaluationMetrics EventMetrics,
    EpisodeEvaluationMetrics EpisodeMetrics);

public static class IndustrialEvaluationReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(IndustrialEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, Options);
    }
}
