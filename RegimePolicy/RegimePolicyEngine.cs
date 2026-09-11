namespace IAApi.RegimePolicy;

/// <summary>
/// Pure, deterministic regime lifecycle policy.
/// It owns no state and performs no I/O. The returned NextState is the only
/// state that a caller may choose to carry to the next evaluation.
/// </summary>
public sealed class RegimePolicyEngine
{
    public RegimePolicyDecision Evaluate(
        RegimePolicyState previousState,
        ReadingObservation observation,
        RegimeEvidenceSnapshot evidence)
    {
        ArgumentNullException.ThrowIfNull(previousState);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            previousState.ActiveRegimeId);

        if (!observation.IsFinite ||
            !double.IsFinite(observation.Value))
        {
            return Suspend(
                previousState,
                RegimeTransitionReasonCode.InvalidReading,
                "A leitura atual não é finita ou foi marcada como inválida.");
        }

        if (observation.HasCriticalGap ||
            evidence.CandidateStability.HasCriticalGap)
        {
            return Suspend(
                previousState,
                RegimeTransitionReasonCode.CriticalGapDetected,
                "Um gap crítico suspendeu a política e invalidou a continuidade candidata.");
        }

        if (observation.HasFrozenSensor)
{
    return Suspend(
        previousState,
        RegimeTransitionReasonCode.FrozenSensorDetected,
        "O sensor permaneceu sem variação por tempo suficiente para caracterizar congelamento.");
}

        if (previousState.Lifecycle ==
            RegimeLifecycleState.SuspendedByDataQuality)
        {
            var resumed = previousState with
            {
                Lifecycle = RegimeLifecycleState.StableKnownRegime,
                Occurrence = OccurrenceState.None,
                CandidateRegimeId = null,
                TransitionStartedAt = null,
                CandidateStartedAt = null,
                TransitionStartedSequence = null,
                CandidateStartedSequence = null,
                ConsecutiveCandidateObservations = 0,
                LifecycleBeforeSuspension = null
            };

            return Decision(
                previousState,
                resumed,
                RegimePolicyAction.ResumeAfterDataQuality,
                false,
                Reason(
                    RegimeTransitionReasonCode.ValidDataResumed,
                    "Dados válidos foram retomados; nenhuma continuidade anterior foi presumida."));
        }

