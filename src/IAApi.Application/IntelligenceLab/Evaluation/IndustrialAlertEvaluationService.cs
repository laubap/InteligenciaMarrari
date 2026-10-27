namespace IAApi.IntelligenceLab.Evaluation;

/// <summary>
/// Avaliador puro e determinístico. Não executa a IA, não grava no banco e
/// não assume que o complemento dos eventos seja normalidade.
/// </summary>
public sealed class IndustrialAlertEvaluationService
{
    public IndustrialEvaluationReport Evaluate(IndustrialEvaluationRun run)
    {
        IndustrialEvaluationValidator.Validate(run);

        var episodes = AlertEpisodeNormalizer.Normalize(
            run.Readings,
            run.AlertEpisodeCadence);

        var orderedEvents = run.Events
            .OrderBy(x => x.DetectionWindowClosesAtUtc)
            .ThenBy(x => x.StartedAtUtc)
            .ThenBy(x => x.EventId, StringComparer.Ordinal)
            .Select(item => new EventWorkItem(item, Coverage(run.Readings, item)))
            .ToArray();

        var usedEpisodes = new HashSet<string>(StringComparer.Ordinal);

        // Primeira passagem: somente alertas iniciados dentro da janela dão TP.
        foreach (var item in orderedEvents.Where(x => x.IsEvaluable(run.MinimumEventCoverageReadings)))
        {
            var episode = FindEpisode(
                episodes,
                usedEpisodes,
                item.Event,
                startInclusive: item.Event.DetectionWindowOpensAtUtc,
                endInclusive: item.Event.DetectionWindowClosesAtUtc);

            if (episode is not null)
            {
                item.Set(EventEvaluationClassification.DetectedOnTime, episode,
                    "AlertEpisodeStartedInsideAllowedWindow");
                usedEpisodes.Add(episode.EpisodeId);
            }
        }

        // Segunda passagem: atraso é registrado, mas não altera recall estrito.
        foreach (var item in orderedEvents.Where(x => x.IsEvaluable(run.MinimumEventCoverageReadings) &&
                                                      x.Classification is null))
        {
            var episode = FindEpisode(
                episodes,
                usedEpisodes,
                item.Event,
                startInclusive: item.Event.DetectionWindowClosesAtUtc.AddTicks(1),
                endInclusive: item.Event.LateDetectionClosesAtUtc);

            if (episode is not null)
            {
                item.Set(EventEvaluationClassification.DetectedLate, episode,
                    "AlertEpisodeStartedAfterDeadline");
                usedEpisodes.Add(episode.EpisodeId);
            }
            else
            {
                item.Set(EventEvaluationClassification.Missed, null,
                    "NoEligibleAlertEpisodeUntilLateDeadline");
            }
        }

        foreach (var item in orderedEvents.Where(x => !x.IsEvaluable(run.MinimumEventCoverageReadings)))
        {
            item.Set(EventEvaluationClassification.NotEvaluable, null,
                "InsufficientReadingCoverageInAllowedWindow");
        }

        var eventResults = orderedEvents
            .OrderBy(x => x.Event.EventId, StringComparer.Ordinal)
            .Select(x => x.ToResult())
            .ToArray();

        var episodeResults = EvaluateEpisodes(
            episodes,
            orderedEvents,
            run.NormalIntervals);

        return new IndustrialEvaluationReport(
            IndustrialEvaluationSchemas.ReportV1,
            run.RunId,
            run.ScenarioId,
            run.TestClientId,
            run.ExecutionIdentity,
            CreateMatchingPolicy(run),
            eventResults,
            episodeResults,
            CreateReadingMetrics(run.Readings, run.NormalIntervals),
            CreateEventMetrics(eventResults),
            CreateEpisodeMetrics(episodeResults));
    }

    private static AlertEpisode? FindEpisode(
        IReadOnlyList<AlertEpisode> episodes,
        ISet<string> usedEpisodes,
        IndustrialLabeledEvent item,
        DateTimeOffset startInclusive,
        DateTimeOffset endInclusive) =>
        episodes
            .Where(episode => !usedEpisodes.Contains(episode.EpisodeId) &&
                              ScopeMatches(episode, item) &&
                              episode.StartedAtUtc >= startInclusive &&
                              episode.StartedAtUtc <= endInclusive)
            .OrderBy(episode => episode.StartedAtUtc)
            .ThenBy(episode => episode.EpisodeId, StringComparer.Ordinal)
            .FirstOrDefault();

