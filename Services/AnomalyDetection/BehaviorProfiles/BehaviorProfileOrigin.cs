namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

/// <summary>
/// Fonte da decisao comportamental final. Este contrato ainda nao e consumido
/// pelo pipeline de deteccao.
/// </summary>
public enum BehaviorProfileOrigin
{
    Unknown = 0,
    Declared = 1,
    Inferred = 2
}
