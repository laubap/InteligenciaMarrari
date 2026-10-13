namespace IAApi.Benchmark.RegimeShadow.DualBaseline.DecisionCounterfactual;
using IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline.Decision;

public sealed record DualBaselineDecisionRecord(
    int FastWindow, long MinuteIndex, DateTime Timestamp, string Tag,
    string EpisodeId, DualBaselineDecisionClassification Classification,
    IReadOnlyList<string> Reasons, bool LegacyFinalAnomaly, double LegacyFinalScore,
    bool CandidateBaselineReady, string RegimeState, string AgainstActive,
    bool MadCandidateVote, bool ZCandidateVote, bool EwmaCandidateVote,
    bool EwmaOnlyMismatch, bool PotentiallySuppressibleRepeatedAlert,
    bool CurrentReadingInActiveReference, bool CurrentReadingInCandidateReference,
    bool OperationalBaselineChanged, bool Promotion,
    IReadOnlyList<string> LegacyAnomalousDetectors);

public sealed record DualBaselineCounterfactualEpisodeSummary(
    string Tag, int FastWindow, string EpisodeId, long TransitionStartMinute,
    long? StableUnresolvedStartMinute, long? CandidateReadyMinute,
    long? FirstEligibleMinute, long LastMinute, long DurationMinutes,
    long LegacyAnomalies, long Continuations, long SuppressibleRepeatedAlerts,
    long CandidateMismatches, long CandidateRejections, long EwmaOnlyMismatches, bool ReturnedToActive,
    bool Promotion, string FinalState);