    private static AlertEpisodeEvaluationResult[] EvaluateEpisodes(
        IReadOnlyList<AlertEpisode> episodes,
        IReadOnlyList<EventWorkItem> events,
        IReadOnlyList<ExplicitNormalInterval> normalIntervals)
    {
        var assigned = events
            .Where(x => x.PrimaryEpisode is not null)
            .ToDictionary(x => x.PrimaryEpisode!.EpisodeId, x => x,
                StringComparer.Ordinal);

        return episodes
            .OrderBy(x => x.EpisodeId, StringComparer.Ordinal)
            .Select(episode =>
            {
                if (assigned.TryGetValue(episode.EpisodeId, out var eventItem))
                {
                    return new AlertEpisodeEvaluationResult(
                        episode.EpisodeId,
                        eventItem.Classification == EventEvaluationClassification.DetectedOnTime
                            ? AlertEpisodeClassification.PrimaryOnTimeDetection
                            : AlertEpisodeClassification.LateDetection,
                        eventItem.Event.EventId,
                        eventItem.Reason!);
                }

                var duplicate = events
                    .Where(x => x.PrimaryEpisode is not null &&
                                (x.Classification == EventEvaluationClassification.DetectedOnTime ||
                                 x.Classification == EventEvaluationClassification.DetectedLate) &&
                                ScopeMatches(episode, x.Event) &&
                                episode.StartedAtUtc > x.PrimaryEpisode!.StartedAtUtc &&
                                episode.StartedAtUtc <= x.Event.LateDetectionClosesAtUtc)
                    .OrderBy(x => x.Event.EventId, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (duplicate is not null)
                {
                    return new AlertEpisodeEvaluationResult(
                        episode.EpisodeId,
                        AlertEpisodeClassification.DuplicateDuringDetectedEvent,
                        duplicate.Event.EventId,
                        "AdditionalEpisodeAfterPrimaryDetection");
                }

                var startedBeforeAllowedWindow = events.Any(x =>
                    ScopeMatches(episode, x.Event) &&
                    episode.StartedAtUtc < x.Event.DetectionWindowOpensAtUtc &&
                    episode.EndedAtUtc >= x.Event.DetectionWindowOpensAtUtc);

                if (IsInsideNormalInterval(episode, normalIntervals))
                {
                    return new AlertEpisodeEvaluationResult(
                        episode.EpisodeId,
                        AlertEpisodeClassification.FalsePositive,
                        null,
                        startedBeforeAllowedWindow
                            ? "StartedBeforeAllowedEventWindow"
                            : "StartedInsideExplicitNormalInterval");
                }

                return new AlertEpisodeEvaluationResult(
                    episode.EpisodeId,
                    AlertEpisodeClassification.NonScorable,
                    null,
                    startedBeforeAllowedWindow
                        ? "StartedBeforeAllowedEventWindowWithoutNormalLabel"
                        : "NoExplicitNormalOrEventAssociation");
            })
            .ToArray();
    }

    private static ReadingEvaluationMetrics CreateReadingMetrics(
        IReadOnlyList<AiReadingEvaluationResult> readings,
        IReadOnlyList<ExplicitNormalInterval> normalIntervals)
    {
        var normalReadings = readings.Count(reading => IsInsideNormalInterval(reading, normalIntervals));
        var alertsInNormal = readings.Count(reading => reading.FinalAlert &&
            IsInsideNormalInterval(reading, normalIntervals));

        return new ReadingEvaluationMetrics(
            readings.Count,
            readings.Count(x => x.FinalAlert),
            normalReadings,
            alertsInNormal,
            readings.Count(x => x.FinalAlert) - alertsInNormal);
    }

    private static EventEvaluationMetrics CreateEventMetrics(
        IReadOnlyList<EventEvaluationResult> results)
    {
        var evaluable = results.Count(x => x.Classification != EventEvaluationClassification.NotEvaluable);
        var onTime = results.Count(x => x.Classification == EventEvaluationClassification.DetectedOnTime);
        var late = results.Count(x => x.Classification == EventEvaluationClassification.DetectedLate);
        var missed = results.Count(x => x.Classification == EventEvaluationClassification.Missed);
        var notEvaluable = results.Count - evaluable;

        return new EventEvaluationMetrics(
            results.Count,
            evaluable,
            onTime,
            late,
            missed,
            notEvaluable,
            evaluable == 0 ? null : onTime / (double)evaluable);
    }

    private static EpisodeEvaluationMetrics CreateEpisodeMetrics(
        IReadOnlyList<AlertEpisodeEvaluationResult> results)
    {
        var onTime = results.Count(x => x.Classification == AlertEpisodeClassification.PrimaryOnTimeDetection);
        var late = results.Count(x => x.Classification == AlertEpisodeClassification.LateDetection);
        var duplicates = results.Count(x => x.Classification == AlertEpisodeClassification.DuplicateDuringDetectedEvent);
        var falsePositives = results.Count(x => x.Classification == AlertEpisodeClassification.FalsePositive);
        var nonScorable = results.Count(x => x.Classification == AlertEpisodeClassification.NonScorable);
        var precisionDenominator = onTime + falsePositives;

        return new EpisodeEvaluationMetrics(
            results.Count,
            onTime,
            late,
            duplicates,
            falsePositives,
            nonScorable,
            precisionDenominator == 0 ? null : onTime / (double)precisionDenominator);
    }

    private static int Coverage(
        IReadOnlyList<AiReadingEvaluationResult> readings,
        IndustrialLabeledEvent item) =>
        // Cobertura 3A é somente contagem de leituras na janela. Ela não
        // garante continuidade temporal, cadência observada ou qualidade do sinal.
        readings.Count(reading => item.Tags.Contains(reading.TagName, StringComparer.Ordinal) &&
                                  reading.TimestampUtc >= item.DetectionWindowOpensAtUtc &&
                                  reading.TimestampUtc <= item.DetectionWindowClosesAtUtc);

    private static EvaluationMatchingPolicy CreateMatchingPolicy(
        IndustrialEvaluationRun run) => new(
            "industrial-alert-matching/v1",
            run.AlertEpisodeCadence,
            run.MinimumEventCoverageReadings,
            "PerClientTagFullReadingSequence;NegativeReadingEndsEpisode",
            "DetectionDeadlineThenEventStartThenEventId;OnePrimaryEpisodePerEvent",
            "ExplicitNormalIntervalsOnly;IntervalsAreStartInclusiveEndExclusive",
            "LateDetectionRecordedButExcludedFromStrictRecall",
            "MinimumReadingCountOnly;DoesNotAssessTemporalContinuityOrSignalQuality");

    private static bool ScopeMatches(AlertEpisode episode, IndustrialLabeledEvent item) =>
        string.Equals(episode.TestClientId, item.TestClientId, StringComparison.Ordinal) &&
        item.Tags.Contains(episode.TagName, StringComparer.Ordinal);

    private static bool IsInsideNormalInterval(
        AlertEpisode episode,
        IReadOnlyList<ExplicitNormalInterval> intervals) =>
        intervals.Any(interval => interval.Tags.Contains(episode.TagName, StringComparer.Ordinal) &&
                                 episode.StartedAtUtc >= interval.StartsAtUtc &&
                                 episode.StartedAtUtc < interval.EndsAtUtc);

    private static bool IsInsideNormalInterval(
        AiReadingEvaluationResult reading,
        IReadOnlyList<ExplicitNormalInterval> intervals) =>
        intervals.Any(interval => interval.Tags.Contains(reading.TagName, StringComparer.Ordinal) &&
                                 reading.TimestampUtc >= interval.StartsAtUtc &&
                                 reading.TimestampUtc < interval.EndsAtUtc);

    private sealed class EventWorkItem(IndustrialLabeledEvent item, int coverage)
    {
        public IndustrialLabeledEvent Event { get; } = item;
        public int Coverage { get; } = coverage;
        public EventEvaluationClassification? Classification { get; private set; }
        public AlertEpisode? PrimaryEpisode { get; private set; }
        public string? Reason { get; private set; }

        public bool IsEvaluable(int minimumCoverage) => Coverage >= minimumCoverage;

        public void Set(EventEvaluationClassification classification,
            AlertEpisode? episode, string reason)
        {
            Classification = classification;
            PrimaryEpisode = episode;
            Reason = reason;
        }

        public EventEvaluationResult ToResult() => new(
            Event.EventId,
            Classification ?? EventEvaluationClassification.Missed,
            PrimaryEpisode?.EpisodeId,
            PrimaryEpisode is null ? null : PrimaryEpisode.StartedAtUtc - Event.StartedAtUtc,
            Coverage,
            Reason ?? "NoEvaluationOutcome");
    }
}
