using System.ComponentModel.DataAnnotations;

public class GrupoAnaliseIa
{
    [Key]
    public int Id { get; set; }

    // Cliente proprietário do grupo.
    public string ClienteId { get; set; } = "";

    // Nome do grupo.
    // Ex.: Boiler, Dry Chamber 01, Compressor Principal...
    public string Nome { get; set; } = "";

    // Descrição opcional.
    public string? Descricao { get; set; }

    // Grupo ativo?
    public bool Ativo { get; set; } = true;

    // Data de criação.
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Quantas tags precisam estar disponíveis
    // para que a análise seja executada.
    public int MinimumAvailableTags { get; set; } = 2;

    // Tempo máximo permitido entre leituras das
    // tags do grupo.
    public int MaxLagMinutes { get; set; } = 0;

    // Peso deste detector dentro do Ensemble.
    public double PesoEnsemble { get; set; } = 1.0;

    // Sensibilidade (0-1)
    public double Sensibilidade { get; set; } = 0.5;

    // Nome do algoritmo utilizado futuramente.
    // Ex.: Correlation, Regression, PCA,
    // IsolationForest, AutoEncoder...
    public string Algoritmo { get; set; } = "Correlation";

    // Tags pertencentes ao grupo.
    public List<GrupoAnaliseTagIa> Tags { get; set; } = new();
}