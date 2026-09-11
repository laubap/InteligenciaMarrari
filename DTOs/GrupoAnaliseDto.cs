public class GrupoAnaliseDto
{
    public int Id { get; set; }

    public string Nome { get; set; } = "";

    public string? Descricao { get; set; }

    public string Algoritmo { get; set; } = "";

    public double Sensibilidade { get; set; }

    public double PesoEnsemble { get; set; }

    public int MinimumAvailableTags { get; set; }

    public int MaxLagMinutes { get; set; }

    public List<string> Tags { get; set; } = [];
}