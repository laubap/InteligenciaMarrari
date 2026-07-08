public class MotivoAnomaliaIA
{
    public int Id { get; set; }

    public string ClienteId { get; set; } = "";

    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;

    public int? CategoriaFalhaIAId { get; set; }

    public CategoriaFalhaIA? CategoriaFalhaIA { get; set; }

    public List<AcaoRecomendadaIA> AcoesRecomendadas { get; set; } = new();
}