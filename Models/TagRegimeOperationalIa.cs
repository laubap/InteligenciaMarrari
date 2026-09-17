/// <summary>
/// Configura uma tag de estado operacional responsável por selecionar
/// o baseline estatístico adequado de uma tag analisada.
///
/// Exemplo conceitual:
///
/// TagName:
///     RES01.INLET_FLOW_LPS
///
/// TagRegime:
///     WELL01.PUMP_RUNNING
///
/// Dessa forma, uma mesma tag contínua pode possuir comportamentos
/// normais diferentes de acordo com o estado operacional do processo.
///
/// Esta configuração não executa detecção e não altera o ensemble.
/// Ela apenas descreve como o histórico poderá ser particionado antes
/// da criação do DetectorInput.
/// </summary>
public class TagRegimeOperacionalIa
{
    public int Id { get; set; }

    /// <summary>
    /// Cliente/tenant proprietário da configuração.
    /// </summary>
    public string ClienteId { get; set; } = "";

    /// <summary>
    /// Tag cuja detecção será condicionada por um regime operacional.
    ///
    /// Exemplo:
    /// SAMAE_SIM.RES01.INLET_FLOW_LPS
    /// </summary>
    public string TagName { get; set; } = "";

    /// <summary>
    /// Tag utilizada para identificar o estado operacional.
    ///
    /// A primeira versão assume uma tag numérica/discreta, como:
    /// 0 = equipamento desligado
    /// 1 = equipamento ligado
    /// </summary>
    public string TagRegime { get; set; } = "";

    /// <summary>
    /// Ativa ou desativa a aplicação desta configuração.
    ///
    /// Quando false, a detecção continua utilizando o perfil global.
    /// </summary>
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Defasagem temporal entre a tag de regime e a tag analisada.
    ///
    /// O instante esperado da tag de regime é calculado como:
    ///
    ///     instanteRegime = instanteTagAnalisada - LagMinutos
    ///
    /// Exemplo:
    ///
    /// PUMP_RUNNING em 10:00
    /// influencia INLET_FLOW_LPS em 10:01
    ///
    /// Nesse caso:
    /// LagMinutos = 1.
    /// </summary>
    public int LagMinutos { get; set; }

    /// <summary>
    /// Idade máxima, em minutos, permitida para uma leitura da tag
    /// de regime ser associada ao instante esperado.
    ///
    /// O alinhamento será causal:
    /// nunca deve ser utilizada uma leitura futura da tag de regime
    /// para explicar uma leitura passada da tag analisada.
    /// </summary>
    public int ToleranciaAlinhamentoMinutos { get; set; } = 2;

    /// <summary>
    /// Tolerância numérica utilizada para considerar dois valores
    /// da tag de regime como pertencentes ao mesmo estado.
    ///
    /// É útil para tags discretas recebidas como double.
    ///
    /// Exemplo:
    /// 0 e 0.0001 podem representar o mesmo estado operacional.
    /// </summary>
    public double ToleranciaEstado { get; set; } = 0.001;

    /// <summary>
    /// Número mínimo de amostras históricas da tag analisada que devem
    /// pertencer ao regime atual para permitir a utilização de um
    /// baseline condicionado.
    ///
    /// Se essa quantidade não for atingida, o sistema deverá preservar
    /// o comportamento atual e utilizar o perfil global.
    /// </summary>
    public int MinimoAmostrasPorRegime { get; set; } = 30;

    /// <summary>
    /// Data de criação da configuração.
    /// </summary>
    public DateTime DataCriacao { get; set; } =
        DateTime.UtcNow;

    /// <summary>
    /// Última atualização da configuração, quando aplicável.
    /// </summary>
    public DateTime? DataAtualizacao { get; set; }
}