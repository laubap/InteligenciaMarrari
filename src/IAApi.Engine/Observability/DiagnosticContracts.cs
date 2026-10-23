using System.Collections.ObjectModel;

namespace IAApi.IntelligenceLab.Observability;

public enum DetectorDiagnosticStatus
{
    Executed = 1,
    NotApplicable = 2,
    Disabled = 3,
    Failed = 4
}

public enum BaselineDiagnosticKind
{
    Global = 1,
    OperationalRegime = 2,
    BehaviorMemory = 3
}

public enum DiagnosticCompletenessStatus
{
    Complete = 1,
    Partial = 2,
    Lost = 3
}

public enum DiagnosticPublicationStatus
{
    Pending = 1,
    Published = 2,
    Dropped = 3,
    Failed = 4
}

public enum DiagnosticStage
{
    RequestReceived = 1,
    ContextLoaded = 2,
    HistoryPrepared = 3,
    DetectorsEvaluated = 4,
    RuntimeDecisionResolved = 5,
    Finalized = 6
}

public sealed record DiagnosticVersionMetadata
{
    public DiagnosticVersionMetadata(
        string apiVersion,
        string buildIdentifier,
        string commitIdentifier,
        string algorithmConfigurationVersion)
    {
        ApiVersion = DiagnosticContractGuard.Required(apiVersion, nameof(apiVersion));
        BuildIdentifier = DiagnosticContractGuard.Required(buildIdentifier, nameof(buildIdentifier));
        CommitIdentifier = DiagnosticContractGuard.Required(commitIdentifier, nameof(commitIdentifier));
        AlgorithmConfigurationVersion = DiagnosticContractGuard.Required(
            algorithmConfigurationVersion,
            nameof(algorithmConfigurationVersion));
    }

    public string ApiVersion { get; }

    public string BuildIdentifier { get; }

    public string CommitIdentifier { get; }

    public string AlgorithmConfigurationVersion { get; }
}

public sealed record DiagnosticCompleteness
{
    public DiagnosticCompleteness(
        DiagnosticCompletenessStatus status,
        IEnumerable<string>? missingSections = null,
        string? reason = null)
    {
        var normalizedMissingSections = (missingSections ?? [])
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Select(section => section.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (status == DiagnosticCompletenessStatus.Complete &&
            (normalizedMissingSections.Length > 0 || !string.IsNullOrWhiteSpace(reason)))
        {
            throw new ArgumentException(
                "Um diagnóstico completo não pode informar lacunas ou motivo de incompletude.",
                nameof(missingSections));
        }

        if (status != DiagnosticCompletenessStatus.Complete &&
            normalizedMissingSections.Length == 0 &&
            string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Diagnósticos parciais ou perdidos precisam registrar a lacuna ou o motivo.",
                nameof(reason));
        }

        Status = status;
        MissingSections = Array.AsReadOnly(normalizedMissingSections);
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public DiagnosticCompletenessStatus Status { get; }

    public IReadOnlyList<string> MissingSections { get; }

    public string? Reason { get; }

    public static DiagnosticCompleteness Complete { get; } =
        new(DiagnosticCompletenessStatus.Complete);
}

public sealed record DetectorDiagnostic
{
    public DetectorDiagnostic(
        string name,
        DetectorDiagnosticStatus status,
        bool votedAnomaly,
        double weight,
        double? originalScore,
        double? normalizedScore,
        double? threshold,
        string? justification,
        IReadOnlyDictionary<string, double>? metrics = null)
    {
        Name = DiagnosticContractGuard.Required(name, nameof(name));
        DiagnosticContractGuard.NonNegativeFinite(weight, nameof(weight));
        DiagnosticContractGuard.OptionalFinite(originalScore, nameof(originalScore));
        DiagnosticContractGuard.OptionalFinite(normalizedScore, nameof(normalizedScore));
        DiagnosticContractGuard.OptionalFinite(threshold, nameof(threshold));

        if (normalizedScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(normalizedScore),
                "Score normalizado deve estar entre 0 e 100.");
        }

        if (status == DetectorDiagnosticStatus.Executed &&
            (!originalScore.HasValue || !normalizedScore.HasValue))
        {
            throw new ArgumentException(
                "Um detector executado deve informar os dois scores.",
                nameof(originalScore));
        }

