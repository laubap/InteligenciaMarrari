using IAApi.Services.AnomalyDetection.Runtime.Incidents.StableDeviation;
using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.Runtime.Incidents.Experimental;

namespace IAApi.Application.Incidents.Experimental.ReadModels;

public interface IIncidentReadModelProjector
{
    IncidentReadModel? Project(IncidentReadModel? previous,
        ExperimentalIncidentEvent incidentEvent);
}

/// <summary>Pure, deterministic projection of the experimental event stream.</summary>
public sealed class IncidentReadModelProjector : IIncidentReadModelProjector
{
    public IncidentReadModel? Project(IncidentReadModel? previous,
        ExperimentalIncidentEvent e)
    {
        if (e.Kind == ExperimentalIncidentEventKind.RawAlert) return previous;
        if (e.IncidentId is null) return previous;
        if (previous is not null && e.Sequence <= previous.LastProcessedSequence)
            return previous;

        var model = previous ?? Open(e);
        if (previous is null) return model;

        var wasSuspended = previous.IsSuspended;
        var isSuspended = e.DataQuality == StableDeviationDataQualityStatus.CriticalGap ||
            e.Lifecycle == StableDeviationIncidentLifecycle.SuspendedDataQuality.ToString();
        var elapsed = isSuspended || wasSuspended
            ? 0
            : Math.Max(0, (e.Timestamp - previous.LastUpdatedAt).TotalMinutes);
        var maximum = e.Detection.Score > previous.MaximumEvidence.Score
            ? e.Detection : previous.MaximumEvidence;
        var candidates = AddDistinct(previous.CandidateIds, e.CandidateId);
        var baselines = AddDistinct(previous.CandidateBaselineIds, e.CandidateBaselineId);
        var milestones = previous.Milestones.ToList();

        if (e.Projection.CandidateBaselineReady && previous.CandidateReadyAt is null)
            AddMilestone(milestones, e, IncidentMilestoneKind.CandidateReady);
        if (e.Lifecycle == StableDeviationIncidentLifecycle.StableUnresolved.ToString() &&
            previous.StableUnresolvedAt is null)
            AddMilestone(milestones, e, IncidentMilestoneKind.StableUnresolved);
        if (e.Projection.CandidateRejected)
            AddMilestone(milestones, e, IncidentMilestoneKind.CandidateRejected);
        if (e.Kind == ExperimentalIncidentEventKind.IncidentEscalated)
        {
            AddMilestone(milestones, e, IncidentMilestoneKind.CandidateMismatchStarted);
            AddMilestone(milestones, e, IncidentMilestoneKind.IncidentEscalated);
        }
        if (isSuspended && !wasSuspended)
            AddMilestone(milestones, e, IncidentMilestoneKind.DataQualitySuspended);
        if (!isSuspended && wasSuspended)
            AddMilestone(milestones, e, IncidentMilestoneKind.DataQualityResumed);
        if (e.Projection.ReturnedToActive)
            AddMilestone(milestones, e, IncidentMilestoneKind.ReturnedToActive);
        if (e.Kind == ExperimentalIncidentEventKind.IncidentResolved)
            AddMilestone(milestones, e, IncidentMilestoneKind.IncidentResolved);
        if (e.Kind == ExperimentalIncidentEventKind.IncidentExecutionBoundary)
            AddMilestone(milestones, e, IncidentMilestoneKind.ExecutionBoundary);

        var resolved = e.Kind == ExperimentalIncidentEventKind.IncidentResolved;
        return previous with
        {
            Lifecycle = e.Lifecycle,
            LastUpdatedAt = e.Timestamp,
            LastUpdatedMinuteIndex = e.MinuteIndex,
            DurationMinutes = Math.Max(0, (e.Timestamp - previous.OpenedAt).TotalMinutes),
            ObservedDurationMinutes = previous.ObservedDurationMinutes + elapsed,
            ActiveBaselineId = e.ActiveBaselineId ?? previous.ActiveBaselineId,
            CandidateBaselineId = e.CandidateBaselineId ?? previous.CandidateBaselineId,
            CurrentCandidateId = e.CandidateId ?? previous.CurrentCandidateId,
            CandidateIds = candidates,
            CandidateBaselineIds = baselines,
            CurrentEvidence = e.Detection,
            MaximumEvidence = maximum,
            CandidateReadyAt = previous.CandidateReadyAt ??
                (e.Projection.CandidateBaselineReady ? e.Timestamp : null),
            StableUnresolvedAt = previous.StableUnresolvedAt ??
                (e.Lifecycle == StableDeviationIncidentLifecycle.StableUnresolved.ToString()
                    ? e.Timestamp : null),
            ContinuationCount = previous.ContinuationCount + (IsContinuation(e) ? 1 : 0),
            CandidateMismatchCount = previous.CandidateMismatchCount +
                (e.Projection.CandidateMismatch ? 1 : 0),
            EscalationCount = previous.EscalationCount +
                (e.Kind == ExperimentalIncidentEventKind.IncidentEscalated ? 1 : 0),
            RejectCandidateCount = previous.RejectCandidateCount +
                (e.Projection.CandidateRejected ? 1 : 0),
            CandidateAttemptCount = candidates.Count,
            EwmaOnlyMismatchCount = previous.EwmaOnlyMismatchCount +
                (e.Projection.EwmaOnlyMismatch ? 1 : 0),
            PersistenceConfirmationCount = previous.PersistenceConfirmationCount +
                (IsConfirmedMadPersistence(e) ? 1 : 0),
            DataQualityStatus = e.DataQuality,
            IsSuspended = isSuspended,
            ReturnedToActive = previous.ReturnedToActive || e.Projection.ReturnedToActive,
            ResolutionAt = resolved ? e.Timestamp : previous.ResolutionAt,
            ClosureReason = resolved ? e.Reason : previous.ClosureReason,
            ExecutionBoundary = previous.ExecutionBoundary ||
                e.Kind == ExperimentalIncidentEventKind.IncidentExecutionBoundary,
            IsOpen = !resolved,
            Milestones = milestones,
            LastProcessedSequence = e.Sequence,
            LastAppliedEventId = e.EventId
        };
    }

