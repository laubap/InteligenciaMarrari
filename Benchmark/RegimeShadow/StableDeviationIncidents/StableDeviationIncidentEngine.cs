using IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;

namespace IAApi.Benchmark.RegimeShadow.StableDeviationIncidents;

/// <summary>Motor puro: não possui relógio, store ou dependência de serviços.</summary>
public sealed class StableDeviationIncidentEngine
{
    public StableDeviationIncidentDecision Evaluate(
        StableDeviationIncidentState? previous,
        StableDeviationIncidentObservation observation)
    {
        if (previous is null)
        {
            if (!observation.RegimeEpisodeActive || !observation.Detection.FinalAnomaly)
                return new(null, null, StableDeviationIncidentAction.None,
                    ["NoLegacyAnomalyInActiveRegimeEpisode"], false, null, false);

            var opened = new StableDeviationIncidentState(
                $"incident:{observation.FastWindow}:{observation.Tag}:{observation.MinuteIndex}",
                observation.FastWindow, observation.Tag, observation.RegimeEpisodeId,
                StableDeviationIncidentLifecycle.Open, null,
                observation.Timestamp, observation.MinuteIndex, observation.Timestamp,
                observation.MinuteIndex, 0, observation.ActiveBaselineId,
                observation.CandidateBaselineId, observation.Detection,
                observation.Detection, observation.Detection,
                observation.CandidateBaselineReady ? observation.Timestamp : null,
                null, 1, 0, 0, 0, 0, null, false,
                observation.EwmaOnlyMismatch ? 1 : 0,
                observation.TemporalConfirmation.IsPresent ? 1 : 0,
                false, false, observation.DataQualityStatus, null, null);
            return new(null, opened, StableDeviationIncidentAction.OpenIncident,
                ["FirstLegacyAnomalyInRegimeEpisode", "FirstDetectionPreserved"],
                true, StableDeviationIncidentAlertKind.Opening, false);
        }

        if (previous.Lifecycle is StableDeviationIncidentLifecycle.Resolved or
            StableDeviationIncidentLifecycle.ClosedByRegimePromotion)
            return new(previous, previous, StableDeviationIncidentAction.None,
                ["IncidentAlreadyTerminal"], false, null, false);

        var updated = Touch(previous, observation);
        if (observation.DataQualityStatus == StableDeviationDataQualityStatus.CriticalGap)
        {
            var suspended = updated with
            {
                LifecycleBeforeSuspension = previous.Lifecycle == StableDeviationIncidentLifecycle.SuspendedDataQuality
                    ? previous.LifecycleBeforeSuspension : previous.Lifecycle,
                Lifecycle = StableDeviationIncidentLifecycle.SuspendedDataQuality,
                DataQualityStatus = StableDeviationDataQualityStatus.CriticalGap
            };
            return new(previous, suspended, StableDeviationIncidentAction.SuspendForDataQuality,
                ["CriticalGapDoesNotResolveIncident"], false, null, false);
        }

        if (previous.Lifecycle == StableDeviationIncidentLifecycle.SuspendedDataQuality)
        {
            if (observation.Classification == DualBaselineDecisionClassification.ResolvedOrReturnedToActive)
                return new(previous, updated with { Lifecycle = StableDeviationIncidentLifecycle.SuspendedDataQuality },
                    StableDeviationIncidentAction.UpdateEvidence,
                    ["PostGapReturnCannotBeProvedByIsolatedReading"], false, null, false);

            var restorable = observation.Classification is
                DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation or
                DualBaselineDecisionClassification.CandidateMismatch;
            if (!restorable)
                return new(previous, updated with { Lifecycle = StableDeviationIncidentLifecycle.SuspendedDataQuality },
                    StableDeviationIncidentAction.UpdateEvidence,
                    ["AwaitingCoverageAfterCriticalGap"], false, null, false);

            updated = updated with
            {
                Lifecycle = previous.LifecycleBeforeSuspension ?? StableDeviationIncidentLifecycle.Open,
                LifecycleBeforeSuspension = null,
                DataQualityStatus = StableDeviationDataQualityStatus.Valid
            };
        }

        if (observation.Promotion)
        {
            var closed = updated with { Lifecycle = StableDeviationIncidentLifecycle.ClosedByRegimePromotion,
                WasPromoted = true, ClosureReason = StableDeviationIncidentClosureReason.RegimePromotion };
            return new(previous, closed, StableDeviationIncidentAction.CloseByRegimePromotion,
                ["ExplicitRegimePromotion"], false, null, true);
        }

        if (observation.ReturnedToActive ||
            observation.Classification == DualBaselineDecisionClassification.ResolvedOrReturnedToActive)
        {
            var resolved = updated with { Lifecycle = StableDeviationIncidentLifecycle.Resolved,
                ReturnedToActive = true,
                ClosureReason = StableDeviationIncidentClosureReason.ReturnedToActiveBaseline };
            return new(previous, resolved, StableDeviationIncidentAction.ResolveReturnedToActive,
                ["RestoreActiveRegime", "ActiveBaselineReferencePreserved"], false, null, true);
        }

        if (observation.Classification == DualBaselineDecisionClassification.CandidateRejectedStillOutsideActive)
        {
            var rejected = updated with
            {
                CandidateBaselineId = null,
                CandidateRejectionCount = previous.CandidateRejectionCount + 1,
                LastRejectedCandidateId = observation.CandidateId ?? previous.CandidateBaselineId,
                MismatchSubeventActive = false
            };
            return new(previous, rejected, StableDeviationIncidentAction.RecordCandidateRejected,
                ["CandidateRejected", "IncidentRemainsOpen", "ReturnToActiveNotProved",
                 observation.OutsideActive ? "StillOutsideActiveBaseline" : "ActiveFitAwaitingProof"],
                false, null, false);
        }

        if (observation.Classification == DualBaselineDecisionClassification.CandidateMismatch)
        {
            var firstOfSubevent = !previous.MismatchSubeventActive;
            var escalated = updated with
            {
                Lifecycle = StableDeviationIncidentLifecycle.Escalated,
                CandidateMismatchCount = previous.CandidateMismatchCount + 1,
                EscalationCount = previous.EscalationCount + (firstOfSubevent ? 1 : 0),
                LastMismatch = observation.Detection,
                MismatchSubeventActive = true
            };
            return new(previous, escalated, firstOfSubevent
                    ? StableDeviationIncidentAction.EscalateCandidateMismatch
                    : StableDeviationIncidentAction.UpdateEvidence,
                [observation.MadCandidateMismatch ? "MadAgainstCandidateAbnormal" : "ZAgainstCandidateAbnormal",
                 firstOfSubevent ? "FirstMismatchOfContinuousSubevent" : "ContinuousMismatchSubevent"],
                firstOfSubevent, firstOfSubevent ? StableDeviationIncidentAlertKind.Escalation : null, false);
        }

        if (observation.Classification == DualBaselineDecisionClassification.StableUnresolvedDeviationContinuation)
        {
            var lifecycle = previous.Lifecycle == StableDeviationIncidentLifecycle.Escalated
                ? StableDeviationIncidentLifecycle.Escalated
                : StableDeviationIncidentLifecycle.StableUnresolved;
            var continuation = updated with
            {
                Lifecycle = lifecycle,
                StableUnresolvedAt = previous.StableUnresolvedAt ?? observation.Timestamp,
                CandidateReadyAt = previous.CandidateReadyAt ?? observation.Timestamp,
                ContinuationCount = previous.ContinuationCount + 1,
                MismatchSubeventActive = false
            };
            return new(previous, continuation,
                previous.Lifecycle == StableDeviationIncidentLifecycle.Open
                    ? StableDeviationIncidentAction.MarkStableUnresolved
                    : StableDeviationIncidentAction.RecordContinuation,
                ["DeviationAgainstActiveStillOpen", "CurrentReadingFitsReadyCandidate"],
                false, null, false);
        }

        return new(previous, updated with { EvidenceUpdateCount = previous.EvidenceUpdateCount + 1 },
            previous.Lifecycle == StableDeviationIncidentLifecycle.SuspendedDataQuality
                ? StableDeviationIncidentAction.ResumeAfterDataQuality
                : StableDeviationIncidentAction.UpdateEvidence,
            ["IncidentEvidenceUpdated"], false, null, false);
    }

