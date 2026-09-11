namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Estado experimental retornado por uma estratégia do simulador.
/// </summary>
public enum EnsembleDecisionStatus
{
    NotApplicable = 0,
    Normal = 1,
    PendingSuspicion = 2,
    ConfirmedAnomaly = 3
}

/// <summary>
/// Resultado completo da comparação entre o ensemble atual
/// e as regras experimentais.
/// </summary>
public sealed class EnsembleSimulationResult
{
    public string DetectorInputTagName { get; init; } = "";
    public DateTime DataHoraAtual { get; init; }

    public CurrentEnsembleSnapshot DecisaoAtual { get; init; } =
        new();

    public EnsembleStrategyResult SimulacaoPorVotos { get; init; } =
        new();

    public EnsembleStrategyResult SimulacaoPorFamilias { get; init; } =
        new();

    public IReadOnlyList<DetectorResultV2> ResultadosV2 { get; init; } =
        Array.Empty<DetectorResultV2>();

    public bool HouveDivergenciaComRegraDeVotos { get; init; }
    public bool HouveDivergenciaComRegraPorFamilias { get; init; }
}

/// <summary>
/// Snapshot da decisão tomada pelo EnsembleDetectionService atual.
/// </summary>
public sealed class CurrentEnsembleSnapshot
{
    public bool Aplicavel { get; init; }
    public bool EhAnomalia { get; init; }
    public double ScoreNormalizado { get; init; }
    public double Threshold { get; init; }
    public double QuantidadeDetectoresAplicaveis { get; init; }
    public double QuantidadeAlertas { get; init; }
}

/// <summary>
/// Resultado de uma estratégia experimental.
/// </summary>
public sealed class EnsembleStrategyResult
{
    public bool Aplicavel { get; init; }
    public bool EhAnomalia { get; init; }

    public EnsembleDecisionStatus Status { get; init; }

    /// <summary>
    /// Score experimental entre 0 e 1.
    /// </summary>
    public double Score { get; init; }

    public int QuantidadeDetectoresAplicaveis { get; init; }
    public int QuantidadeVotosPositivos { get; init; }
    public int QuantidadeFamiliasPositivas { get; init; }

    public string Explicacao { get; init; } = "";

    public IReadOnlyList<FamilySimulationResult> Familias { get; init; } =
        Array.Empty<FamilySimulationResult>();

    public static EnsembleStrategyResult NotApplicable(
        string explanation)
    {
        return new EnsembleStrategyResult
        {
            Aplicavel = false,
            EhAnomalia = false,
            Status = EnsembleDecisionStatus.NotApplicable,
            Score = 0,
            Explicacao = explanation
        };
    }

    public static EnsembleStrategyResult Normal(
        string explanation)
    {
        return new EnsembleStrategyResult
        {
            Aplicavel = true,
            EhAnomalia = false,
            Status = EnsembleDecisionStatus.Normal,
            Score = 0,
            Explicacao = explanation
        };
    }
}

/// <summary>
/// Consolidação experimental dos detectores de uma mesma família.
/// </summary>
public sealed class FamilySimulationResult
{
    public DetectorFamily Family { get; init; }
    public double Score { get; init; }
    public int DetectorCount { get; init; }

    public IReadOnlyList<string> Detectors { get; init; } =
        Array.Empty<string>();
}
