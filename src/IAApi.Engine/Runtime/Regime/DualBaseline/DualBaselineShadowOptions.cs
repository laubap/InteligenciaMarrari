namespace IAApi.Services.AnomalyDetection.Runtime.Regime.DualBaseline;

/// <summary>
/// Configuracao exclusivamente observacional do experimento B3.5.1.
/// Os thresholds dos detectores sao copiados explicitamente da configuracao
/// funcional; os demais parametros sao experimentais e nao afetam a IA.
/// </summary>
public sealed record DualBaselineShadowOptions
{
    public const string ReferenceTag = "DRYCHAMBER04.MATERIAL_TEMPERATURE";
    public bool Enabled { get; init; }
    public bool DecisionCounterfactualEnabled { get; init; }
    public bool ObserveAllTagsAndWindows { get; init; }
    public bool IncidentCounterfactualEnabled { get; init; }
    public string Tag { get; init; } = ReferenceTag;
    public int FastWindowIndex { get; init; } = 4;
    public long TraceStartMinuteIndex { get; init; } = 315240;
    public long TraceEndMinuteIndex { get; init; } = 316220;

    // Dois conjuntos de dez pontos: minimo funcional ja exigido por MAD/EWMA,
    // aplicado separadamente a formacao e validacao. Nao e calibracao de classe.
    public int FormationMinimumSamples { get; init; } = 10;
    public int ValidationMinimumSamples { get; init; } = 10;
    public int MinimumIndependentSamples { get; init; } = 20;

    // Regra experimental de estabilidade de escala; fica no metadata/trace.
    public double MaximumFormationValidationScaleRatio { get; init; } = 2.0;

    public double MadThreshold { get; init; }
    public double ZScoreThreshold { get; init; }
    public double EwmaLambda { get; init; }
    public double EwmaSigmaLimit { get; init; }

}
