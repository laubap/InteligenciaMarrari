using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Maturity;

/// <summary>
/// Maquina pura e deterministica de maturidade comportamental.
/// </summary>
public sealed class BehaviorProfileTemporalStabilizer
{
    public BehaviorProfileMaturityEvaluation Evaluate(
        BehaviorProfileTemporalState previous,
        BehaviorProfileV2Result raw,
        DateTime timestamp,
        int minuteIndex,
        BehaviorProfileMaturityOptions options)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(options);

        var rawFlip =
            previous.LastRawProfile != TagBehaviorType.Unknown &&
            raw.Type != TagBehaviorType.Unknown &&
            previous.LastRawProfile != raw.Type;

        var totalFlips =
            previous.TotalRawFlipCount +
            (rawFlip ? 1 : 0);

        var recent =
            previous.RecentRawWinners
                .Append(raw.Type)
                .TakeLast(
                    options.RecentWinnerWindowSize)
                .ToArray();

        var recentFlips =
            CountFlips(recent);

        var baseState =
            previous with
            {
                LastRawProfile =
                    raw.Type,

                TotalRawFlipCount =
                    totalFlips,

                RecentRawWinners =
                    recent
            };

        if (!HasFormationEvidence(
                raw,
                options,
                out var insufficiencyReason))
        {
            var lifecycle =
                previous.StableProfile ==
                TagBehaviorType.Unknown
                    ? BehaviorProfileMaturityLifecycle.InsufficientEvidence
                    : BehaviorProfileMaturityLifecycle.Conflicted;

            var next =
                baseState with
                {
                    Lifecycle =
                        lifecycle,

                    CandidateProfile =
                        TagBehaviorType.Unknown,

                    CandidateSince =
                        null,

                    CandidateSinceMinuteIndex =
                        null,

                    CandidateObservationCount =
                        0
                };

            return Result(
                next,
                timestamp,
                recentFlips,
                false,
                insufficiencyReason);
        }

        if (
            previous.StableProfile ==
            raw.Type
            &&
            previous.StableProfile !=
            TagBehaviorType.Unknown)
        {
            var stable =
                baseState with
                {
                    Lifecycle =
                        BehaviorProfileMaturityLifecycle.Stable,

                    StableConfidence =
                        raw.Confidence,

                    CandidateProfile =
                        TagBehaviorType.Unknown,

                    CandidateSince =
                        null,

                    CandidateSinceMinuteIndex =
                        null,

                    CandidateObservationCount =
                        0
                };

            return Result(
                stable,
                timestamp,
                recentFlips,
                false,
                "Perfil estável confirmado novamente pela inferência raw.");
        }

        var sameCandidate =
            previous.CandidateProfile ==
            raw.Type
            &&
            previous.CandidateSince.HasValue;

        var candidateSince =
            sameCandidate
                ? previous.CandidateSince
                : timestamp;

        var candidateSinceMinute =
            sameCandidate
                ? previous.CandidateSinceMinuteIndex
                : minuteIndex;

        var candidateCount =
            sameCandidate
                ? previous.CandidateObservationCount + 1
                : 1;

        var duration =
            candidateSince.HasValue
                ? Math.Max(
                    0,
                    (timestamp -
                     candidateSince.Value)
                    .TotalMinutes)
                : 0;

        var conflict =
            HasCounterContinuousConflict(
                raw,
                options);

        var ordinaryPromotion =
            !conflict
            &&
            candidateCount >=
            options.MinimumCandidateSamples
            &&
            duration >=
            options.MinimumCandidateDurationMinutes
            &&
            raw.Margin >=
            options.MinimumWinnerMargin
            &&
            recentFlips <=
            options.MaximumRecentFlipsForPromotion;

        var extendedConflictPromotion =
            conflict
            &&
            candidateCount >=
            options.ExtendedConflictSamples
            &&
            duration >=
            options.ExtendedConflictDurationMinutes
            &&
            recentFlips <=
            options.MaximumRecentFlipsForPromotion;

        if (
            ordinaryPromotion ||
            extendedConflictPromotion)
        {
            var next =
                baseState with
                {
                    Lifecycle =
                        BehaviorProfileMaturityLifecycle.Stable,

                    StableProfile =
                        raw.Type,

                    StableConfidence =
                        raw.Confidence,

                    CandidateProfile =
                        TagBehaviorType.Unknown,

                    CandidateSince =
                        null,

                    CandidateSinceMinuteIndex =
                        null,

                    CandidateObservationCount =
                        0
                };

            var reason =
                extendedConflictPromotion
                    ? "Conflito estrutural persistiu pelo horizonte estendido; vencedor raw temporalmente dominante promovido."
                    : "Candidato promoveu apos suficiência, margem e persistência temporal.";

            return Result(
                next,
                timestamp,
                recentFlips,
                conflict,
                reason);
        }