        if (status != DetectorDiagnosticStatus.Executed && votedAnomaly)
        {
            throw new ArgumentException(
                "Somente detectores executados podem votar anomalia.",
                nameof(votedAnomaly));
        }

        if (status == DetectorDiagnosticStatus.Failed &&
            string.IsNullOrWhiteSpace(justification))
        {
            throw new ArgumentException(
                "Um detector com falha precisa informar uma justificativa segura.",
                nameof(justification));
        }

        Name = name.Trim();
        Status = status;
        VotedAnomaly = votedAnomaly;
        Weight = weight;
        OriginalScore = originalScore;
        NormalizedScore = normalizedScore;
        Threshold = threshold;
        Justification = string.IsNullOrWhiteSpace(justification)
            ? null
            : justification.Trim();
        Metrics = DiagnosticContractGuard.ReadOnlyMetrics(metrics);
    }

    public string Name { get; }
    public DetectorDiagnosticStatus Status { get; }
    public bool VotedAnomaly { get; }
    public double Weight { get; }
    public double? OriginalScore { get; }
    public double? NormalizedScore { get; }
    public double? Threshold { get; }
    public string? Justification { get; }
    public IReadOnlyDictionary<string, double> Metrics { get; }
}

public sealed record EnsembleDiagnostic
{
    public EnsembleDiagnostic(
        bool applicable,
        bool legacyAnomaly,
        double originalScore,
        double normalizedScore,
        double threshold,
        int registeredDetectorCount,
        int applicableDetectorCount,
        int alertCount,
        double applicableWeightSum)
    {
        DiagnosticContractGuard.Range(originalScore, 0, 5, nameof(originalScore));
        DiagnosticContractGuard.Range(normalizedScore, 0, 100, nameof(normalizedScore));
        DiagnosticContractGuard.Range(threshold, 0, 100, nameof(threshold));
        DiagnosticContractGuard.NonNegative(registeredDetectorCount, nameof(registeredDetectorCount));
        DiagnosticContractGuard.NonNegative(applicableDetectorCount, nameof(applicableDetectorCount));
        DiagnosticContractGuard.NonNegative(alertCount, nameof(alertCount));
        DiagnosticContractGuard.NonNegativeFinite(applicableWeightSum, nameof(applicableWeightSum));

        if (applicableDetectorCount > registeredDetectorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicableDetectorCount),
                "ApplicableDetectorCount não pode exceder RegisteredDetectorCount.");
        }

        if (alertCount > applicableDetectorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alertCount),
                "AlertCount não pode exceder ApplicableDetectorCount.");
        }

        Applicable = applicable;
        LegacyAnomaly = legacyAnomaly;
        OriginalScore = originalScore;
        NormalizedScore = normalizedScore;
        Threshold = threshold;
        RegisteredDetectorCount = registeredDetectorCount;
        ApplicableDetectorCount = applicableDetectorCount;
        AlertCount = alertCount;
        ApplicableWeightSum = applicableWeightSum;
    }

    public bool Applicable { get; }
    public bool LegacyAnomaly { get; }
    public double OriginalScore { get; }
    public double NormalizedScore { get; }
    public double Threshold { get; }
    public int RegisteredDetectorCount { get; }
    public int ApplicableDetectorCount { get; }
    public int AlertCount { get; }
    public double ApplicableWeightSum { get; }
}

public sealed record BaselineDiagnostic
{
    public BaselineDiagnostic(
        BaselineDiagnosticKind kind,
        bool applied,
        string reason,
        double mean,
        double standardDeviation,
        double deadband,
        double deadbandConfidence,
        int sampleCount,
        double? regimeValue,
        string? behaviorCode,
        int? behaviorSnapshotVersion)
    {
        Reason = DiagnosticContractGuard.Required(reason, nameof(reason));
        DiagnosticContractGuard.Finite(mean, nameof(mean));
        DiagnosticContractGuard.NonNegativeFinite(standardDeviation, nameof(standardDeviation));
        DiagnosticContractGuard.NonNegativeFinite(deadband, nameof(deadband));
        DiagnosticContractGuard.NonNegativeFinite(deadbandConfidence, nameof(deadbandConfidence));
        DiagnosticContractGuard.NonNegative(sampleCount, nameof(sampleCount));
        DiagnosticContractGuard.OptionalFinite(regimeValue, nameof(regimeValue));

        if (behaviorSnapshotVersion is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(behaviorSnapshotVersion));
        }

