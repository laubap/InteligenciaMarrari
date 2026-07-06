// Modelo de resposta usado quando uma anomalia é detectada.
// Contém informações de identificação da tag, resultado da detecção,
// resumo, perfil comportamental, contexto operacional, dependências,
// risco de processo, tendências e dados do processo associado.
public class AnomaliaResponse
{
    public string ClienteId { get; set; } = "";
    public string TagName { get; set; } = "";
    public string TipoTag { get; set; } = "";

    // Indica se o valor recebido foi classificado como anomalia.
    public bool EhAnomalia { get; set; }
    public float Score { get; set; }

    public string Mensagem { get; set; } = "";
    public FeedbackSemelhanteDto? FeedbackSemelhante { get; set; }

    public ResumoAnomaliaDto Resumo { get; set; } = new();
    public PerfilComportamentalDto Perfil { get; set; } = new();
    public ContextoOperacionalDto? Contexto { get; set; }
    public List<DependenciaAnalisadaDto> Dependencias { get; set; } = new();
    public RiscoProcessoDto Risco { get; set; } = new();
    public TendenciasDto Tendencias { get; set; } = new();
    public ProcessoDto? Processo { get; set; }
  
}

// Dados resumidos sobre a detecção da anomalia.
public class ResumoAnomaliaDto
{
    public string Status { get; set; } = "";
    public string Conclusao { get; set; } = "";
}

// Dados do perfil comportamental da tag comparando valor recebido com histórico.
public class PerfilComportamentalDto
{
    public double ValorRecebido { get; set; }
    public double MediaHistorica { get; set; }
    public double LimiteInferior { get; set; }
    public double LimiteSuperior { get; set; }
    public double Score { get; set; }
}

// Informações operacionais relacionadas à tag, incluindo contexto e ações.
public class ContextoOperacionalDto
{
    public string? Descricao { get; set; }
    public string? Criticidade { get; set; }
    public string? Equipamento { get; set; }
    public string? Area { get; set; }

    public List<string> Impactos { get; set; } = new();
    public List<string> CausasProvaveis { get; set; } = new();
    public List<string> AcoesRecomendadas { get; set; } = new();
    public List<string> ModosOperacao { get; set; } = new();

    public string? ObservacaoModoOperacao { get; set; }
}

// Informações sobre cada dependência de tag avaliada junto com a anomalia.
public class DependenciaAnalisadaDto
{
    public string TagName { get; set; } = "";
    public string? Descricao { get; set; }

    public double ValorAtual { get; set; }
    public double Media { get; set; }
    public double LimiteInferior { get; set; }
    public double LimiteSuperior { get; set; }

    public bool EhAnomalia { get; set; }
    public string Status { get; set; } = "";

    public string? TipoRelacao { get; set; }
    public string? Impacto { get; set; }
    public string? Criticidade { get; set; }
    public string? Equipamento { get; set; }
    public string? Area { get; set; }

    public string Interpretacao { get; set; } = "";
}

// Informações de risco de processo associadas à anomalia.
public class RiscoProcessoDto
{
    public int Score { get; set; }
    public string Classificacao { get; set; } = "";
    public string Mensagem { get; set; } = "";
    public List<string> Fatores { get; set; } = new();
}

// Tendências de risco e valor para a anomalia.
public class TendenciasDto
{
    public string TendenciaRisco { get; set; } = "";
    public string MensagemTendenciaRisco { get; set; } = "";

    public string TendenciaValor { get; set; } = "";
    public string MensagemTendenciaValor { get; set; } = "";
}

// Dados básicos do processo associado à tag e à anomalia.
public class ProcessoDto
{
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public string? Area { get; set; }
    public string? Criticidade { get; set; }

    public string? Objetivo { get; set; }

    public string? CondicaoNormal { get; set; }

    public string? ConsequenciasFalha { get; set; }

    public string? ProcedimentoRecomendado { get; set; }
}