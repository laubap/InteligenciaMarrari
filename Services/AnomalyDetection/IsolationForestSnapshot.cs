using IAApi.Services.ModelTraining;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Representação serializável de um modelo Isolation Forest.
/// Esse objeto será salvo como JSON na pasta Models.
/// </summary>
public class IsolationForestSnapshot
{
    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public DateTime DataTreinamento { get; set; }

    public int QuantidadeArvores { get; set; }

    public int TamanhoAmostra { get; set; }

    public int QuantidadeCaracteristicas { get; set; }

    public int ProfundidadeMaxima { get; set; }

    public int FeatureWindowSize { get; set; }

    public double AnomalyThreshold { get; set; }

    // Controle de versão do modelo treinado.
    public int VersaoModelo { get; set; } = 1;

    // Quantidade total de pontos utilizados no treinamento.
    public int QuantidadePontosTreinamento { get; set; }

    // Identifica quais janelas foram usadas no treinamento.
    // Será utilizado para descobrir se o modelo ficou desatualizado.
    public string HashJanelasTreinamento { get; set; } = "";

    // Indica se o treinamento utilizou o histórico recente
    // por não existirem janelas configuradas para a tag.
    public bool UsouFallbackHistoricoRecente { get; set; }

    // Registra exatamente os períodos considerados normais
    // que foram utilizados para treinar este modelo.
    public List<JanelaTreinamentoSnapshot> JanelasTreinamento
        { get; set; } = new();

    public List<IsolationTreeSnapshot> Arvores { get; set; } = new();
}

public class IsolationTreeSnapshot
{
    public int ProfundidadeMaxima { get; set; }

    public int QuantidadeCaracteristicas { get; set; }

    public int QuantidadeAmostras { get; set; }

    public IsolationTreeNodeSnapshot Raiz { get; set; } = new();
}

public class IsolationTreeNodeSnapshot
{
    public bool EhFolha { get; set; }

    public int IndiceCaracteristica { get; set; }

    public double ValorDivisao { get; set; }

    public int QuantidadeAmostras { get; set; }

    public int Profundidade { get; set; }

    public int QuantidadeCaracteristicas { get; set; }

    public IsolationTreeNodeSnapshot? Esquerda { get; set; }

    public IsolationTreeNodeSnapshot? Direita { get; set; }
}