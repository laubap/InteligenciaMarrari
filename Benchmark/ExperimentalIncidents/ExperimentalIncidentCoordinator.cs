using System.Security.Cryptography;
using System.Text;
using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

namespace IAApi.Benchmark.ExperimentalIncidents;

/// <summary>Pure coordinator. It owns no clock, store, sink or hidden state.</summary>
public sealed class ExperimentalIncidentCoordinator
{
    private readonly StableDeviationIncidentEngine _lifecycle = new();

    public ExperimentalIncidentEvaluation Evaluate(
        ExperimentalIncidentAggregateState? previous,
        ExperimentalIncidentObservation observation)
    {
        var state = previous ?? new(0, -1, null, null, 0, null);
        if (observation.Sequence <= state.LastProcessedSequence)
            return new(state, [], true);

        var events = new List<ExperimentalIncidentEvent>();
        var next = state with { LastProcessedSequence = observation.Sequence };

        if (next.Incident is null && next.PendingRawAlert is null &&
            observation.RegimeEpisodeActive && observation.Detection.FinalAnomaly)
        {
            var correlation = Id("correlation", observation.ExecutionScopeId,
                observation.FastWindow, observation.Tag, observation.MinuteIndex);
            var rawId = $"raw:{correlation}";
            var pending = new PendingRawAlertState(correlation, rawId,
                observation.RegimeEpisodeId, observation.Timestamp,
                observation.MinuteIndex, observation.Detection,
                observation.ActiveBaselineId, observation.Sequence);
            next = next with { PendingRawAlert = pending };
            events.Add(Event(observation, pending, null,
                ExperimentalIncidentEventKind.RawAlert, "FirstLegacyFinalAnomaly",
                StableDeviationIncidentLifecycle.Open.ToString(), rawId));
        }

        if (next.Incident is null && next.PendingRawAlert is not null &&
            observation.ReturnedToActive && !observation.CandidateRegimeConfirmed)
        {
            return new(next with { PendingRawAlert = null }, events);
        }

        if (next.Incident is null && next.PendingRawAlert is not null &&
            observation.CandidateRegimeConfirmed)
        {
            var pending = next.PendingRawAlert;
            var incidentId = $"incident:{pending.CorrelationId}";
            var first = Detection(pending.FirstDetection, pending.ObservedAt,
                pending.ObservedMinuteIndex);
            var current = Detection(observation.Detection, observation.Timestamp,
                observation.MinuteIndex);
            var attempts = next.CandidateAttemptCount + 1;
            var incident = new StableDeviationIncidentState(
                incidentId, observation.FastWindow, observation.Tag,
                observation.RegimeEpisodeId, StableDeviationIncidentLifecycle.Open,
                null, observation.Timestamp, observation.MinuteIndex,
                observation.Timestamp, observation.MinuteIndex, 0,
                observation.ActiveBaselineId, observation.CandidateBaselineId,
                first, current, current.Score > first.Score ? current : first,
                observation.CandidateBaselineReady ? observation.Timestamp : null,
                null, observation.Detection.FinalAnomaly ? 1 : 0,
                0, 0, 0, 0, null, false,
                observation.EwmaOnlyMismatch ? 1 : 0,
                observation.Persistence.Meaning == ExperimentalPersistenceMeaning.MadTemporalConfirmation ? 1 : 0,
                false, false,
                observation.CriticalGap ? StableDeviationDataQualityStatus.CriticalGap :
                    StableDeviationDataQualityStatus.Valid,
                null, null);
            next = next with
            {
                Incident = incident,
                CandidateAttemptCount = attempts,
                LastCandidateId = observation.CandidateId
            };
            events.Add(Event(observation, pending, incident,
                ExperimentalIncidentEventKind.IncidentOpened,
                "CandidateRegimeConfirmed;OpeningNotificationAlreadyDeliveredByLegacy",
                incident.Lifecycle.ToString(), pending.RawAlertId));
            return new(next, events);
        }

        if (next.Incident is null) return new(next, events);

        var attemptChanged = observation.CandidateId is not null &&
            !string.Equals(observation.CandidateId, next.LastCandidateId,
                StringComparison.Ordinal);
        if (attemptChanged)
            next = next with
            {
                CandidateAttemptCount = next.CandidateAttemptCount + 1,
                LastCandidateId = observation.CandidateId
            };

        var lifecycleObservation = ToLifecycleObservation(observation);
        var decision = _lifecycle.Evaluate(next.Incident, lifecycleObservation);
        if (decision.NextState is null) return new(next, events);
        next = next with { Incident = decision.NextState };

        var kind = decision.Action switch
        {
            StableDeviationIncidentAction.EscalateCandidateMismatch =>
                ExperimentalIncidentEventKind.IncidentEscalated,
            StableDeviationIncidentAction.ResolveReturnedToActive or
            StableDeviationIncidentAction.CloseByRegimePromotion =>
                ExperimentalIncidentEventKind.IncidentResolved,
            _ => ExperimentalIncidentEventKind.IncidentUpdated
        };
        if (decision.Action != StableDeviationIncidentAction.None)
            events.Add(Event(observation, next.PendingRawAlert!, decision.NextState,
                kind, string.Join(';', decision.Reasons),
                decision.NextState.Lifecycle.ToString(), next.PendingRawAlert!.RawAlertId));
        return new(next, events);
    }

