using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Observability;
using IAApi.Services.AnomalyDetection.Runtime;
using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

namespace IAApi.Tests.IntelligenceLab;

public sealed class PredictionDiagnosticCaptureTests
{
    private static readonly DateTime ReadingTime =
        new(2026, 10, 23, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EnabledCollector_ReceivesRealScoresAndPartialDiagnostic()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var oldScore = detector.Score;
        var oldVote = detector.EhAnomalia;

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, decision,
            TimeSpan.FromMilliseconds(12), DateTimeOffset.UtcNow);

        var diagnostic = Assert.Single(buffer.Snapshot());
        Assert.Equal(DiagnosticCompletenessStatus.Partial, diagnostic.Completeness.Status);
        Assert.Contains("Baseline", diagnostic.Completeness.MissingSections);
        Assert.Equal(76.5, diagnostic.Ensemble!.NormalizedScore, 5);
        Assert.Equal(3.825, diagnostic.Ensemble.OriginalScore, 5);
        Assert.Equal(60, diagnostic.Ensemble.Threshold);
        Assert.True(diagnostic.RuntimeDecision!.FinalAnomaly);
        var zscore = Assert.Single(diagnostic.Detectors);
        Assert.Equal(DetectorDiagnosticStatus.Executed, zscore.Status);
        Assert.True(zscore.VotedAnomaly);
        Assert.Equal(oldScore, detector.Score);
        Assert.Equal(oldVote, detector.EhAnomalia);
    }

    [Fact]
    public void AggregationTrace_ExportaContribuicaoSemRecalcularDecisao()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var detail = Assert.Single(detector.DetalhesDetectores);
        detail.ScoreOriginal = detector.Score;
        detail.ScoreNormalizado = detector.ScoreNormalizado;
        detail.AggregationParticipationKnown = true;
        detail.ParticipatedInAggregation = true;
        detail.EffectiveWeight = detail.Peso;
        detail.WeightedNumeratorContribution = detail.ScoreNormalizado * detail.Peso;
        detector.AggregationPolicy = "WeightedMeanAllApplicable";

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(), request, ReadingTime,
            null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        var ensemble = Assert.Single(buffer.Snapshot()).Ensemble!;
        var contribution = Assert.Single(ensemble.Contributions!);
        Assert.Equal("Z-Score", contribution.Detector);
        Assert.Null(contribution.Family);
        Assert.True(contribution.Registered);
        Assert.True(contribution.Executed);
        Assert.True(contribution.Applicable);
        Assert.Equal(true, contribution.ParticipatedInAggregation);
        Assert.Equal(11.475d, contribution.WeightedNumeratorContribution!.Value, precision: 6);
        Assert.Equal(ensemble.ApplicableWeightSum, ensemble.EffectiveWeightSum, precision: 6);
        Assert.Equal(ensemble.NormalizedScore,
            contribution.WeightedNumeratorContribution.Value / ensemble.ApplicableWeightSum,
            precision: 6);
        Assert.Equal("WeightedMeanAllApplicable", ensemble.AggregationPolicy);
    }

    [Fact]
    public void MissingAggregationTrace_IsExplicitlyUnknown()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(), request, ReadingTime,
            null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        var diagnostic = Assert.Single(buffer.Snapshot());
        Assert.Null(diagnostic.Ensemble!.Contributions);
        Assert.Null(diagnostic.Ensemble.AggregationPolicy);
        Assert.Contains("EnsembleAggregation", diagnostic.Completeness.MissingSections);
    }

    [Fact]
    public void InvokedButNotApplicable_RemainsExecutedInAggregationTrace()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var detail = Assert.Single(detector.DetalhesDetectores);
        detail.ExecutionStatus = DetectorDiagnosticStatus.NotApplicable;
        detail.InvocationAttempted = true;
        detail.ExecutionCompleted = true;
        detail.Aplicavel = false;
        detail.VotouAnomalia = false;
        detail.AggregationParticipationKnown = true;
        detail.ParticipatedInAggregation = false;
        detail.EffectiveWeight = 0;
        detail.WeightedNumeratorContribution = 0;
        detector.AggregationPolicy = "WeightedMeanAllApplicable";

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(), request, ReadingTime,
            null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        var contribution = Assert.Single(Assert.Single(buffer.Snapshot()).Ensemble!.Contributions!);
        Assert.Equal(true, contribution.Executed);
        Assert.Equal(true, contribution.ExecutionCompleted);
        Assert.False(contribution.Applicable);
        Assert.Null(contribution.NormalizedScore);
    }

    [Fact]
    public void NotInvokedDetector_RemainsNotExecutedInAggregationTrace()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var detail = Assert.Single(detector.DetalhesDetectores);
        detail.ExecutionStatus = DetectorDiagnosticStatus.NotApplicable;
        detail.InvocationAttempted = false;
        detail.ExecutionCompleted = false;
        detail.Aplicavel = false;
        detail.VotouAnomalia = false;
        detail.AggregationParticipationKnown = true;
        detail.ParticipatedInAggregation = false;
        detail.EffectiveWeight = 0;
        detail.WeightedNumeratorContribution = 0;
        detector.AggregationPolicy = "WeightedMeanAllApplicable";

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(), request, ReadingTime,
            null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        var contribution = Assert.Single(Assert.Single(buffer.Snapshot()).Ensemble!.Contributions!);
        Assert.Equal(false, contribution.Executed);
        Assert.Equal(false, contribution.ExecutionCompleted);
        Assert.False(contribution.Applicable);
        Assert.Null(contribution.NormalizedScore);
    }

    [Fact]
    public void KnownRuntimeReasons_ArePublishedAsSafeCodesWithoutChangingDecision()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var original = decision with
        {
            Reasons = ["RuntimeDecisionDisabled", "LegacyPreserved"]
        };

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, original,
            TimeSpan.Zero, DateTimeOffset.UtcNow);

        var diagnostic = Assert.Single(buffer.Snapshot());
        Assert.NotNull(diagnostic.RuntimeDecision);
        Assert.Equal(original.EmitAnomaly, diagnostic.RuntimeDecision!.FinalAnomaly);
        Assert.Collection(diagnostic.RuntimeDecision.Reasons,
            reason => Assert.Equal("RuntimeDecisionDisabled", reason),
            reason => Assert.Equal("LegacyPreserved", reason));
        Assert.DoesNotContain("RuntimeDecisionReasons", diagnostic.Completeness.MissingSections);
        Assert.Equal(2, original.Reasons.Count);
    }

    [Fact]
    public void DynamicOrUnrecognizedReason_IsNotPublishedAndIsMarkedIncomplete()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var original = decision with
        {
            Reasons = [
                "FreshMultivariateAnomalyAccepted",
                "MultivariateGroup:private-customer-group",
                "unknown secret data"
            ]
        };

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, original,
            TimeSpan.Zero, DateTimeOffset.UtcNow);

        var diagnostic = Assert.Single(buffer.Snapshot());
        Assert.Equal(
            "FreshMultivariateAnomalyAccepted",
            Assert.Single(diagnostic.RuntimeDecision!.Reasons));
        Assert.Contains("RuntimeDecisionReasons", diagnostic.Completeness.MissingSections);
        Assert.DoesNotContain("private-customer-group", string.Join(" ", diagnostic.RuntimeDecision.Reasons));
        Assert.DoesNotContain("unknown secret data", string.Join(" ", diagnostic.RuntimeDecision.Reasons));
        Assert.Equal(3, original.Reasons.Count);
    }

    [Fact]
    public void DisabledBuffer_DoesNotRetainDiagnostics()
    {
        var buffer = new BoundedDiagnosticBuffer(5); // desabilitado por padrao
        var (request, detector, decision) = Arrange();
        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void CollectorException_DoesNotModifyOperationalResults()
    {
        var (request, detector, decision) = Arrange();
        var score = detector.Score;
        var finalVote = decision.EmitAnomaly;

        PredictionDiagnosticCapture.TryCapture(
            new ThrowingCollector(), Version(), request, ReadingTime,
            null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        Assert.Equal(score, detector.Score);
        Assert.Equal(finalVote, decision.EmitAnomaly);
    }

    [Fact]
    public void UnspecifiedTimestamp_IsNotSilentlyConvertedToUtc()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(), request,
            DateTime.SpecifyKind(ReadingTime, DateTimeKind.Unspecified),
            null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void MissingEnsembleMetrics_ProducesPartialDiagnostic_NotFabricatedScore()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        detector.Metricas.Clear();

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, decision, TimeSpan.Zero, DateTimeOffset.UtcNow);

        var diagnostic = Assert.Single(buffer.Snapshot());
        Assert.Null(diagnostic.Ensemble);
        Assert.Contains("Ensemble", diagnostic.Completeness.MissingSections);
    }

    [Fact]
    public void FailedDetector_PreservesFailureStatusWithoutExceptionText()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var detail = Assert.Single(detector.DetalhesDetectores);
        detail.Aplicavel = false;
        detail.VotouAnomalia = false;
        detail.ExecutionStatus = DetectorDiagnosticStatus.Failed;
        detail.Explicacao = "Internal error: sensitive content";

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, decision,
            TimeSpan.Zero, DateTimeOffset.UtcNow);

        var diagnostic = Assert.Single(buffer.Snapshot());
        var failure = Assert.Single(diagnostic.Detectors);
        Assert.Equal(DetectorDiagnosticStatus.Failed, failure.Status);
        Assert.False(failure.VotedAnomaly);
        Assert.DoesNotContain("sensitive", failure.Justification!);
    }

    [Fact]
    public void InvalidDetectorScores_AreMarkedMissingRatherThanReportedNormal()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        detector.DetalhesDetectores[0].ScoreNormalizado = double.NaN;

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, decision,
            TimeSpan.Zero, DateTimeOffset.UtcNow);

        var diagnostic = Assert.Single(buffer.Snapshot());
        Assert.Empty(diagnostic.Detectors);
        Assert.Contains("Detectors", diagnostic.Completeness.MissingSections);
    }

    [Fact]
    public void ShadowResults_AreCapturedWithCausalAgeWithoutLeakingExplanation()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var minute = MultivariateShadowRuntimeCoordinator.CriarMinuteIndex(ReadingTime);
        var shadow = new MultivariateDetectionResult
        {
            Aplicavel = true,
            EhAnomalia = true,
            ClienteId = request.ClienteId,
            GroupName = "TEST-GROUP",
            MinuteIndex = minute - 1,
            ScoreNormalizado = 82,
            LimiteScore = 70,
            MahalanobisSquared = 9.5,
            TagsUtilizadas = ["BOMBA_VAZAO", "BOMBA_PRESSAO"],
            Explicacao = "Sensitive private diagnostic"
        };

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, decision,
            TimeSpan.Zero, DateTimeOffset.UtcNow,
            multivariateShadowResults: [shadow]);

        var diagnostic = Assert.Single(buffer.Snapshot());
        var item = Assert.Single(diagnostic.Multivariate);
        Assert.True(item.Applicable);
        Assert.True(item.Anomaly);
        Assert.NotNull(item.NormalizedScore);
        Assert.NotNull(item.Threshold);
        Assert.NotNull(item.MahalanobisSquared);
        Assert.Equal(82d, item.NormalizedScore.Value, precision: 6);
        Assert.Equal(70d, item.Threshold.Value, precision: 6);
        Assert.Equal(9.5d, item.MahalanobisSquared.Value, precision: 6);
        Assert.Equal(1, item.AgeMinutes);
        Assert.Equal("TEST-GROUP", item.GroupName);
        Assert.Null(item.Reason);
        Assert.DoesNotContain("Multivariate", diagnostic.Completeness.MissingSections);
        Assert.True(decision.EmitAnomaly); // Telemetria nao interfere na decisao.
    }

    [Fact]
    public void MissingShadowCoordinator_IsDistinctFromNoFinalizedVector()
    {
        var (request, detector, decision) = Arrange();
        var missingBuffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var emptyBuffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var version = Version();

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(missingBuffer), version,
            request, ReadingTime, null, detector, decision,
            TimeSpan.Zero, DateTimeOffset.UtcNow);
        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(emptyBuffer), version,
            request, ReadingTime, null, detector, decision,
            TimeSpan.Zero, DateTimeOffset.UtcNow,
            multivariateShadowResults: []);

        var missing = Assert.Single(missingBuffer.Snapshot());
        var empty = Assert.Single(emptyBuffer.Snapshot());
        Assert.Contains("Multivariate", missing.Completeness.MissingSections);
        Assert.DoesNotContain("Multivariate", empty.Completeness.MissingSections);
        Assert.Empty(missing.Multivariate);
        Assert.Empty(empty.Multivariate);
    }

    [Fact]
    public void FutureOrInvalidMultivariateEvidence_IsMarkedMissingWithoutAbortingCapture()
    {
        var buffer = new BoundedDiagnosticBuffer(5, enabled: true);
        var (request, detector, decision) = Arrange();
        var futureMinute = MultivariateShadowRuntimeCoordinator.CriarMinuteIndex(ReadingTime) + 1;
        var invalid = new MultivariateDetectionResult
        {
            Aplicavel = true,
            ClienteId = request.ClienteId,
            GroupName = "TEST-GROUP",
            MinuteIndex = futureMinute,
            ScoreNormalizado = double.NaN,
            LimiteScore = 70,
            MahalanobisSquared = 1
        };

        PredictionDiagnosticCapture.TryCapture(
            new InMemoryDiagnosticCollector(buffer), Version(),
            request, ReadingTime, null, detector, decision,
            TimeSpan.Zero, DateTimeOffset.UtcNow,
            multivariateShadowResults: [invalid]);

        var diagnostic = Assert.Single(buffer.Snapshot());
        Assert.Empty(diagnostic.Multivariate);
        Assert.Contains("Multivariate", diagnostic.Completeness.MissingSections);
        Assert.True(diagnostic.RuntimeDecision!.FinalAnomaly);
    }

    private static DiagnosticVersionMetadata Version() =>
        new("test-api", "test-build", "test-commit", "test-config");

    private static (AnomaliaRequest, DetectorResult, RuntimeAnomalyDecision) Arrange()
    {
        var request = new AnomaliaRequest
        {
            ClienteId = "CLIENTE-TESTE", TagName = "BOMBA_VAZAO",
            Valor = 0, DataHora = ReadingTime
        };
        var detector = new DetectorResult
        {
            Aplicavel = true, EhAnomalia = true,
            Score = 3.825, ScoreNormalizado = 76.5,
            Metricas = new Dictionary<string, double>
            {
                ["limiteEnsemble"] = 60,
                ["quantidadeDetectoresRegistrados"] = 1,
                ["quantidadeDetectoresAplicaveis"] = 1,
                ["quantidadeAlertas"] = 1,
                ["somaPesosAplicaveis"] = 0.15
            },
            DetalhesDetectores =
            [
                new DetectorDebugItem
                {
                    Nome = "Z-Score", Aplicavel = true,
                    VotouAnomalia = true, ScoreOriginal = 5,
                    ScoreNormalizado = 100, Peso = 0.15,
                    ExecutionStatus = DetectorDiagnosticStatus.Executed,
                    InvocationAttempted = true,
                    ExecutionCompleted = true
                }
            ]
        };
        var decision = new RuntimeAnomalyDecision(
            LegacyFinalAnomaly: true,
            EmitAnomaly: true,
            EmitNewEvidence: true,
            UsedLegacyFallback: true,
            Source: "legacy",
            Reasons: []);
        return (request, detector, decision);
    }

    private sealed class ThrowingCollector : IIntelligenceLabDiagnosticCollector
    {
        public IIntelligenceLabDiagnosticSession Begin(
            DiagnosticContext context,
            DiagnosticVersionMetadata version) =>
            throw new InvalidOperationException("Simulated collector failure");
    }
}
