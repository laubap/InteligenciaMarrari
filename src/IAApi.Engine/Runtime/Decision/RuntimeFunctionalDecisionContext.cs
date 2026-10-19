namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.18 â€” contexto mÃ­nimo necessÃ¡rio para aplicar no runtime real
/// as mesmas regras funcionais validadas no benchmark.
///
/// Nesta etapa o contexto ainda pode estar indisponÃ­vel.
/// A ausÃªncia dele nunca autoriza reduÃ§Ã£o: preserva Legacy.
///
/// B3.20.11 â€” adiciona evidÃªncias fortes do baseline ativo sem alterar
/// a assinatura posicional original do record. Isso preserva compatibilidade
/// com testes/callers existentes e permite ao runtime recuperar, de forma
/// conservadora, falsos negativos do ensemble Legacy.
/// </summary>
public sealed record RuntimeFunctionalDecisionContext(
    string? IncidentId,
    bool CandidateBaselineReady,
    string RegimeState,
    string AgainstActive,
    bool MadCandidateVote,
    bool ZCandidateVote,
    bool EwmaCandidateVote,
    bool CriticalGap,
    bool OperationalBaselineChanged,
    bool Promotion)
{
    public bool ZActiveVote { get; init; }
    public double ZActiveScore { get; init; }

    public bool MadActiveVote { get; init; }
    public double MadActiveScore { get; init; }

    public bool RateActiveVote { get; init; }
    public double RateActiveScore { get; init; }

    public bool FrozenSensor { get; init; }

    public bool IncreasingNoise { get; init; }

    public bool IncreasingNoiseStarted { get; init; }

    public bool IncreasingNoiseContinuation { get; init; }

    public double LegacyScoreNormalized { get; init; }
}

public static class RuntimeFunctionalDecisionSources
{
    public const string LegacyNormal = "LegacyNormal";
    public const string NewAnomaly = "NewAnomaly";
    public const string StableUnresolvedContinuation =
        "StableUnresolvedContinuation";
    public const string CandidateMismatch = "CandidateMismatch";
    public const string DataQualityUncertain =
        "DataQualityUncertain";
}

