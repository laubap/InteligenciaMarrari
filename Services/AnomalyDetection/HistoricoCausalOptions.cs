namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Chave do recorte causal do histórico de detecção (P31, BACKLOG 25).
///
/// Seção: <c>AnomalyDetection:HistoricoCausal</c>.
/// <c>Habilitado</c> é <c>false</c> por padrão e o padrão vem do código:
/// o <c>appsettings.json</c> não tem a seção.
///
/// Com a chave ligada, <c>PredictionDetectionPreparationService.PrepararAsync</c>
/// usa só os pontos do histórico <b>anteriores</b> à hora da leitura
/// (<c>DataHora &lt; dataHoraAtual</c>), a mesma janela do replay CP300.
/// Isso evita que a leitura do lote, buscada no início do ciclo, seja
/// recusada como "anterior ao último ponto do histórico" e gravada com
/// nota 0. O validador não muda.
///
/// Se os pontos posteriores à leitura avançam mais que
/// <c>ToleranciaMinutos</c> (relógio ou fuso errado), o histórico não é
/// recortado e o validador recusa a entrada, como antes.
///
/// Rollback: chave em <c>false</c> (ou sem a seção) e reiniciar a IAApi.
/// </summary>
public sealed class HistoricoCausalOptions
{
    public const string SectionName =
        "AnomalyDetection:HistoricoCausal";

    public const double ToleranciaMinutosPadrao = 30;

    public bool Habilitado { get; set; }

    public double ToleranciaMinutos { get; set; } =
        ToleranciaMinutosPadrao;
}
