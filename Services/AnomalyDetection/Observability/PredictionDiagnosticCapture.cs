using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;
using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Observability;

/// <summary>
/// Primeiro ponto de captura passiva APOS a finalizacao operacional.
/// Nenhuma informacao e recalculada e nenhum sink e registrado em DI.
/// Um diagnostico nesta fase e sempre explicitamente PARCIAL.
/// </summary>
public static class PredictionDiagnosticCapture
{
    public static void TryCapture(
        IIntelligenceLabDiagnosticCollector? collector,
        DiagnosticVersionMetadata? version,
        AnomaliaRequest request,
        DateTime readingTimestamp,
        BaselineDiagnostic? baseline,
        DetectorResult ensembleResult,
        RuntimeAnomalyDecision runtimeDecision,
        TimeSpan elapsed,
        DateTimeOffset receivedAtUtc)
    {
        if (collector is null || version is null)
        {
            return;
        }

        try
        {
            // Unspecified nao possui fuso definido. Nao inventar UTC.
            if (readingTimestamp.Kind == DateTimeKind.Unspecified ||
                !double.IsFinite(request.Valor) ||
                elapsed < TimeSpan.Zero)
            {
                return;
            }

            var readingUtc = new DateTimeOffset(readingTimestamp).ToUniversalTime();
            var context = DiagnosticContext.CreateNew(
                receivedAtUtc: receivedAtUtc);

            var missingSections = new List<string>
            {
                "Acquisition",
                "HistoryQuality",
                "DetectorThresholdsAndCalculationReferences",
                "RuntimeFunctionalContext",
                "Multivariate",
                "HttpCorrelationAndRunId",
                "StageObservations"
            };

            if (baseline is null)
            {
                missingSections.Add("Baseline");
            }

            var detectors = CaptureDetectors(ensembleResult, missingSections);
            var ensemble = CaptureEnsemble(ensembleResult, missingSections);
            var runtime = new RuntimeDecisionDiagnostic(
                runtimeDecision.LegacyFinalAnomaly,
                runtimeDecision.EmitAnomaly,
                runtimeDecision.EmitNewEvidence,
                runtimeDecision.UsedLegacyFallback,
                runtimeDecision.Source,
                reasons: [], // Motivos textuais ainda nao foram classificados para telemetria segura.
                temporalConfirmation: null);
            missingSections.Add("RuntimeDecisionReasons");

            var diagnostic = new IntelligenceLabReadingDiagnostic(
                context,
                version,
                request.ClienteId,
                request.TagName,
                readingUtc,
                request.Valor,
                new DiagnosticCompleteness(
                    DiagnosticCompletenessStatus.Partial,
                    missingSections,
                    "Captura passiva inicial; secoes ainda nao instrumentadas."),
                new DiagnosticTiming(elapsed),
                baseline: baseline,
                detectors: detectors,
                ensemble: ensemble,
                runtimeDecision: runtime,
                completedAtUtc: DateTimeOffset.UtcNow);

            collector.Begin(context, version).Complete(diagnostic);
        }
        catch (Exception)
        {
            // Fail-open: diagnostico e inteiramente opcional e nao afeta a resposta.
            // Contabilizacao de perdas sera instrumentada em incremento proprio.
        }
    }

    private static IReadOnlyList<DetectorDiagnostic> CaptureDetectors(
        DetectorResult result,
        List<string> missingSections)
    {
        var diagnostics = new List<DetectorDiagnostic>();
        if (result.DetalhesDetectores is null)
        {
            missingSections.Add("Detectors");
            return diagnostics;
        }

        foreach (var detail in result.DetalhesDetectores)
        {
            try
            {
                var executed = detail.ExecutionStatus == DetectorDiagnosticStatus.Executed;
                diagnostics.Add(new DetectorDiagnostic(
                    detail.Nome,
                    detail.ExecutionStatus,
                    executed && detail.VotouAnomalia,
                    detail.Peso,
                    executed ? detail.ScoreOriginal : null,
                    executed ? detail.ScoreNormalizado : null,
                    threshold: null,
                    justification: detail.ExecutionStatus == DetectorDiagnosticStatus.Failed
                        ? "Falha capturada durante a execucao do detector."
                        : null,
                    metrics: executed ? detail.Metricas : null));
            }
            catch (Exception)
            {
                // Nao atribuir falsamente o estado normal a um detector nao capturado.
                if (!missingSections.Contains("Detectors"))
                {
                    missingSections.Add("Detectors");
                }
            }
        }

        return diagnostics;
    }

    private static EnsembleDiagnostic? CaptureEnsemble(
        DetectorResult result,
        List<string> missingSections)
    {
        try
        {
            var metrics = result.Metricas;
            if (metrics is null ||
                !TryCount(metrics, "quantidadeDetectoresRegistrados", out var registered) ||
                !TryCount(metrics, "quantidadeDetectoresAplicaveis", out var applicable) ||
                !TryCount(metrics, "quantidadeAlertas", out var alerts) ||
                !metrics.TryGetValue("limiteEnsemble", out var threshold) ||
                !metrics.TryGetValue("somaPesosAplicaveis", out var weightSum))
            {
                missingSections.Add("Ensemble");
                return null;
            }

            return new EnsembleDiagnostic(
                result.Aplicavel,
                result.EhAnomalia,
                result.Score,
                result.ScoreNormalizado,
                threshold,
                registered,
                applicable,
                alerts,
                weightSum);
        }
        catch (Exception)
        {
            missingSections.Add("Ensemble");
            return null;
        }
    }

    private static bool TryCount(
        IReadOnlyDictionary<string, double> metrics,
        string key,
        out int count)
    {
        count = 0;
        if (!metrics.TryGetValue(key, out var raw) ||
            !double.IsFinite(raw) ||
            raw < 0 || raw > int.MaxValue || raw != Math.Truncate(raw))
        {
            return false;
        }

        count = (int)raw;
        return true;
    }
}