        var candidateState =
            baseState with
            {
                Lifecycle =
                    conflict
                        ? BehaviorProfileMaturityLifecycle.Conflicted
                        : BehaviorProfileMaturityLifecycle.Candidate,

                CandidateProfile =
                    raw.Type,

                CandidateSince =
                    candidateSince,

                CandidateSinceMinuteIndex =
                    candidateSinceMinute,

                CandidateObservationCount =
                    candidateCount
            };

        var waitingReason =
            conflict
                ? "Counter e Continuous possuem evidência forte concorrente; aguardando resolução temporal."
                : recentFlips >
                  options.MaximumRecentFlipsForPromotion
                    ? "Candidato bloqueado por flips recentes."
                    : "Candidato aguardando duração e persistência mínimas.";

        return Result(
            candidateState,
            timestamp,
            recentFlips,
            conflict,
            waitingReason);
    }

    private static bool HasFormationEvidence(
        BehaviorProfileV2Result raw,
        BehaviorProfileMaturityOptions options,
        out string reason)
    {
        if (
            !raw.HasSufficientSamples ||
            raw.Type ==
            TagBehaviorType.Unknown)
        {
            reason =
                "Inferência raw insuficiente ou inconclusiva.";

            return false;
        }

        if (
            raw.Features.SampleCount <
            options.MinimumHistorySamples
            ||
            raw.Features.ObservedDurationMinutes <
            options.MinimumObservedDurationMinutes)
        {
            reason =
                "Histórico ainda não atingiu amostras/duração mínimas de maturidade.";

            return false;
        }

        if (
            raw.Features.CriticalGapCount >
            options.MaximumCriticalGapsForPromotion)
        {
            reason =
                "Gap crítico bloqueia formação do perfil estável.";

            return false;
        }

        reason = "";

        return true;
    }

    private static bool HasCounterContinuousConflict(
        BehaviorProfileV2Result raw,
        BehaviorProfileMaturityOptions options)
    {
        var pair =
            (
                raw.Winner ==
                TagBehaviorType.Counter
                &&
                raw.RunnerUp ==
                TagBehaviorType.Continuous
            )
            ||
            (
                raw.Winner ==
                TagBehaviorType.Continuous
                &&
                raw.RunnerUp ==
                TagBehaviorType.Counter
            );

        return pair
               &&
               raw.RunnerUpScore >=
               options.StrongRunnerUpScore
               &&
               raw.Margin <
               options.CounterContinuousConflictMargin;
    }

    private static int CountFlips(
        IReadOnlyList<TagBehaviorType> values)
    {
        var filtered =
            values
                .Where(
                    value =>
                        value !=
                        TagBehaviorType.Unknown)
                .ToArray();

        var count = 0;

        for (
            var index = 1;
            index < filtered.Length;
            index++)
        {
            if (
                filtered[index] !=
                filtered[index - 1])
            {
                count++;
            }
        }

        return count;
    }

    private static BehaviorProfileMaturityEvaluation Result(
        BehaviorProfileTemporalState state,
        DateTime timestamp,
        int recentFlips,
        bool conflict,
        string reason)
    {
        var duration =
            state.CandidateSince.HasValue
                ? Math.Max(
                    0,
                    (timestamp -
                     state.CandidateSince.Value)
                    .TotalMinutes)
                : 0;

        return new BehaviorProfileMaturityEvaluation(
            state,
            new StableBehaviorProfileObservation
            {
                MaturityState =
                    state.Lifecycle,

                StableProfile =
                    state.StableProfile,

                StableConfidence =
                    state.StableConfidence,

                CandidateProfile =
                    state.CandidateProfile,

                CandidateSince =
                    state.CandidateSince,

                CandidateSinceMinuteIndex =
                    state.CandidateSinceMinuteIndex,

                EvidenceDurationMinutes =
                    duration,

                WinnerPersistence =
                    state.CandidateObservationCount,

                FlipCount =
                    state.TotalRawFlipCount,

                RecentFlipCount =
                    recentFlips,

                HasCounterContinuousConflict =
                    conflict,

                Reason =
                    reason
            });
    }
}