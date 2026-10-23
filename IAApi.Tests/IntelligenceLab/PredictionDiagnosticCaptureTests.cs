using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Observability;
using IAApi.Services.AnomalyDetection.Runtime;
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
                    ExecutionStatus = DetectorDiagnosticStatus.Executed
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
