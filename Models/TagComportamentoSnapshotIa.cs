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