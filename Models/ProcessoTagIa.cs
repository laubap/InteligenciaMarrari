using System.Text.Json.Serialization;

// Modelo que representa a associação de uma tag a um processo.
// Uma mesma tag pode participar de vários processos, mas somente um vínculo
// deve ser considerado principal para fins de contexto operacional padrão.
public class ProcessoTagIa
{
    public int Id { get; set; }

    public int ProcessoIaId { get; set; }

    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public string? PapelDaTag { get; set; }

    // Uma tag pode ter vários vínculos, mas no máximo um principal.
    public bool Principal { get; set; }

    [JsonIgnore]
    public ProcessoIa? ProcessoIa { get; set; }
}