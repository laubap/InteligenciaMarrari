namespace IAApi.Benchmark.ExperimentalAnomalyDecisions;

public enum ExperimentalAnomalyDecisionKind
{
    LegacyNormal,

    /// <summary>
    /// Continua sendo uma nova evidÃªncia anÃ´mala.
    /// </summary>
    NewAnomaly,

    /// <summary>
    /// O desvio continua existindo, mas esta leitura Ã© compatÃ­vel
    /// com o Candidate B jÃ¡ conhecido pelo incidente.
    /// NÃƒO significa Normal.
    /// </summary>
    StableUnresolvedContinuation,

    /// <summary>
    /// A leitura deixou de ser compatÃ­vel com B.
    /// Pode representar B -> C.
    /// </summary>
    CandidateMismatch,

    /// <summary>
    /// A qualidade dos dados nÃ£o permite reduzir a decisÃ£o com seguranÃ§a.
    /// </summary>
    DataQualityUncertain,

    /// <summary>
    /// Increasing Noise continua ativo, mas esta leitura nao representa
    /// uma nova evidencia independente.
    /// </summary>
    IncreasingNoiseContinuation
}

public sealed record ExperimentalAnomalyDecisionInput(
    int FastWindow,
    long MinuteIndex,
    DateTime Timestamp,
    string Tag,

    bool LegacyFinalAnomaly,
    double LegacyScore,

    string? IncidentId,
    string? RegimeEpisodeId,

    bool CandidateBaselineReady,
    string RegimeState,
    string AgainstActive,

    bool MadCandidateVote,
    bool ZCandidateVote,
    bool EwmaCandidateVote,

    bool CriticalGap,
    bool OperationalBaselineChanged,
    bool Promotion,

    IReadOnlyList<string> LegacyAnomalousDetectors)
{
    public bool FrozenSensor { get; init; }

    public bool IncreasingNoise { get; init; }

    public bool IncreasingNoiseStarted { get; init; }

    public bool IncreasingNoiseContinuation { get; init; }
}

public sealed record ExperimentalAnomalyDecision(
    ExperimentalAnomalyDecisionKind Kind,

    /// <summary>
    /// Indica se esta leitura deve ser contabilizada como
    /// uma NOVA ANOMALIA no modelo experimental.
    ///
    /// CandidateMismatch pode representar nova evidÃªncia
    /// sem necessariamente representar uma nova ocorrÃªncia.
    /// </summary>
    bool CountsAsNewAnomaly,

    /// <summary>
    /// Indica que existe evidÃªncia nova relevante que deve
    /// permanecer visÃ­vel no modo experimental.
    ///
    /// Isso permite preservar CandidateMismatch / B -> C
    /// sem inflar automaticamente NEW ANOMALIES.
    /// </summary>
    bool CountsAsNewEvidence,

    IReadOnlyList<string> Reasons);

public sealed record ExperimentalAnomalyDecisionRecord(
    int FastWindow,
    long MinuteIndex,
    DateTime Timestamp,
    string Tag,

    bool LegacyFinalAnomaly,
    double LegacyScore,

ExperimentalAnomalyDecisionKind ExperimentalDecision,
bool CountsAsNewAnomaly,
bool CountsAsNewEvidence,

string? IncidentId,
    string? RegimeEpisodeId,

    bool CandidateBaselineReady,
    string RegimeState,
    string AgainstActive,

    bool MadCandidateVote,
    bool ZCandidateVote,
    bool EwmaCandidateVote,

    bool CriticalGap,
    bool OperationalBaselineChanged,
    bool Promotion,

    IReadOnlyList<string> LegacyAnomalousDetectors,
    IReadOnlyList<string> Reasons);

