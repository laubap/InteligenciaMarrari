public class TagComportamentoSnapshotIa
{
    public int Id { get; set; }

    public int TagComportamentoIaId { get; set; }

    /// <summary>
    /// Versão sequencial do snapshot para este comportamento.
    /// Ex.: 1, 2, 3...
    ///
    /// Permite preservar a evolução histórica do comportamento
    /// sem sobrescrever estatísticas anteriores.
    /// </summary>
    public int Versao { get; set; }

    public double Media { get; set; }

    public double DesvioPadrao { get; set; }

    /*
     * B3.25 — identidade comportamental rica.
     *
     * Nullable por compatibilidade com snapshots criados antes desta evolução.
     * Registros antigos permanecem válidos e continuam sendo reconciliados
     * pelas dimensões legadas até que um snapshot novo seja produzido.
     */
    public double? Mediana { get; set; }

    public double? PrimeiroQuartil { get; set; }

    public double? TerceiroQuartil { get; set; }

    public double? IntervaloInterquartil { get; set; }

    public double? RobustScale { get; set; }

    public double? TypicalAbsoluteStep { get; set; }

    public double Minimo { get; set; }

    public double Maximo { get; set; }

    public double Amplitude { get; set; }

    public double PercentualZeros { get; set; }

    public double VariacaoMedia { get; set; }

    public int QuantidadePicos { get; set; }

    public int TotalRegistrosHistorico { get; set; }

    public int TotalRegistrosUsados { get; set; }

    public int TotalOutliersRemovidos { get; set; }

    public double DeadbandRecomendado { get; set; }

    public double ConfiancaDeadband { get; set; }

    public string? JustificativaDeadband { get; set; }

    /// <summary>
    /// Período histórico utilizado para produzir este snapshot.
    /// </summary>
    public DateTime PeriodoInicio { get; set; }

    public DateTime PeriodoFim { get; set; }

    public DateTime DataCriacao { get; set; } =
        DateTime.UtcNow;
}