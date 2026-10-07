using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

namespace IAApi.Benchmark.ExperimentalIncidents;

public sealed class ExperimentalIncidentAdapter
{
    public ExperimentalIncidentObservation Adapt(
        string executionScopeId,
        long sequence,
        DualBaselineTraceRecord row,
        DetectorResult original,
        DualBaselineDecisionClassification classification,
        string regimeEpisodeId,
        bool regimeEpisodeActive)
    {
        var anomalous = original.DetalhesDetectores
            .Where(x => x.VotouAnomalia).Select(x => x.Nome).ToArray();
        var persistence = original.DetalhesDetectores.FirstOrDefault(x =>
            x.Nome.Contains("Persist", StringComparison.OrdinalIgnoreCase));
        var temporal = persistence?.TemporalConfirmation;
        var origin = temporal?.UnderlyingEvidenceKind ??
            persistence?.UnderlyingEvidenceKind ?? UnderlyingEvidenceKind.Unknown;
        var role = persistence?.EvidenceRole ?? DetectorEvidenceRole.StatisticalEvidence;
        var meaning = persistence is null
            ? ExperimentalPersistenceMeaning.None
            : role == DetectorEvidenceRole.TemporalConfirmation &&
              origin == UnderlyingEvidenceKind.Mad
                ? ExperimentalPersistenceMeaning.MadTemporalConfirmation
                : origin switch
                {
                    UnderlyingEvidenceKind.DeadbandDeviation =>
                        ExperimentalPersistenceMeaning.LegacyDeadbandStatisticalEvidence,
                    UnderlyingEvidenceKind.StandardDeviationFallback =>
                        ExperimentalPersistenceMeaning.LegacyStandardDeviationFallbackEvidence,
                    _ => ExperimentalPersistenceMeaning.UninterpretedLegacyEvidence
                };

        return new ExperimentalIncidentObservation(
            executionScopeId, row.FastWindowIndex, sequence, row.Timestamp,
            row.MinuteIndex, row.Tag, regimeEpisodeId, regimeEpisodeActive,
            row.RegimeState, row.PolicyAction, row.CandidateId,
            row.ActiveBaselineId, row.CandidateBaselineId,
            row.RegimeState == "CandidateRegime",
            classification == DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive,
            classification == DualBaselineDecisionClassification.CandidateMismatch,
            classification == DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation &&
                row.EwmaCandidate?.Vote == true,
            classification == DualBaselineDecisionClassification.ResolvedOrReturnedToActive,
            row.CandidateCriticalGap || row.PolicyAction == "SuspendForDataQuality",
            row.Promotion, row.OperationalBaselineChanged, row.AgainstActive == "Abnormal",
            row.CandidateBaselineReady,
            new(original.EhAnomalia, original.Score, anomalous),
            new(persistence is not null, role, origin, temporal?.Status, meaning,
                temporal?.ConsecutivePoints ?? 0, temporal?.DurationMinutes ?? 0,
                temporal?.StartedAt, temporal?.InterruptedByGap ?? false));
    }
}