    public ExperimentalIncidentEvaluation AtExecutionBoundary(
        ExperimentalIncidentAggregateState previous,
        ExperimentalIncidentObservation observation)
    {
        if (previous.Incident is null)
            return new(previous with { PendingRawAlert = null,
                LastProcessedSequence = observation.Sequence }, []);
        var decision = _lifecycle.AtExecutionBoundary(previous.Incident,
            observation.Timestamp, observation.MinuteIndex);
        var next = previous with { Incident = decision.NextState,
            LastProcessedSequence = observation.Sequence };
        var pending = previous.PendingRawAlert ?? new PendingRawAlertState(
            Id("correlation", observation.ExecutionScopeId, observation.FastWindow,
                observation.Tag, previous.Incident.OpenedMinuteIndex), "none",
            observation.RegimeEpisodeId, previous.Incident.OpenedAt,
            previous.Incident.OpenedMinuteIndex,
            FromLifecycle(previous.Incident.FirstDetection),
            previous.Incident.ActiveBaselineId, observation.Sequence);
        return new(next, [Event(observation, pending, decision.NextState,
            ExperimentalIncidentEventKind.IncidentExecutionBoundary,
            "ExecutionBoundaryIsNotResolution", decision.NextState!.Lifecycle.ToString(),
            pending.RawAlertId)]);
    }

    private static StableDeviationIncidentObservation ToLifecycleObservation(
        ExperimentalIncidentObservation o) => new(
        o.FastWindow, o.Tag, o.RegimeEpisodeId, o.Timestamp, o.MinuteIndex,
        Classification(o), [], Detection(o.Detection, o.Timestamp, o.MinuteIndex),
        o.ActiveBaselineId, o.CandidateBaselineId, o.RegimeState,
        o.CandidateBaselineReady,
        o.CandidateMismatch && !o.EwmaOnlyMismatch, o.CandidateMismatch && !o.EwmaOnlyMismatch,
        o.EwmaOnlyMismatch, o.ReturnedToActive, o.Promotion,
        o.OperationalBaselineChanged,
        o.CriticalGap ? StableDeviationDataQualityStatus.CriticalGap :
            StableDeviationDataQualityStatus.Valid,
        new(o.Persistence.Meaning == ExperimentalPersistenceMeaning.MadTemporalConfirmation,
            o.Persistence.Status, o.Persistence.Origin, o.Persistence.ConsecutivePoints,
            o.Persistence.DurationMinutes, o.Persistence.StartedAt,
            o.Persistence.GapInterrupted),
        o.RegimeEpisodeActive, o.CandidateId, o.OutsideActive);

    private static DualBaselineDecisionClassification Classification(
        ExperimentalIncidentObservation o)
    {
        if (o.ReturnedToActive) return DualBaselineDecisionClassification.ResolvedOrReturnedToActive;
        if (o.CandidateRejected) return DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive;
        if (o.CandidateMismatch) return DualBaselineDecisionClassification.CandidateMismatch;
        if (o.CandidateBaselineReady && o.RegimeState == "StableUnresolvedDeviation")
            return DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation;
        return DualBaselineDecisionClassification.NotEligible;
    }

    private static IncidentDetectionSnapshot Detection(
        ExperimentalDetectorSnapshot d, DateTime timestamp, long minute) =>
        new(timestamp, minute, d.FinalAnomaly, d.Score, d.AnomalousDetectors);
    private static ExperimentalDetectorSnapshot FromLifecycle(IncidentDetectionSnapshot d) =>
        new(d.FinalAnomaly, d.Score, d.AnomalousDetectors);

    private static ExperimentalIncidentEvent Event(
        ExperimentalIncidentObservation o, PendingRawAlertState pending,
        StableDeviationIncidentState? incident, ExperimentalIncidentEventKind kind,
        string reason, string lifecycle, string originatingRawAlertId) => new(
        Id("event", pending.CorrelationId, kind, o.Sequence), pending.CorrelationId,
        kind, o.Timestamp, o.MinuteIndex, o.Sequence, o.Tag, o.FastWindow,
        o.RegimeEpisodeId, incident?.IncidentId, o.CandidateId,
        o.ActiveBaselineId, o.CandidateBaselineId, originatingRawAlertId,
        lifecycle, reason, o.Detection, o.Persistence,
        new(kind == ExperimentalIncidentEventKind.IncidentOpened ? pending.ObservedAt : null,
            kind == ExperimentalIncidentEventKind.IncidentOpened ? pending.ObservedMinuteIndex : null,
            kind == ExperimentalIncidentEventKind.IncidentOpened ? pending.FirstDetection : null,
            o.CandidateBaselineReady,
            o.CandidateRejected, o.CandidateMismatch, o.EwmaOnlyMismatch,
            o.ReturnedToActive, o.CriticalGap, o.OutsideActive),
        o.CriticalGap ? StableDeviationDataQualityStatus.CriticalGap :
            StableDeviationDataQualityStatus.Valid,
        false, true);

    private static string Id(string prefix, params object?[] parts)
    {
        var raw = string.Join('|', parts.Select(x => x?.ToString() ?? "null"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..20];
        return $"{prefix}:{hash.ToLowerInvariant()}";
    }
}
