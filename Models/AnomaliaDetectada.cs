// Modelo que representa uma anomalia detectada para uma tag.
// Guarda informações sobre o valor recebido, o score de anomalia e o
// diagnóstico de risco associado ao processo.
public class AnomaliaDetectada
{
    public int Id { get; set; }

    // Identificador do cliente proprietário da tag.
    public string ClienteId { get; set; } = "";

    // Nome da tag analisada.
    public string TagName { get; set; } = "";

    // Tipo da tag (por exemplo, digital, analógica, etc.).
    public string TipoTag { get; set; } = "";

    // Valor atual recebido da tag no momento da detecção.
    public double ValorRecebido { get; set; }

    // Score calculado para classificar a anomalia.
    public double Score { get; set; }

    // Indica se o valor é considerado anomalia.
    public bool EhAnomalia { get; set; }

    // Mensagem explicando o resultado da detecção.
    public string Mensagem { get; set; } = "";

    // Criticidade da anomalia, se aplicável.
    public string? Criticidade { get; set; }

    // Dependências de outras tags relacionadas à anomalia.
    public List<AnomaliaDependenciaDetectada> Dependencias { get; set; } = new();

    // Score de risco do processo associado a essa anomalia.
    public int ScoreRiscoProcesso { get; set; }
    
    // Classificação de risco derivada do score de processo.
    public string? ClassificacaoRisco { get; set; }

    // Tendência do risco ao longo do tempo.
    public string? TendenciaRisco { get; set; }

    // Tendência do valor da tag ao longo do tempo.
    public string? TendenciaValor { get; set; }

    public StatusAnomalia Status { get; set; } =
    StatusAnomalia.Pendente;

    // Data e hora em que a anomalia foi detectada.
    public DateTime DataDeteccao { get; set; } = DateTime.Now;
}