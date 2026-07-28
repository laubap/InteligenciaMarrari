using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.Interpretation;

public class AnomalyInterpretationContext
{
    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public double ValorAtual { get; set; }

    public DateTime DataHoraAtual { get; set; }

    public double MediaHistorica { get; set; }

    public double DesvioPadraoHistorico { get; set; }

    public bool EhAnomalia { get; set; }

    public double ScoreNormalizado { get; set; }

    public string Severidade { get; set; } = "";

    public string ExplicacaoEnsemble { get; set; } = "";

    public Dictionary<string, double> MetricasGerais { get; set; } =
        new();

    public List<DetectorDebugItem> Detectores { get; set; } =
        new();

    /*
     * Contexto opcional cadastrado pelo cliente na tabela TagsContextoIa.
     * As propriedades ficam separadas da entidade do banco para evitar
     * acoplamento entre a camada de interpretação e o Entity Framework.
     */

    public bool ContextoDisponivel { get; set; }

    public bool ContextoCompleto { get; set; }

    public string TipoRepresentacao { get; set; } = "";

    public string DescricaoTag { get; set; } = "";

    public string Criticidade { get; set; } = "";

    public string Equipamento { get; set; } = "";

    public string Area { get; set; } = "";

    public string ObservacaoModoOperacao { get; set; } = "";

    public List<string> Impactos { get; set; } =
        new();

    public List<string> ModosOperacao { get; set; } =
        new();

    public List<string> CausasProvaveis { get; set; } =
        new();

    public List<string> AcoesRecomendadas { get; set; } =
        new();
}