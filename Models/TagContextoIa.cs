// Modelo que representa o contexto operacional de uma tag.
// Ele armazena informações complementares usadas na análise e nas dependências.
public class TagContextoIa
{
    public int Id { get; set; }

    // Cliente e tag associados ao contexto.
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";

    // Metadados textuais do contexto.
    public string? TipoRepresentacao { get; set; }
    public string? Descricao { get; set; }
    public string? Criticidade { get; set; }

    // Listas são serializadas em JSON para armazenamento.
    public string? Impactos { get; set; }
    public string? ModosOperacao { get; set; }
    public string? CausasProvaveis { get; set; }
    public string? AcoesRecomendadas { get; set; }

    public string? Equipamento { get; set; }
    public string? Area { get; set; }
    public string? ObservacaoModoOperacao { get; set; }

    // Indica se o contexto tem as informações mínimas necessárias.
    public bool ContextoCompleto { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;
}