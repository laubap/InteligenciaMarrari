namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Representa as características utilizadas pelo Isolation Forest.
///
/// Cada ponto histórico deixa de ser somente um valor e passa
/// a representar também o seu comportamento temporal.
/// </summary>
public class IsolationForestFeature
{
    public double Valor { get; set; }

    public double MediaMovel { get; set; }

    public double DesvioMovel { get; set; }

    public double DiferencaMediaMovel { get; set; }

    public double ZScoreLocal { get; set; }

    public double TaxaVariacaoPorMinuto { get; set; }

    public double Ewma { get; set; }

    public double HoraSin { get; set; }

    public double HoraCos { get; set; }

    /// <summary>
    /// Converte a característica para o vetor numérico usado
    /// pelas árvores do Isolation Forest.
    /// </summary>
    public IReadOnlyList<double> ParaVetor()
    {
        return new[]
        {
            Valor,
            MediaMovel,
            DesvioMovel,
            DiferencaMediaMovel,
            ZScoreLocal,
            TaxaVariacaoPorMinuto,
            Ewma,
            HoraSin,
            HoraCos
        };
    }
}