        Kind = kind;
        Applied = applied;
        Mean = mean;
        StandardDeviation = standardDeviation;
        Deadband = deadband;
        DeadbandConfidence = deadbandConfidence;
        SampleCount = sampleCount;
        RegimeValue = regimeValue;
        BehaviorCode = string.IsNullOrWhiteSpace(behaviorCode) ? null : behaviorCode.Trim();
        BehaviorSnapshotVersion = behaviorSnapshotVersion;
    }

    public BaselineDiagnosticKind Kind { get; }
    public bool Applied { get; }
    public string Reason { get; }
    public double Mean { get; }
    public double StandardDeviation { get; }
    public double Deadband { get; }
    public double DeadbandConfidence { get; }
    public int SampleCount { get; }
    public double? RegimeValue { get; }
    public string? BehaviorCode { get; }
    public int? BehaviorSnapshotVersion { get; }
}

public sealed record HistoryQualityDiagnostic
{
    public HistoryQualityDiagnostic(
        int originalPointCount,
        int validPointCount,
        int invalidValueCount,
        int invalidTimestampCount,
        int duplicateTimestampCount,
        int outOfOrderTransitionCount,
        double medianIntervalMinutes,
        double largestGapMinutes,
        double criticalGapThresholdMinutes,
        bool hasCriticalGap)
    {
        DiagnosticContractGuard.NonNegative(originalPointCount, nameof(originalPointCount));
        DiagnosticContractGuard.NonNegative(validPointCount, nameof(validPointCount));
        DiagnosticContractGuard.NonNegative(invalidValueCount, nameof(invalidValueCount));
        DiagnosticContractGuard.NonNegative(invalidTimestampCount, nameof(invalidTimestampCount));
        DiagnosticContractGuard.NonNegative(duplicateTimestampCount, nameof(duplicateTimestampCount));
        DiagnosticContractGuard.NonNegative(outOfOrderTransitionCount, nameof(outOfOrderTransitionCount));
        DiagnosticContractGuard.NonNegativeFinite(medianIntervalMinutes, nameof(medianIntervalMinutes));
        DiagnosticContractGuard.NonNegativeFinite(largestGapMinutes, nameof(largestGapMinutes));
        DiagnosticContractGuard.NonNegativeFinite(criticalGapThresholdMinutes, nameof(criticalGapThresholdMinutes));

        if (validPointCount > originalPointCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validPointCount));
        }

        OriginalPointCount = originalPointCount;
        ValidPointCount = validPointCount;
        InvalidValueCount = invalidValueCount;
        InvalidTimestampCount = invalidTimestampCount;
        DuplicateTimestampCount = duplicateTimestampCount;
        OutOfOrderTransitionCount = outOfOrderTransitionCount;
        MedianIntervalMinutes = medianIntervalMinutes;
        LargestGapMinutes = largestGapMinutes;
        CriticalGapThresholdMinutes = criticalGapThresholdMinutes;
        HasCriticalGap = hasCriticalGap;
    }

    public int OriginalPointCount { get; }
    public int ValidPointCount { get; }
    public int InvalidValueCount { get; }
    public int InvalidTimestampCount { get; }
    public int DuplicateTimestampCount { get; }
    public int OutOfOrderTransitionCount { get; }
    public double MedianIntervalMinutes { get; }
    public double LargestGapMinutes { get; }
    public double CriticalGapThresholdMinutes { get; }
    public bool HasCriticalGap { get; }
}

public sealed record TemporalConfirmationDiagnostic
{
    public TemporalConfirmationDiagnostic(
        string status,
        int consecutivePoints,
        double durationMinutes,
        double requiredDurationMinutes,
        int requiredPoints,
        bool interruptedByGap)
    {
        Status = DiagnosticContractGuard.Required(status, nameof(status));
        DiagnosticContractGuard.NonNegative(consecutivePoints, nameof(consecutivePoints));
        DiagnosticContractGuard.NonNegativeFinite(durationMinutes, nameof(durationMinutes));
        DiagnosticContractGuard.NonNegativeFinite(requiredDurationMinutes, nameof(requiredDurationMinutes));
        DiagnosticContractGuard.NonNegative(requiredPoints, nameof(requiredPoints));

        ConsecutivePoints = consecutivePoints;
        DurationMinutes = durationMinutes;
        RequiredDurationMinutes = requiredDurationMinutes;
        RequiredPoints = requiredPoints;
        InterruptedByGap = interruptedByGap;
    }

