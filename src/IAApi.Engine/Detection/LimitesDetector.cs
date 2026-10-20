namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Referências e limites usados por um detector para explicar seu resultado.
///
/// Todos os campos são opcionais porque nem todo detector trabalha com
/// limites inferior/superior ou com uma referência estatística única.
/// </summary>
public sealed class LimitesDetector
{
    /// <summary>
    /// Limite inferior calculado pelo detector, quando aplicável.
    /// </summary>
    public double? Inferior { get; init; }

    /// <summary>
    /// Limite superior calculado pelo detector, quando aplicável.
    /// </summary>
    public double? Superior { get; init; }

    /// <summary>
    /// Referência central usada na comparação.
    /// Pode representar média, mediana, EWMA anterior ou outra referência.
    /// </summary>
    public double? Referencia { get; init; }

    /// <summary>
    /// Escala usada pelo detector.
    /// Pode representar desvio-padrão, MAD robusto ou outra medida de dispersão.
    /// </summary>
    public double? Escala { get; init; }

    /// <summary>
    /// Threshold bruto do detector, quando houver.
    /// Exemplo: limite de sigma, Z-score ou score mínimo.
    /// </summary>
    public double? Threshold { get; init; }
}
