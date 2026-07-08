public class CategoriaFalhaIA
{
    public int Id { get; set; }

    public string ClienteId { get; set; } = "";

    public string Nome { get; set; } = "";

    public string? Descricao { get; set; }

    public bool Ativa { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;

    public List<MotivoAnomaliaIA> Motivos { get; set; } = new();
}