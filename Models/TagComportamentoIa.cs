public class TagComportamentoIa
{
    public const string StatusCandidate = "Candidate";
    public const string StatusKnown = "Known";
    public const string StatusDeprecated = "Deprecated";

    public const string OrigemAutomatic = "Automatic";
    public const string OrigemOperationalRegime = "OperationalRegime";
    public const string OrigemManual = "Manual";

    public const string ClassificacaoUndefined = "Undefined";
    public const string ClassificacaoNormal = "Normal";
    public const string ClassificacaoKnownAbnormal = "KnownAbnormal";

    public int Id { get; set; }

    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    /// <summary>
    /// Identificador estável do comportamento dentro da tag.
    /// Ex.: BEH-001, BEH-002.
    ///
    /// O código não depende das estatísticas atuais do comportamento,
    /// permitindo que snapshots evoluam sem alterar sua identidade.
    /// </summary>
    public string Codigo { get; set; } = "";

    /// <summary>
    /// Estado de maturidade da memória comportamental.
    ///
    /// Candidate: comportamento ainda em avaliação.
    /// Known: comportamento reconhecido com confiança suficiente.
    /// Deprecated: comportamento mantido apenas para histórico.
    /// </summary>
    public string Status { get; set; } =
        StatusCandidate;

    /// <summary>
    /// Origem do comportamento:
    /// descoberta automática, regime operacional explícito ou cadastro manual.
    /// </summary>
    public string Origem { get; set; } =
        OrigemAutomatic;

    /// <summary>
    /// Classificação semântica do comportamento.
    ///
    /// Known não significa automaticamente Normal.
    /// Um comportamento recorrente pode ser conhecido e ainda ser anormal.
    /// </summary>
    public string Classificacao { get; set; } =
        ClassificacaoUndefined;

    public DateTime PrimeiraObservacao { get; set; }

    public DateTime UltimaObservacao { get; set; }

    public int QuantidadeOcorrencias { get; set; }

    public long TotalAmostras { get; set; }

    /// <summary>
    /// Confiança consolidada na existência/identidade deste comportamento.
    /// Escala conceitual: 0 a 100.
    /// </summary>
    public double Confianca { get; set; }

    public DateTime DataCriacao { get; set; } =
        DateTime.UtcNow;

    public DateTime? DataAtualizacao { get; set; }
}