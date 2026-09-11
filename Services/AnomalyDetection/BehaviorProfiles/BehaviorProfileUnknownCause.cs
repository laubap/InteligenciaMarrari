namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

/// <summary>
/// Preserva a causa de um resultado Unknown em vez de colapsar situacoes
/// semanticamente diferentes em um unico valor.
/// </summary>
public enum BehaviorProfileUnknownCause
{
    None = 0,
    MissingDeclaration = 1,
    InvalidOrUnrecognizedDeclaration = 2,
    InferenceInconclusive = 3
}