        return previousState.Lifecycle switch
        {
            RegimeLifecycleState.StableKnownRegime =>
                EvaluateStable(previousState, observation, evidence),

            RegimeLifecycleState.TransitionSuspected =>
                EvaluateTransition(previousState, observation, evidence),

            RegimeLifecycleState.CandidateRegime =>
                EvaluateCandidate(previousState, observation, evidence),

            RegimeLifecycleState.StableUnresolvedDeviation =>
                EvaluateUnresolved(previousState, observation, evidence),

            _ => throw new ArgumentOutOfRangeException(
                nameof(previousState),
                previousState.Lifecycle,
                "Estado de ciclo de vida não suportado.")
        };
    }

    private static RegimePolicyDecision EvaluateStable(
        RegimePolicyState previous,
        ReadingObservation observation,
        RegimeEvidenceSnapshot evidence)
    {
        if (evidence.HasReturnedToActiveRegime ||
            (!evidence.IsOutsideActiveRegime &&
             !evidence.HasMaterialTransitionEdge &&
             !evidence.HasSlowDriftEvidence &&
             !evidence.HasPersistentDeviation &&
             !evidence.HasIncreasingNoiseEvidence))
        {
            var stable = ClearTransient(previous) with
            {
                Occurrence = OccurrenceState.None
            };

            return Decision(
                previous,
                stable,
                RegimePolicyAction.None,
                false,
                Reason(
                    RegimeTransitionReasonCode.NoRelevantEvidence,
                    "Nenhuma evidência relevante de mudança de regime foi observada."));
        }

        if (evidence.HasMaterialTransitionEdge ||
            evidence.HasSlowDriftEvidence)
        {
            var reason = evidence.HasMaterialTransitionEdge
                ? Reason(
                    RegimeTransitionReasonCode.MaterialTransitionObserved,
                    "Uma transição material abriu observação de regime.")
                : Reason(
                    RegimeTransitionReasonCode.SlowDriftObserved,
                    "Um drift lento abriu observação sem confirmar novo plateau.");

            var transition = previous with
            {
                Lifecycle = RegimeLifecycleState.TransitionSuspected,
                Occurrence = evidence.HasSlowDriftEvidence
                    ? OccurrenceState.PersistentDeviation
                    : OccurrenceState.TransitionInProgress,
                TransitionStartedAt = observation.Timestamp,
                TransitionStartedSequence = observation.Sequence,
                CandidateRegimeId = null,
                CandidateStartedAt = null,
                CandidateStartedSequence = null,
                ConsecutiveCandidateObservations = 0
            };

            return Decision(
                previous,
                transition,
                RegimePolicyAction.ObserveTransition,
                true,
                reason);
        }

        if (evidence.HasIncreasingNoiseEvidence)
        {
            return Decision(
                previous,
                previous with
                {
                    Occurrence =
                        OccurrenceState.PersistentDeviation
                },
                RegimePolicyAction.None,
                false,
                Reason(
                    RegimeTransitionReasonCode.IncreasingNoiseObserved,
                    "A variabilidade aumentou de forma sustentada sem evidência de mudança do centro do regime ativo."));
        }

        var occurrence = evidence.HasPersistentDeviation
            ? OccurrenceState.PersistentDeviation
            : OccurrenceState.TransientDeviation;

        return Decision(
            previous,
            previous with { Occurrence = occurrence },
            RegimePolicyAction.None,
            false,
            Reason(
                evidence.HasPersistentDeviation
                    ? RegimeTransitionReasonCode.PersistentDeviationObserved
                    : RegimeTransitionReasonCode.NoRelevantEvidence,
                evidence.HasPersistentDeviation
                    ? "O desvio persiste, mas não existe evidência estrutural para abrir candidato."
                    : "O desvio atual é tratado como transitório."));
    }

    private static RegimePolicyDecision EvaluateTransition(
        RegimePolicyState previous,
        ReadingObservation observation,
        RegimeEvidenceSnapshot evidence)
    {
        if (evidence.HasReturnedToActiveRegime)
        {
            return RestoreActive(previous, OccurrenceState.TransientDeviation);
        }

        if (evidence.CandidateInvalidated)
        {
            return Reject(
                previous,
                evidence.HasPersistentDeviation
                    ? OccurrenceState.PersistentDeviation
                    : OccurrenceState.TransientDeviation,
                RegimeTransitionReasonCode.CandidateExplicitlyInvalidated,
                "A evidência invalidou a formação do candidato.");
        }

        if (evidence.CandidateFormationConfirmed ||
            evidence.HasLevelShiftConfirmation)
        {
            var candidateId = evidence.ProposedCandidateRegimeId ??
                              $"candidate:{observation.Sequence}";

            var candidate = previous with
            {
                Lifecycle = RegimeLifecycleState.CandidateRegime,
                Occurrence = OccurrenceState.TransitionInProgress,
                CandidateRegimeId = candidateId,
                CandidateStartedAt = observation.Timestamp,
                CandidateStartedSequence = observation.Sequence,
                ConsecutiveCandidateObservations = 1
            };

            var reasons = new List<RegimeTransitionReason>
            {
                Reason(
                    RegimeTransitionReasonCode.CandidateFormationConfirmed,
                    "A formação de uma distribuição candidata foi confirmada.")
            };

            if (evidence.HasLevelShiftConfirmation)
            {
                reasons.Add(Reason(
                    RegimeTransitionReasonCode.LevelShiftConfirmed,
                    "Uma mudança estrutural de patamar confirmou a candidatura."));
            }

            return Decision(
                previous,
                candidate,
                RegimePolicyAction.OpenCandidate,
                true,
                reasons.ToArray());
        }

        /*
         * B3.22.5 V2b:
         * an episode may have been opened by ROC before sustained-noise
         * evidence had enough history. Once Increasing Noise explains the
         * behavior and no material edge remains, preserve the active regime.
         */
        if (evidence.HasIncreasingNoiseEvidence &&
            !evidence.HasMaterialTransitionEdge &&
            !evidence.HasSlowDriftEvidence)
        {
            var stableNoise =
                ClearTransient(previous) with
                {
                    Occurrence =
                        OccurrenceState.PersistentDeviation
                };

            return Decision(
                previous,
                stableNoise,
                RegimePolicyAction.RestoreActiveRegime,
                false,
                candidateWasPromoted: false,
                candidateWasRejected:
                    previous.CandidateRegimeId is not null,
                Reason(
                    RegimeTransitionReasonCode.IncreasingNoiseObserved,
                    "A expansão sustentada de variabilidade não confirmou mudança de regime; o regime ativo foi preservado."));
        }

        return Decision(
            previous,
            previous with
            {
                Occurrence = evidence.HasPersistentDeviation
                    ? OccurrenceState.PersistentDeviation
                    : OccurrenceState.TransitionInProgress
            },
            RegimePolicyAction.ObserveTransition,
            true,
            Reason(
                evidence.HasSlowDriftEvidence
                    ? RegimeTransitionReasonCode.SlowDriftObserved
                    : RegimeTransitionReasonCode.CandidateNeedsMoreData,
                evidence.HasSlowDriftEvidence
                    ? "O drift continua sem plateau candidato confirmado."
                    : "A transição ainda não possui dados para formar um candidato."));
    }

    private static RegimePolicyDecision EvaluateCandidate(
        RegimePolicyState previous,
        ReadingObservation observation,
        RegimeEvidenceSnapshot evidence)
    {
        if (evidence.HasReturnedToActiveRegime)
        {
            return RestoreActive(previous, OccurrenceState.TransientDeviation);
        }

        if (evidence.CandidateInvalidated ||
            evidence.CandidateStability.Status ==
                CandidateStabilityStatus.Unstable)
        {
            return Reject(
                previous,
                evidence.HasPersistentDeviation
                    ? OccurrenceState.PersistentDeviation
                    : OccurrenceState.TransientDeviation,
                evidence.CandidateInvalidated
                    ? RegimeTransitionReasonCode.CandidateExplicitlyInvalidated
                    : RegimeTransitionReasonCode.CandidateIsUnstable,
                evidence.CandidateInvalidated
                    ? "O candidato foi explicitamente invalidado."
                    : "O candidato não manteve estabilidade interna.");
        }

        if (evidence.CandidateStability.Status ==
            CandidateStabilityStatus.Stable)
        {
            return ResolveStableCandidate(
                previous,
                observation,
                evidence);
        }

        return Decision(
            previous,
            previous with
            {
                Occurrence = OccurrenceState.TransitionInProgress,
                ConsecutiveCandidateObservations =
                    previous.ConsecutiveCandidateObservations + 1
            },
            RegimePolicyAction.ContinueCandidate,
            true,
            Reason(
                RegimeTransitionReasonCode.CandidateNeedsMoreData,
                "O candidato ainda está estabilizando ou possui dados insuficientes."));
    }

    private static RegimePolicyDecision EvaluateUnresolved(
        RegimePolicyState previous,
        ReadingObservation observation,
        RegimeEvidenceSnapshot evidence)
    {
        if (evidence.HasReturnedToActiveRegime)
        {
            return RestoreActive(previous, OccurrenceState.TransientDeviation);
        }

        if (evidence.CandidateInvalidated ||
            evidence.CandidateStability.Status == CandidateStabilityStatus.Unstable)
        {
            return Reject(
                previous,
                OccurrenceState.PersistentDeviation,
                evidence.CandidateInvalidated
                    ? RegimeTransitionReasonCode.CandidateExplicitlyInvalidated
                    : RegimeTransitionReasonCode.CandidateIsUnstable,
                "O desvio candidato deixou de ser válido ou estável.");
        }

        if (evidence.CandidateStability.Status == CandidateStabilityStatus.Stable)
        {
            return ResolveStableCandidate(previous, observation, evidence);
        }

        return Decision(
            previous,
            previous with { Occurrence = OccurrenceState.PersistentDeviation },
            RegimePolicyAction.None,
            true,
            Reason(
                RegimeTransitionReasonCode.StabilityWithoutLegitimacy,
                "O desvio permanece não resolvido e o baseline ativo continua preservado."));
    }

    private static RegimePolicyDecision ResolveStableCandidate(
        RegimePolicyState previous,
        ReadingObservation observation,
        RegimeEvidenceSnapshot evidence)
    {
        var legitimacy = evidence.Legitimacy;

        if (legitimacy.HasSafetyViolation)
        {
            return StableUnresolved(
                previous,
                RegimeTransitionReasonCode.PromotionBlockedBySafetyViolation,
                "Uma violação de segurança veta absolutamente a promoção.");
        }

        if (legitimacy.HasContradictoryEvidence)
        {
            return StableUnresolved(
                previous,
                RegimeTransitionReasonCode.PromotionBlockedByContradictoryEvidence,
                "Evidência contraditória bloqueia a promoção do candidato.");
        }

        if (legitimacy.MatchesKnownRegime)
        {
            var next = ClearTransient(previous) with
            {
                ActiveRegimeId = legitimacy.MatchedKnownRegimeId!,
                Occurrence = OccurrenceState.ConfirmedRegimeTransition
            };

            return Decision(
                previous,
                next,
                RegimePolicyAction.ActivateKnownRegime,
                false,
                candidateWasPromoted: true,
                candidateWasRejected: false,
                Reason(
                    RegimeTransitionReasonCode.CandidateIsStable,
                    "O candidato apresentou estabilidade suficiente."),
                Reason(
                    RegimeTransitionReasonCode.KnownRegimeMatched,
                    "O candidato corresponde a um regime conhecido."));
        }

        if (legitimacy.AuthorizesNewRegime)
        {
            if (observation.SignalKind != RegimeSignalKind.Continuous)
            {
                return StableUnresolved(
                    previous,
                    RegimeTransitionReasonCode.PromotionBlockedBySignalKind,
                    observation.SignalKind == RegimeSignalKind.Unknown
                        ? "Unknown nunca pode promover regime novo, inclusive com aprovação humana."
                        : $"{observation.SignalKind} não possui algoritmo de promoção de regime novo nesta versão.");
            }

            var promotedId = previous.CandidateRegimeId ??
                             evidence.ProposedCandidateRegimeId ??
                             $"candidate:{observation.Sequence}";

            var next = ClearTransient(previous) with
            {
                ActiveRegimeId = promotedId,
                Occurrence = OccurrenceState.ConfirmedRegimeTransition
            };

            return Decision(
                previous,
                next,
                RegimePolicyAction.ConfirmNewRegime,
                false,
                candidateWasPromoted: true,
                candidateWasRejected: false,
                Reason(
                    RegimeTransitionReasonCode.CandidateIsStable,
                    "O candidato apresentou estabilidade suficiente."),
                Reason(
                    RegimeTransitionReasonCode.NewRegimePromotionAuthorized,
                    "Uma autoridade explícita autorizou a promoção do regime novo."));
        }

        return StableUnresolved(
            previous,
            RegimeTransitionReasonCode.StabilityWithoutLegitimacy,
            "Estabilidade sem legitimidade não promove um regime novo.");
    }

    private static RegimePolicyDecision StableUnresolved(
        RegimePolicyState previous,
        RegimeTransitionReasonCode reasonCode,
        string description)
    {
        var unresolved = previous with
        {
            Lifecycle = RegimeLifecycleState.StableUnresolvedDeviation,
            Occurrence = OccurrenceState.StableUnresolvedDeviation
        };

        return Decision(
            previous,
            unresolved,
            RegimePolicyAction.MarkStableUnresolved,
            true,
            Reason(RegimeTransitionReasonCode.CandidateIsStable,
                "O candidato é estatisticamente estável."),
            Reason(reasonCode, description));
    }

    private static RegimePolicyDecision RestoreActive(
        RegimePolicyState previous,
        OccurrenceState occurrence)
    {
        return Decision(
            previous,
            ClearTransient(previous) with { Occurrence = occurrence },
            RegimePolicyAction.RestoreActiveRegime,
            false,
            candidateWasPromoted: false,
            candidateWasRejected: previous.CandidateRegimeId is not null,
            Reason(
                RegimeTransitionReasonCode.ReturnedToActiveRegime,
                "A observação retornou ao regime ativo preservado."));
    }

    private static RegimePolicyDecision Reject(
        RegimePolicyState previous,
        OccurrenceState occurrence,
        RegimeTransitionReasonCode reasonCode,
        string description)
    {
        return Decision(
            previous,
            ClearTransient(previous) with { Occurrence = occurrence },
            RegimePolicyAction.RejectCandidate,
            false,
            candidateWasPromoted: false,
            candidateWasRejected: true,
            Reason(reasonCode, description));
    }

    private static RegimePolicyDecision Suspend(
        RegimePolicyState previous,
        RegimeTransitionReasonCode reasonCode,
        string description)
    {
        var suspended = previous with
        {
            Lifecycle = RegimeLifecycleState.SuspendedByDataQuality,
            Occurrence = OccurrenceState.DataQualityIncident,
            CandidateRegimeId = null,
            CandidateStartedAt = null,
            CandidateStartedSequence = null,
            ConsecutiveCandidateObservations = 0,
            LifecycleBeforeSuspension = previous.Lifecycle
        };

        return Decision(
            previous,
            suspended,
            RegimePolicyAction.SuspendForDataQuality,
            true,
            candidateWasPromoted: false,
            candidateWasRejected: previous.CandidateRegimeId is not null,
            Reason(reasonCode, description));
    }

    private static RegimePolicyState ClearTransient(
        RegimePolicyState state) =>
        state with
        {
            Lifecycle = RegimeLifecycleState.StableKnownRegime,
            CandidateRegimeId = null,
            TransitionStartedAt = null,
            CandidateStartedAt = null,
            TransitionStartedSequence = null,
            CandidateStartedSequence = null,
            ConsecutiveCandidateObservations = 0,
            LifecycleBeforeSuspension = null
        };

    private static RegimePolicyDecision Decision(
        RegimePolicyState previous,
        RegimePolicyState next,
        RegimePolicyAction action,
        bool freeze,
        params RegimeTransitionReason[] reasons) =>
        new(previous, next, action, reasons, freeze);

    private static RegimePolicyDecision Decision(
        RegimePolicyState previous,
        RegimePolicyState next,
        RegimePolicyAction action,
        bool freeze,
        bool candidateWasPromoted,
        bool candidateWasRejected,
        params RegimeTransitionReason[] reasons) =>
        new(
            previous,
            next,
            action,
            reasons,
            freeze,
            candidateWasPromoted,
            candidateWasRejected);

    private static RegimeTransitionReason Reason(
        RegimeTransitionReasonCode code,
        string description) =>
        new(code, description);
}
