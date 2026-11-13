/// <summary>
/// Componente adicional (2..N) do regime combinado de uma
/// <see cref="TagRegimeOperacionalIa"/> (PLANO_REGIME_COMBINADO.md §6.2, opção (a)).
///
/// O componente 1 continua sendo a própria <see cref="TagRegimeOperacionalIa.TagRegime"/>
/// (com o mapa em <see cref="TagRegimeOperacionalIa.MapaEstados"/>); esta tabela
/// guarda só os componentes seguintes, na ordem em que entram na tupla.
///
/// Exemplo (B-01, TAG29): componente 1 = TAG42 na linha principal;
/// aqui, Ordem 2 = TAG43 (mapa "2:2;*:0") e Ordem 3 = TAG46 (mapa "380:380;*:450").
///
/// Só é lido com AnomalyDetection:RegimeCombinado:Habilitado = true (a partir da RC-8).
/// Com a chave desligada, esta tabela é ignorada.
/// </summary>
public class TagRegimeComponenteIa
{
    public int Id { get; set; }

    /// <summary>
    /// Id da <see cref="TagRegimeOperacionalIa"/> a que o componente pertence.
    /// Remover a configuração remove os componentes (cascata).
    /// </summary>
    public int ConfiguracaoId { get; set; }

    /// <summary>
    /// Posição do componente na tupla. Começa em 2 (o 1 é a TagRegime da
    /// configuração) e é única por configuração.
    /// </summary>
    public int Ordem { get; set; }

    /// <summary>
    /// Tag cujo estado compõe o regime nesta posição.
    /// </summary>
    public string TagRegime { get; set; } = "";

    /// <summary>
    /// Mapa de estados opcional, na sintaxe de OperationalRegimeStateMap
    /// (ex.: "2:2;*:0"). Nulo = usa o valor lido sem mapear.
    /// </summary>
    public string? MapaEstados { get; set; }

    /// <summary>
    /// Tolerância numérica para casar a leitura com uma entrada do mapa
    /// (ou para comparar estados, sem mapa).
    /// </summary>
    public double ToleranciaEstado { get; set; } = 0.001;
}
