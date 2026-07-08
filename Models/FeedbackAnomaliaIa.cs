public class FeedbackAnomaliaIa
{
    public int Id { get; set; }

    public int AnomaliaId { get; set; }

    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";

    public int? MotivoAnomaliaIAId { get; set; }

    public MotivoAnomaliaIA? MotivoAnomaliaIA { get; set; }
    public string? Descricao { get; set; }
    public string? AcaoTomada { get; set; }

    public DateTime DataRegistro { get; set; }
}