    public string Status { get; }
    public int ConsecutivePoints { get; }
    public double DurationMinutes { get; }
    public double RequiredDurationMinutes { get; }
    public int RequiredPoints { get; }
    public bool InterruptedByGap { get; }
}

public sealed record RuntimeDecisionDiagnostic
{
    public RuntimeDecisionDiagnostic(
        bool legacyAnomaly,
        bool finalAnomaly,
        bool emitNewEvidence,
        bool usedLegacyFallback,
        string source,
        IEnumerable<string>? reasons,
        TemporalConfirmationDiagnostic? temporalConfirmation)
    {
        LegacyAnomaly = legacyAnomaly;
        FinalAnomaly = finalAnomaly;
        EmitNewEvidence = emitNewEvidence;
        UsedLegacyFallback = usedLegacyFallback;
        Source = DiagnosticContractGuard.Required(source, nameof(source));
        Reasons = DiagnosticContractGuard.ReadOnlyStrings(reasons, nameof(reasons));
        TemporalConfirmation = temporalConfirmation;
    }

    public bool LegacyAnomaly { get; }
    public bool FinalAnomaly { get; }
    public bool EmitNewEvidence { get; }
    public bool UsedLegacyFallback { get; }
    public string Source { get; }
    public IReadOnlyList<string> Reasons { get; }
    public TemporalConfirmationDiagnostic? TemporalConfirmation { get; }
}

public sealed record RegimeDiagnostic(
    string? State,
    string? ActiveRegimeId,
    string? CandidateRegimeId,
    bool CandidateBaselineReady,
    bool OperationalBaselineChanged,
    bool Promotion);

public sealed record BehaviorMemoryDiagnostic(
    bool Recognized,
    bool BaselineApplied,
    string? BehaviorCode,
    string? Classification,
    int? SnapshotVersion,
    double? CompatibilityConfidence,
    string? Reason);

public sealed record MultivariateDiagnostic
{
    public MultivariateDiagnostic(
        string groupName,
        bool applicable,
        bool anomaly,
        double? normalizedScore,
        double? threshold,
        double? mahalanobisSquared,
        long minuteIndex,
        int ageMinutes,
        IEnumerable<string>? tags,
        string? reason)
    {
        GroupName = DiagnosticContractGuard.Required(groupName, nameof(groupName));
        DiagnosticContractGuard.OptionalFinite(normalizedScore, nameof(normalizedScore));
        DiagnosticContractGuard.OptionalFinite(threshold, nameof(threshold));
        DiagnosticContractGuard.OptionalFinite(mahalanobisSquared, nameof(mahalanobisSquared));
        if (normalizedScore is < 0 or > 100 || threshold is < 0 or > 100 || mahalanobisSquared < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(normalizedScore));
        }

        if (!applicable && anomaly)
        {
            throw new ArgumentException(
                "Uma análise multivariada não aplicável não pode votar anomalia.",
                nameof(anomaly));
        }

        DiagnosticContractGuard.NonNegative(ageMinutes, nameof(ageMinutes));
        Applicable = applicable;
        Anomaly = anomaly;
        NormalizedScore = normalizedScore;
        Threshold = threshold;
        MahalanobisSquared = mahalanobisSquared;
        MinuteIndex = minuteIndex;
        AgeMinutes = ageMinutes;
        Tags = DiagnosticContractGuard.ReadOnlyStrings(tags, nameof(tags));
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public string GroupName { get; }
    public bool Applicable { get; }
    public bool Anomaly { get; }
    public double? NormalizedScore { get; }
    public double? Threshold { get; }
    public double? MahalanobisSquared { get; }
    public long MinuteIndex { get; }
    public int AgeMinutes { get; }
    public IReadOnlyList<string> Tags { get; }
    public string? Reason { get; }
}