    private static IncidentReadModel Open(ExperimentalIncidentEvent e)
    {
        var firstAt = e.Projection.FirstDetectionAt ?? e.Timestamp;
        var firstMinute = e.Projection.FirstDetectionMinuteIndex ?? e.MinuteIndex;
        var firstDetection = e.Projection.FirstDetection ?? e.Detection;
        var milestones = new List<IncidentMilestone>
        {
            new(IncidentMilestoneKind.RawAlertObserved,
                firstAt, firstMinute,
                "FirstLegacyFinalAnomaly", StableDeviationIncidentLifecycle.Open.ToString()),
            new(IncidentMilestoneKind.IncidentOpened, e.Timestamp, e.MinuteIndex,
                e.Reason, e.Lifecycle, e.CandidateId, e.CandidateBaselineId)
        };
        if (e.Projection.CandidateBaselineReady)
            AddMilestone(milestones, e, IncidentMilestoneKind.CandidateReady);
        var candidates = AddDistinct(Array.Empty<string>(), e.CandidateId);
        var baselines = AddDistinct(Array.Empty<string>(), e.CandidateBaselineId);
        return new(e.IncidentId!, e.OriginatingRawAlertId ?? "unknown", e.Tag,
            e.FastWindow, e.Lifecycle, firstAt,
            firstMinute, e.Timestamp, e.MinuteIndex,
            e.Timestamp, e.MinuteIndex,
            Math.Max(0, (e.Timestamp - firstAt).TotalMinutes),
            0, e.ActiveBaselineId, e.CandidateBaselineId,
            e.RegimeEpisodeId ?? "unknown", e.CandidateId, candidates, baselines,
            firstDetection, e.Detection, e.Detection,
            e.Projection.CandidateBaselineReady ? e.Timestamp : null, null,
            0, 0, 0, 0, candidates.Count,
            e.Projection.EwmaOnlyMismatch ? 1 : 0,
            IsConfirmedMadPersistence(e) ? 1 : 0,
            e.DataQuality,
            e.DataQuality == StableDeviationDataQualityStatus.CriticalGap,
            false, null, null, false, true, milestones, e.Sequence, e.EventId);
    }

    private static bool IsContinuation(ExperimentalIncidentEvent e) =>
        e.Reason.Contains("Continuation", StringComparison.OrdinalIgnoreCase) ||
        e.Reason.Contains("CurrentReadingFitsReadyCandidate", StringComparison.OrdinalIgnoreCase);

    private static bool IsConfirmedMadPersistence(ExperimentalIncidentEvent e) =>
        e.Persistence.Meaning == ExperimentalPersistenceMeaning.MadTemporalConfirmation &&
        e.Persistence.Status == TemporalConfirmationStatus.Confirmed;

    private static IReadOnlyList<string> AddDistinct(
        IReadOnlyList<string> values, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || values.Contains(value, StringComparer.Ordinal))
            return values;
        return values.Concat([value]).ToArray();
    }

    private static void AddMilestone(List<IncidentMilestone> milestones,
        ExperimentalIncidentEvent e, IncidentMilestoneKind kind)
    {
        if (milestones.Any(x => x.Kind == kind && x.MinuteIndex == e.MinuteIndex &&
                                x.CandidateId == e.CandidateId)) return;
        milestones.Add(new(kind, e.Timestamp, e.MinuteIndex, e.Reason,
            e.Lifecycle, e.CandidateId, e.CandidateBaselineId));
    }
}
