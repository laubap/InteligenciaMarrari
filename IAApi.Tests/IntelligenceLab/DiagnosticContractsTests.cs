using IAApi.IntelligenceLab.Observability;

namespace IAApi.Tests.IntelligenceLab;

public sealed class DiagnosticContractsTests
{
    [Fact]
    public void DiagnosticContext_NormalizesIdentifiersAndUtcTimestamp()
    {
        var context = DiagnosticContext.Create(
            " correlation-42 ",
            " run-7 ",
            new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.FromHours(-3)),
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal("correlation-42", context.CorrelationId);
        Assert.Equal("run-7", context.RunId);
        Assert.Equal(TimeSpan.Zero, context.ReceivedAtUtc.Offset);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), context.DiagnosticId);
    }

    [Fact]
    public void DetectorDiagnostic_RequiresScoresWhenExecuted()
    {
        Assert.Throws<ArgumentException>(() => new DetectorDiagnostic(
            "Z-Score",
            DetectorDiagnosticStatus.Executed,
            votedAnomaly: false,
            weight: 0.15,
            originalScore: null,
            normalizedScore: null,
            threshold: 3,
            justification: null));
    }

    [Fact]
    public void DetectorDiagnostic_PreservesDistinctOriginalAndNormalizedScores()
    {
        var sourceMetrics = new Dictionary<string, double>
        {
            ["scoreIsolation"] = 0.72
        };

        var diagnostic = new DetectorDiagnostic(
            "Isolation Forest",
            DetectorDiagnosticStatus.Executed,
            votedAnomaly: true,
            weight: 0.25,
            originalScore: 0.72,
            normalizedScore: 81.5,
            threshold: 0.62,
            justification: "Modelo disponível.",
            metrics: sourceMetrics);

        sourceMetrics["scoreIsolation"] = 0.1;

        Assert.Equal(0.72, diagnostic.OriginalScore);
        Assert.Equal(81.5, diagnostic.NormalizedScore);
        Assert.Equal(0.72, diagnostic.Metrics["scoreIsolation"]);
    }

    [Fact]
    public void DetectorDiagnostic_PreventsNonExecutedDetectorFromVoting()
    {
        Assert.Throws<ArgumentException>(() => new DetectorDiagnostic(
            "EWMA",
            DetectorDiagnosticStatus.NotApplicable,
            votedAnomaly: true,
            weight: 0.15,
            originalScore: null,
            normalizedScore: null,
            threshold: null,
            justification: "Histórico insuficiente."));
    }

    [Fact]
    public void EnsembleDiagnostic_PreservesLegacyAndDecisionScales()
    {
        var ensemble = new EnsembleDiagnostic(
            applicable: true,
            legacyAnomaly: true,
            originalScore: 3.25,
            normalizedScore: 65,
            threshold: 60,
            registeredDetectorCount: 7,
            applicableDetectorCount: 7,
            alertCount: 3,
            applicableWeightSum: 1);

        Assert.Equal(3.25, ensemble.OriginalScore);
        Assert.Equal(65, ensemble.NormalizedScore);
    }

    [Fact]
    public void Completeness_RequiresReasonForPartialOrLostDiagnostic()
    {
        Assert.Throws<ArgumentException>(() => new DiagnosticCompleteness(
            DiagnosticCompletenessStatus.Partial));

        var completeness = new DiagnosticCompleteness(
            DiagnosticCompletenessStatus.Lost,
            ["Detectors"],
            "Fila indisponível.");

        Assert.Equal(DiagnosticCompletenessStatus.Lost, completeness.Status);
        Assert.Equal("Detectors", Assert.Single(completeness.MissingSections));
    }

    [Fact]
    public void ReadingDiagnostic_RequiresCoreSectionsWhenComplete()
    {
        var context = DiagnosticContext.Create(
            "correlation-1",
            runId: null,
            DateTimeOffset.UtcNow);

        var metadata = new DiagnosticVersionMetadata(
            "10.0.0",
            "build-1",
            "commit-1",
            "config-1");

        Assert.Throws<ArgumentException>(() => new IntelligenceLabReadingDiagnostic(
            context,
            metadata,
            "PK7",
            "PK7.TAG1",
            DateTimeOffset.UtcNow,
            10,
            DiagnosticCompleteness.Complete,
            new DiagnosticTiming(TimeSpan.Zero)));
    }

    [Fact]
    public void EnsembleDiagnostic_RejectsMoreApplicableThanRegisteredDetectors()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnsembleDiagnostic(
            applicable: true,
            legacyAnomaly: true,
            originalScore: 3.5,
            normalizedScore: 70,
            threshold: 60,
            registeredDetectorCount: 0,
            applicableDetectorCount: 1,
            alertCount: 1,
            applicableWeightSum: 0.2));
    }

    [Fact]
    public void ReadingDiagnostic_CompleteRequiresMatchingDetectorInventory()
    {
        var timestamp = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var context = DiagnosticContext.Create("run-correlation", "run-1", timestamp);
        var version = new DiagnosticVersionMetadata("1", "build", "commit", "configuration");
        var baseline = new BaselineDiagnostic(
            BaselineDiagnosticKind.Global,
            applied: true,
            reason: "Baseline histórico válido.",
            mean: 100,
            standardDeviation: 1,
            deadband: 5,
            deadbandConfidence: 1,
            sampleCount: 30,
            regimeValue: null,
            behaviorCode: null,
            behaviorSnapshotVersion: null);
        var history = new HistoryQualityDiagnostic(
            originalPointCount: 30,
            validPointCount: 30,
            invalidValueCount: 0,
            invalidTimestampCount: 0,
            duplicateTimestampCount: 0,
            outOfOrderTransitionCount: 0,
            medianIntervalMinutes: 1,
            largestGapMinutes: 1,
            criticalGapThresholdMinutes: 5,
            hasCriticalGap: false);
        var ensemble = new EnsembleDiagnostic(
            applicable: true,
            legacyAnomaly: true,
            originalScore: 3.5,
            normalizedScore: 70,
            threshold: 60,
            registeredDetectorCount: 1,
            applicableDetectorCount: 1,
            alertCount: 1,
            applicableWeightSum: 0.2);
        var runtime = new RuntimeDecisionDiagnostic(
            legacyAnomaly: true,
            finalAnomaly: true,
            emitNewEvidence: true,
            usedLegacyFallback: true,
            source: "Legacy",
            reasons: null,
            temporalConfirmation: null);

        IntelligenceLabReadingDiagnostic CreateReading(
            IEnumerable<DetectorDiagnostic> recordedDetectors) => new(
            context,
            version,
            "PK7",
            "PK7.TAG1",
            timestamp,
            0,
            DiagnosticCompleteness.Complete,
            new DiagnosticTiming(TimeSpan.Zero),
            baseline: baseline,
            historyQuality: history,
            detectors: recordedDetectors,
            ensemble: ensemble,
            runtimeDecision: runtime);

        Assert.Throws<ArgumentException>(() => CreateReading([]));

        var executed = new DetectorDiagnostic(
            "Z-Score",
            DetectorDiagnosticStatus.Executed,
            votedAnomaly: true,
            weight: 0.2,
            originalScore: 3.5,
            normalizedScore: 100,
            threshold: 3,
            justification: "Queda abrupta.");

        var complete = CreateReading([executed]);
        Assert.Equal(DiagnosticCompletenessStatus.Complete, complete.Completeness.Status);
        Assert.Single(complete.Detectors);

        var noVote = new DetectorDiagnostic(
            "Z-Score",
            DetectorDiagnosticStatus.Executed,
            votedAnomaly: false,
            weight: 0.2,
            originalScore: 3.5,
            normalizedScore: 100,
            threshold: 3,
            justification: "Sem voto.");
        Assert.Throws<ArgumentException>(() => CreateReading([noVote]));
    }

    [Fact]
    public void DetectorDiagnostic_FailedRequiresSafeJustification()
    {
        Assert.Throws<ArgumentException>(() => new DetectorDiagnostic(
            "EWMA",
            DetectorDiagnosticStatus.Failed,
            votedAnomaly: false,
            weight: 0.15,
            originalScore: null,
            normalizedScore: null,
            threshold: null,
            justification: "  "));

        var failed = new DetectorDiagnostic(
            "EWMA",
            DetectorDiagnosticStatus.Failed,
            votedAnomaly: false,
            weight: 0.15,
            originalScore: null,
            normalizedScore: null,
            threshold: null,
            justification: "  Modelo não disponível.  ");

        Assert.Equal("Modelo não disponível.", failed.Justification);
        Assert.False(failed.VotedAnomaly);
    }

    [Fact]
    public void MultivariateDiagnostic_NotApplicableCannotVoteAnomaly()
    {
        Assert.Throws<ArgumentException>(() => new MultivariateDiagnostic(
            groupName: "BOMBA_PRESSAO_VAZAO",
            applicable: false,
            anomaly: true,
            normalizedScore: null,
            threshold: null,
            mahalanobisSquared: null,
            minuteIndex: 10,
            ageMinutes: 0,
            tags: ["BOMBA.PRESSAO", "BOMBA.VAZAO"],
            reason: "Grupo sem histórico suficiente."));
    }

    [Fact]
    public void TemporalConfirmationDiagnostic_RejectsInvalidDurationsAndCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemporalConfirmationDiagnostic(
            "Pending", -1, 0, 4, 5, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemporalConfirmationDiagnostic(
            "Pending", 1, double.NaN, 4, 5, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemporalConfirmationDiagnostic(
            "Pending", 1, 1, -4, 5, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemporalConfirmationDiagnostic(
            "Pending", 1, 1, 4, -5, false));
        Assert.Throws<ArgumentException>(() => new TemporalConfirmationDiagnostic(
            " ", 1, 1, 4, 5, false));

        var confirmation = new TemporalConfirmationDiagnostic(
            " Confirmed ", 5, 4, 4, 5, false);
        Assert.Equal("Confirmed", confirmation.Status);
        Assert.Equal(5, confirmation.ConsecutivePoints);
    }

    [Fact]
    public void DiagnosticFailure_RequiresStageCodeAndSafeMessage()
    {
        Assert.Throws<ArgumentException>(() => new DiagnosticFailure(
            " ", "DIAG_ERROR", "Mensagem segura", false));
        Assert.Throws<ArgumentException>(() => new DiagnosticFailure(
            "Publisher", " ", "Mensagem segura", false));
        Assert.Throws<ArgumentException>(() => new DiagnosticFailure(
            "Publisher", "DIAG_ERROR", " ", false));

        var failure = new DiagnosticFailure(
            " Publisher ", " DIAG_ERROR ", " Não foi possível publicar. ", false);
        Assert.Equal("Publisher", failure.Stage);
        Assert.Equal("DIAG_ERROR", failure.Code);
        Assert.Equal("Não foi possível publicar.", failure.SafeMessage);
        Assert.False(failure.AffectedOperationalResponse);
    }

    [Fact]
    public void DetectorDiagnostic_ExplicitThresholdScaleDistinguishesOriginalFromNormalized()
    {
        var original = new DetectorDiagnostic(
            "Isolation Forest", DetectorDiagnosticStatus.Executed,
            votedAnomaly: true, weight: 0.25, originalScore: 0.72,
            normalizedScore: 81.5, threshold: 0.62,
            justification: "Modelo disponível.",
            thresholdScale: DetectorThresholdScale.OriginalScore);

        var normalized = new DetectorDiagnostic(
            "Z-Score", DetectorDiagnosticStatus.Executed,
            votedAnomaly: true, weight: 0.15, originalScore: 3.5,
            normalizedScore: 80, threshold: 60,
            justification: "Queda observada.",
            thresholdScale: DetectorThresholdScale.NormalizedScore);

        Assert.Equal(DetectorThresholdScale.OriginalScore, original.ThresholdScale);
        Assert.Equal(0.62, original.Threshold);
        Assert.Equal(DetectorThresholdScale.NormalizedScore, normalized.ThresholdScale);
        Assert.Equal(60, normalized.Threshold);
    }

    [Fact]
    public void DetectorDiagnostic_LegacyThresholdDoesNotAssumeScale()
    {
        var legacy = new DetectorDiagnostic(
            "MAD", DetectorDiagnosticStatus.Executed,
            votedAnomaly: false, weight: 0.15, originalScore: 2,
            normalizedScore: 30, threshold: 3,
            justification: "Limite legado sem metadado de escala.");

        Assert.Equal(3, legacy.Threshold);
        Assert.Equal(DetectorThresholdScale.Unknown, legacy.ThresholdScale);
    }

    [Fact]
    public void DetectorDiagnostic_RejectsContradictoryThresholdMetadata()
    {
        DetectorDiagnostic Create(double? threshold, DetectorThresholdScale scale) => new(
            "EWMA", DetectorDiagnosticStatus.Executed,
            votedAnomaly: false, weight: 0.15, originalScore: 0.2,
            normalizedScore: 20, threshold: threshold,
            justification: "Diagnóstico simulado.", thresholdScale: scale);

        Assert.Throws<ArgumentException>(() =>
            Create(null, DetectorThresholdScale.OriginalScore));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(101, DetectorThresholdScale.NormalizedScore));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(-1, DetectorThresholdScale.NormalizedScore));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(20, (DetectorThresholdScale)99));
    }
}
