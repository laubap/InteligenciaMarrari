namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Perfil comportamental inferido para a série.
///
/// Unknown é deliberadamente conservador: quando não existe evidência
/// suficiente para classificar a tag, o roteamento mantém compatibilidade
/// com o ensemble legado.
/// </summary>
public enum TagBehaviorType
{
    Unknown = 0,
    Continuous = 1,
    Discrete = 2,
    Counter = 3,
    Setpoint = 4
}