    public StableDeviationIncidentDecision AtExecutionBoundary(
        StableDeviationIncidentState state, DateTime timestamp, long minuteIndex) =>
        new(state, state with { LastUpdatedAt = timestamp, LastMinuteIndex = minuteIndex,
                LastBoundaryReason = "ExecutionBoundary" },
            StableDeviationIncidentAction.ExecutionBoundary,
            ["FastWindowEndedWithoutSemanticResolution"], false, null, false);

    private static StableDeviationIncidentState Touch(
        StableDeviationIncidentState state, StableDeviationIncidentObservation observation)
    {
        var elapsed = observation.DataQualityStatus == StableDeviationDataQualityStatus.CriticalGap ||
                      state.Lifecycle == StableDeviationIncidentLifecycle.SuspendedDataQuality
            ? 0
            : Math.Max(0, (observation.Timestamp - state.LastUpdatedAt).TotalMinutes);
        var maximum = observation.Detection.Score > state.MaximumEvidence.Score
            ? observation.Detection : state.MaximumEvidence;
        return state with
        {
            LastUpdatedAt = observation.Timestamp,
            LastMinuteIndex = observation.MinuteIndex,
            ObservedDurationMinutes = state.ObservedDurationMinutes + elapsed,
            CandidateBaselineId = observation.CandidateBaselineId ?? state.CandidateBaselineId,
            CandidateReadyAt = state.CandidateReadyAt ??
                (observation.CandidateBaselineReady ? observation.Timestamp : null),
            CurrentEvidence = observation.Detection,
            MaximumEvidence = maximum,
            RawAnomalyRecords = state.RawAnomalyRecords + (observation.Detection.FinalAnomaly ? 1 : 0),
            EwmaOnlyMismatchCount = state.EwmaOnlyMismatchCount + (observation.EwmaOnlyMismatch ? 1 : 0),
            TemporalConfirmationCount = state.TemporalConfirmationCount +
                (observation.TemporalConfirmation.IsPresent ? 1 : 0),
            DataQualityStatus = observation.DataQualityStatus
        };
    }
}
