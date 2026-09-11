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

    // Decisão OPERACIONAL usada pela API/front/notificações.
    public bool EhAnomalia { get; set; }

    // ===============================
    // B3.19 — auditoria funcional
    // ===============================

    // Resultado Legacy original do ensemble, antes da decisão funcional.
    //
    // Nullable para distinguir registros históricos anteriores à B3.19,
    // que não possuíam essa informação persistida.
    public bool? LegacyEhAnomalia { get; set; }

    // Indica se a decisão funcional classificou a leitura como nova evidência.
    // Ex.: CandidateMismatch pode ser false em EhAnomalia e true aqui.
    public bool? EmitNewEvidence { get; set; }

    // Fonte semântica da decisão:
    // LegacyNormal, NewAnomaly, StableUnresolvedContinuation,
    // CandidateMismatch, DataQualityUncertain ou fallback Legacy.
    public string? RuntimeDecisionSource { get; set; }

    // Indica se o resolver precisou usar o caminho Legacy/fail-safe.
    public bool? RuntimeUsedLegacyFallback { get; set; }

    // Motivos completos da decisão serializados como JSON.
    public string? RuntimeDecisionReasonsJson { get; set; }

    // Incidente funcional correlacionado, quando existir.
    public string? RuntimeIncidentId { get; set; }

    // Estado do regime observado no momento da decisão.
    public string? RuntimeRegimeState { get; set; }

    // Estado de prontidão da Candidate Baseline B.
    public bool? RuntimeCandidateBaselineReady { get; set; }

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