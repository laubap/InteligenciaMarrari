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

    // Avalia se a IA identificou corretamente que havia uma anomalia.
public AvaliacaoIa AvaliacaoDeteccao { get; set; } =
    AvaliacaoIa.NaoAvaliado;

// Avalia se a causa ou tipo de falha sugerido pela IA estava correto.
public AvaliacaoIa AvaliacaoCausa { get; set; } =
    AvaliacaoIa.NaoAvaliado;

// Avalia se as ações recomendadas pela IA foram úteis.
public AvaliacaoIa AvaliacaoAcao { get; set; } =
    AvaliacaoIa.NaoAvaliado;

    public DateTime DataRegistro { get; set; }
}