namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Nível de confiança atribuído ao conjunto de evidências.
///
/// O nível não substitui o diagnóstico da anomalia.
/// Ele representa o grau de sustentação independente do evento.
/// </summary>
public enum ConfidenceLevel
{
    NotApplicable = 0,
    Normal = 1,
    Suspicious = 2,
    Probable = 3,
    Confirmed = 4
}
