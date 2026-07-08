public class AcaoRecomendadaIA
{
    public int Id { get; set; }

    public int MotivoAnomaliaIAId { get; set; }

    public string ClienteId { get; set; } = "";

    public string Descricao { get; set; } = "";

    public bool Ativa { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;

    public MotivoAnomaliaIA? MotivoAnomaliaIA { get; set; }
}