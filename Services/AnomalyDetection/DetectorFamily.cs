namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Agrupa detectores que produzem tipos semelhantes de evidência.
///
/// O Ensemble V2 deverá combinar primeiro os resultados dentro de cada
/// família e, depois, combinar famílias diferentes. Isso reduz o falso
/// consenso causado por detectores correlacionados.
/// </summary>
public enum DetectorFamily
{
    /// <summary>
    /// Família ainda não informada. Deve ser evitada em detectores migrados.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Desvio do valor em relação a uma distribuição ou referência histórica.
    /// Exemplos: Z-Score e Mediana/MAD.
    /// </summary>
    StatisticalDeviation = 1,

    /// <summary>
    /// Tendência, suavização e deslocamento acumulado ao longo do tempo.
    /// Exemplo: EWMA.
    /// </summary>
    TrendAndSmoothing = 2,

    /// <summary>
    /// Mudanças abruptas, taxas e transições de patamar.
    /// Exemplos: Taxa de Variação e Mudança de Patamar.
    /// </summary>
    Transition = 3,

    /// <summary>
    /// Confirmação de que um comportamento permaneceu por tempo ou pontos
    /// suficientes.
    /// Exemplo: Persistência.
    /// </summary>
    TemporalConfirmation = 4,

    /// <summary>
    /// Evidência calculada pela relação simultânea entre várias tags.
    /// </summary>
    Multivariate = 5,

    /// <summary>
    /// Evidência produzida por modelos de aprendizado de máquina.
    /// Exemplo: Isolation Forest.
    /// </summary>
    MachineLearning = 6,

    /// <summary>
    /// Qualidade, ausência, congelamento, atraso ou validade dos dados.
    /// </summary>
    DataQuality = 7,

    /// <summary>
    /// Estados, comandos, códigos e transições discretas.
    /// </summary>
    DiscreteState = 8
}
