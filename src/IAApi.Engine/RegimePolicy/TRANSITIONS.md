# B2.2 - Regime Policy: executable specification

## Frozen principles

1. Stability authorizes investigation; it never authorizes promotion alone.
2. A completely new regime is not learned only because it persisted or became stable.
3. The active baseline remains preserved while a candidate exists.
4. Regime lifecycle and occurrence classification are independent.
5. The policy is not a detector and produces no ensemble vote.
6. Safety violations are an absolute promotion veto.
7. Contradictory evidence blocks promotion.
8. `CandidateStability` and `LegitimacyEvidence` are independent inputs.
9. `RegimeSignalKind.Unknown` never confirms a new regime, including with human approval.
10. The engine owns no state and performs no I/O.

## Functional contract

```text
PreviousRegimeState + ReadingObservation + RegimeEvidenceSnapshot
    -> RegimePolicyDecision(NextState, Action, AuditReasons)
```

The caller supplies sequence and timestamp. The engine does not read a global
clock, database, detector, history service, configuration or dependency
injection container.

## Lifecycle transitions

| Previous lifecycle | Evidence | Next lifecycle | Action |
|---|---|---|---|
| StableKnownRegime | none | StableKnownRegime | None |
| StableKnownRegime | material edge | TransitionSuspected | ObserveTransition |
| StableKnownRegime | slow drift | TransitionSuspected | ObserveTransition |
| TransitionSuspected | return to active | StableKnownRegime | RestoreActiveRegime |
| TransitionSuspected | candidate formation/level shift | CandidateRegime | OpenCandidate |
| TransitionSuspected | invalidated | StableKnownRegime | RejectCandidate |
| CandidateRegime | insufficient/stabilizing | CandidateRegime | ContinueCandidate |
| CandidateRegime | unstable/invalid | StableKnownRegime | RejectCandidate |
| CandidateRegime | return to active | StableKnownRegime | RestoreActiveRegime |
| CandidateRegime | stable, no legitimacy | StableUnresolvedDeviation | MarkStableUnresolved |
| CandidateRegime | stable, known match | StableKnownRegime | ActivateKnownRegime |
| CandidateRegime | stable, authorized new Continuous regime | StableKnownRegime | ConfirmNewRegime |
| StableUnresolvedDeviation | no resolution | StableUnresolvedDeviation | None |
| StableUnresolvedDeviation | return to active | StableKnownRegime | RestoreActiveRegime |
| StableUnresolvedDeviation | stable, known match | StableKnownRegime | ActivateKnownRegime |
| StableUnresolvedDeviation | stable, authorized new Continuous regime | StableKnownRegime | ConfirmNewRegime |
| Any | invalid reading/critical gap | SuspendedByDataQuality | SuspendForDataQuality |
| SuspendedByDataQuality | valid observation | StableKnownRegime | ResumeAfterDataQuality |

`ConfirmedRegime` and `CandidateRejected` are auditable actions rather than
persistent lifecycle states. Confirmation atomically selects the next active
regime. Rejection atomically clears the candidate and restores the preserved
active regime.

## Stability versus legitimacy

`CandidateStability` describes the statistical shape of the candidate:

```text
sample coverage, observed duration, cycle coverage, dispersion, residual
slope, cadence reliability and gaps
```

`LegitimacyEvidence` describes why the candidate may represent valid operation:

```text
known-regime match, validated operational context, explicit mode/setpoint
change, human approval, multivariate process coherence and safety blockers
```

The policy never derives one from the other.

## Promotion rules

A known regime may be activated when the candidate is stable, explicitly
matches that known regime and has no veto.

A completely new regime may be confirmed only when all conditions hold:

```text
candidate stability == Stable
signal kind == Continuous
explicit promotion authority is present
no safety violation
no contradictory evidence
```

Multivariate coherence alone is supporting evidence, not promotion authority.
Unknown, Discrete, Counter and Setpoint do not confirm new regimes in version 1.

## Data quality

A critical gap or invalid reading suspends the policy. Candidate continuity is
discarded, no elapsed time is inferred, and the active regime identity is
preserved. The first valid observation resumes conservatively in the active
known regime without reconstructing the prior candidate.

## Baseline invariant

This phase has no baseline object or mutation command. Decisions expose
`ActiveBaselineMustRemainFrozen` while observing a transition, candidate,
unresolved stable deviation or data-quality suspension. No candidate point can
alter an active baseline through this module.
