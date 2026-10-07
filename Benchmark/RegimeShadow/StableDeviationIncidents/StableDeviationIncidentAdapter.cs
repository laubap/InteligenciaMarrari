using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

namespace IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

public sealed class StableDeviationIncidentAdapter
{
    public StableDeviationIncidentObservation Adapt(
        DualBaselineTraceRecord row,
        DetectorResult original,
        DualBaselineDecisionClassification classification,
        IReadOnlyList<string> reasons,
        string regimeEpisodeId,
        bool regimeEpisodeActive)
    {
        var anomalous = original.DetalhesDetectores.Where(x => x.VotouAnomalia)
            .Select(x => x.Nome).ToArray();
        var detection = new IncidentDetectionSnapshot(row.Timestamp, row.MinuteIndex,
            original.EhAnomalia, original.Score, anomalous);
        var persistence = original.DetalhesDetectores.FirstOrDefault(x =>
            x.Nome.Equals("Persistência", StringComparison.OrdinalIgnoreCase));
        var temporal = persistence?.TemporalConfirmation;
        return new StableDeviationIncidentObservation(
            row.FastWindowIndex, row.Tag, regimeEpisodeId, row.Timestamp, row.MinuteIndex,
            classification, reasons, detection, row.ActiveBaselineId, row.CandidateBaselineId,
            row.RegimeState, row.CandidateBaselineReady,
            row.MadCandidate?.Vote == true, row.ZCandidate?.Vote == true,
            classification == DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation &&
                row.EwmaCandidate?.Vote == true,
            classification == DualBaselineDecisionClassification.ResolvedOrReturnedToActive,
            row.Promotion, row.OperationalBaselineChanged,
            row.CandidateCriticalGap || row.PolicyAction == "SuspendForDataQuality"
                ? StableDeviationDataQualityStatus.CriticalGap
                : StableDeviationDataQualityStatus.Valid,
            new IncidentTemporalConfirmationSnapshot(
                temporal?.IsActive == true,
                temporal?.Status,
                temporal?.UnderlyingEvidenceKind ?? persistence?.UnderlyingEvidenceKind,
                temporal?.ConsecutivePoints ?? 0,
                temporal?.DurationMinutes ?? 0,
                temporal?.StartedAt,
                temporal?.InterruptedByGap ?? false),
            regimeEpisodeActive,
            row.CandidateId,
            row.AgainstActive == "Abnormal");
    }
}
