// Modelo que representa o contexto operacional de uma tag.
// Ele armazena informações complementares usadas na análise
// e nas dependências.
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

    // Listas serializadas em JSON para armazenamento.
    public string? Impactos { get; set; }
    public string? ModosOperacao { get; set; }
    public string? CausasProvaveis { get; set; }
    public string? AcoesRecomendadas { get; set; }

    public string? Equipamento { get; set; }
    public string? Area { get; set; }
    public string? ObservacaoModoOperacao { get; set; }

    // Menor variação que o cliente considera relevante.
    //
    // Exemplo:
    // 0.5 para uma temperatura significa que alterações menores
    // que 0,5 °C não devem gerar alerta estatístico relevante.
    //
    // É nullable porque o cliente pode ainda não ter configurado.
    public double? VariacaoMinimaRelevanteCliente { get; set; }

    // Motivo fornecido pelo cliente ou engenheiro de processo.
    //
    // Exemplo:
    // "Oscilações inferiores a 0,5 °C fazem parte da precisão normal
    // do sensor e não afetam o processo."
    public string? JustificativaVariacaoMinima { get; set; }

    // Indica se o valor sugerido automaticamente pela IA
    // foi revisado e aprovado pelo cliente.
    public bool VariacaoMinimaAprovadaPeloCliente { get; set; }

    // Data da aprovação ou última alteração desse parâmetro.
    public DateTime? DataAprovacaoVariacaoMinima { get; set; }

    // Indica se o contexto tem as informações mínimas necessárias.
    public bool ContextoCompleto { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;
}