public sealed record DiagnosticTiming
{
    public DiagnosticTiming(
        TimeSpan total,
        IReadOnlyDictionary<string, TimeSpan>? stages = null)
    {
        if (total < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(total));
        }

        var copiedStages = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in stages ?? new Dictionary<string, TimeSpan>())
        {
            if (string.IsNullOrWhiteSpace(stage.Key) || stage.Value < TimeSpan.Zero)
            {
                throw new ArgumentException("Estágios de tempo devem possuir nome e duração não negativa.");
            }

            copiedStages.Add(stage.Key.Trim(), stage.Value);
        }

        Total = total;
        Stages = new ReadOnlyDictionary<string, TimeSpan>(copiedStages);
    }

    public TimeSpan Total { get; }
    public IReadOnlyDictionary<string, TimeSpan> Stages { get; }
}

public sealed record DiagnosticFailure
{
    public DiagnosticFailure(
        string stage,
        string code,
        string safeMessage,
        bool affectedOperationalResponse)
    {
        Stage = DiagnosticContractGuard.Required(stage, nameof(stage));
        Code = DiagnosticContractGuard.Required(code, nameof(code));
        SafeMessage = DiagnosticContractGuard.Required(safeMessage, nameof(safeMessage));
        AffectedOperationalResponse = affectedOperationalResponse;
    }

    public string Stage { get; }
    public string Code { get; }
    public string SafeMessage { get; }
    public bool AffectedOperationalResponse { get; }
}

public sealed record IntelligenceLabReadingDiagnostic
{
    public IntelligenceLabReadingDiagnostic(
        DiagnosticContext context,
        DiagnosticVersionMetadata version,
        string clienteId,
        string tagName,
        DateTimeOffset readingTimestampUtc,
        double readingValue,
        DiagnosticCompleteness completeness,
        DiagnosticTiming timing,
        BaselineDiagnostic? baseline = null,
        HistoryQualityDiagnostic? historyQuality = null,
        IEnumerable<DetectorDiagnostic>? detectors = null,
        EnsembleDiagnostic? ensemble = null,
        RuntimeDecisionDiagnostic? runtimeDecision = null,
        RegimeDiagnostic? regime = null,
        BehaviorMemoryDiagnostic? behaviorMemory = null,
        IEnumerable<MultivariateDiagnostic>? multivariate = null,
        DiagnosticFailure? failure = null,
        DateTimeOffset? completedAtUtc = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        ClienteId = DiagnosticContractGuard.Required(clienteId, nameof(clienteId));
        TagName = DiagnosticContractGuard.Required(tagName, nameof(tagName));
        DiagnosticContractGuard.Finite(readingValue, nameof(readingValue));

        Completeness = completeness ?? throw new ArgumentNullException(nameof(completeness));
        Timing = timing ?? throw new ArgumentNullException(nameof(timing));

        var recordedDetectors = (detectors ?? []).ToArray();

        if (recordedDetectors.Any(detector => detector is null))
        {
            throw new ArgumentException(
                "A coleção de detectores não pode conter elementos nulos.",
                nameof(detectors));
        }

        if (completeness.Status == DiagnosticCompletenessStatus.Complete)
        {
            if (baseline is null || historyQuality is null || ensemble is null || runtimeDecision is null)
            {
                throw new ArgumentException(
                    "Diagnósticos completos exigem baseline, qualidade de histórico, ensemble e decisão runtime.",
                    nameof(completeness));
            }

            if (recordedDetectors.Length != ensemble.RegisteredDetectorCount ||
                recordedDetectors.Count(detector => detector.Status == DetectorDiagnosticStatus.Executed) !=
                    ensemble.ApplicableDetectorCount ||
                recordedDetectors.Count(detector => detector.VotedAnomaly) != ensemble.AlertCount)
            {
                throw new ArgumentException(
                    "Diagnóstico completo não corresponde ao inventário, aos detectores aplicáveis ou aos votos do Ensemble.",
                    nameof(detectors));
            }
        }

        ReadingTimestampUtc = readingTimestampUtc.ToUniversalTime();
        ReadingValue = readingValue;
        Baseline = baseline;
        HistoryQuality = historyQuality;
        Detectors = Array.AsReadOnly(recordedDetectors);
        Ensemble = ensemble;
        RuntimeDecision = runtimeDecision;
        Regime = regime;
        BehaviorMemory = behaviorMemory;
        Multivariate = Array.AsReadOnly((multivariate ?? []).ToArray());
        Failure = failure;
        CompletedAtUtc = completedAtUtc?.ToUniversalTime();
    }

