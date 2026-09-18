public class TagComportamentoOcorrenciaIa
{
    public int Id { get; set; }

    public int TagComportamentoIaId { get; set; }

    /// <summary>
    /// Início do intervalo em que este comportamento foi observado.
    /// </summary>
    public DateTime DataHoraInicio { get; set; }

    /// <summary>
    /// Fim do intervalo em que este comportamento foi observado.
    /// </summary>
    public DateTime DataHoraFim { get; set; }

    /// <summary>
    /// Quantidade de amostras contidas nesta ocorrência.
    /// </summary>
    public int QuantidadeAmostras { get; set; }

    /// <summary>
    /// Confiança de que este intervalo pertence ao comportamento associado.
    /// Escala conceitual: 0 a 100.
    /// </summary>
    public double Confianca { get; set; }

    /// <summary>
    /// Indica se esta ocorrência foi efetivamente utilizada para atualizar
    /// o conhecimento/baseline do comportamento.
    ///
    /// Uma ocorrência candidata ou rejeitada pode ser registrada sem
    /// contaminar o aprendizado.
    /// </summary>
    public bool UtilizadaNoAprendizado { get; set; }

    public DateTime DataCriacao { get; set; } =
        DateTime.UtcNow;
}