    public DiagnosticContext Context { get; }
    public DiagnosticVersionMetadata Version { get; }
    public string ClienteId { get; }
    public string TagName { get; }
    public DateTimeOffset ReadingTimestampUtc { get; }
    public double ReadingValue { get; }
    public DiagnosticCompleteness Completeness { get; }
    public DiagnosticTiming Timing { get; }
    public BaselineDiagnostic? Baseline { get; }
    public HistoryQualityDiagnostic? HistoryQuality { get; }
    public IReadOnlyList<DetectorDiagnostic> Detectors { get; }
    public EnsembleDiagnostic? Ensemble { get; }
    public RuntimeDecisionDiagnostic? RuntimeDecision { get; }
    public RegimeDiagnostic? Regime { get; }
    public BehaviorMemoryDiagnostic? BehaviorMemory { get; }
    public IReadOnlyList<MultivariateDiagnostic> Multivariate { get; }
    public DiagnosticFailure? Failure { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
}

public sealed record DiagnosticStageObservation
{
    public DiagnosticStageObservation(
        DiagnosticStage stage,
        DateTimeOffset observedAtUtc,
        TimeSpan elapsed,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        Stage = stage;
        ObservedAtUtc = observedAtUtc.ToUniversalTime();
        Elapsed = elapsed;
        Attributes = DiagnosticContractGuard.ReadOnlyAttributes(attributes);
    }

    public DiagnosticStage Stage { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public TimeSpan Elapsed { get; }
    public IReadOnlyDictionary<string, string> Attributes { get; }
}

public sealed record DiagnosticPublicationResult
{
    public DiagnosticPublicationResult(
        Guid diagnosticId,
        DiagnosticPublicationStatus status,
        string? reason = null)
    {
        if (diagnosticId == Guid.Empty)
        {
            throw new ArgumentException("DiagnosticId deve ser diferente de vazio.", nameof(diagnosticId));
        }

        if (status is DiagnosticPublicationStatus.Dropped or DiagnosticPublicationStatus.Failed &&
            string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Publicações descartadas ou com falha precisam registrar o motivo.",
                nameof(reason));
        }

        DiagnosticId = diagnosticId;
        Status = status;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public Guid DiagnosticId { get; }
    public DiagnosticPublicationStatus Status { get; }
    public string? Reason { get; }
}

internal static class DiagnosticContractGuard
{
    public static string Required(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O valor é obrigatório.", paramName);
        }

        return value.Trim();
    }

    public static void Finite(double value, string paramName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(paramName, "O valor deve ser finito.");
        }
    }

    public static void OptionalFinite(double? value, string paramName)
    {
        if (value.HasValue)
        {
            Finite(value.Value, paramName);
        }
    }

    public static void NonNegativeFinite(double value, string paramName)
    {
        Finite(value, paramName);
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    public static void Range(double value, double min, double max, string paramName)
    {
        Finite(value, paramName);
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    public static void NonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    public static IReadOnlyDictionary<string, double> ReadOnlyMetrics(
        IReadOnlyDictionary<string, double>? metrics)
    {
        var copied = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in metrics ?? new Dictionary<string, double>())
        {
            Required(metric.Key, nameof(metrics));
            Finite(metric.Value, nameof(metrics));
            copied.Add(metric.Key.Trim(), metric.Value);
        }

        return new ReadOnlyDictionary<string, double>(copied);
    }

    public static IReadOnlyDictionary<string, string> ReadOnlyAttributes(
        IReadOnlyDictionary<string, string>? attributes)
    {
        var copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in attributes ?? new Dictionary<string, string>())
        {
            copied.Add(
                Required(attribute.Key, nameof(attributes)),
                Required(attribute.Value, nameof(attributes)));
        }

        return new ReadOnlyDictionary<string, string>(copied);
    }

    public static IReadOnlyList<string> ReadOnlyStrings(
        IEnumerable<string>? values,
        string paramName)
    {
        return Array.AsReadOnly((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }
}
