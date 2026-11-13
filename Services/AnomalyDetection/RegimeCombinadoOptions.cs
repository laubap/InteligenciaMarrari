namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Chave do regime combinado (Opção A, PLANO_REGIME_COMBINADO.md §6.1 / RC-4).
///
/// Seção: <c>AnomalyDetection:RegimeCombinado</c>.
/// <c>Habilitado</c> é <c>false</c> por padrão e o padrão vem do código:
/// o <c>appsettings.json</c> não tem a seção.
///
/// Com a chave desligada, o provider não lê a tabela de componentes,
/// ignora <c>MapaEstados</c> e o resolver segue pelo caminho atual
/// (um componente, tolerância numérica). Na RC-4 nada lê esta opção
/// ainda; o uso entra na RC-8.
///
/// Rollback: chave em <c>false</c> (volta ao comportamento atual sem
/// tocar no banco).
/// </summary>
public sealed class RegimeCombinadoOptions
{
    public const string SectionName =
        "AnomalyDetection:RegimeCombinado";

    public bool Habilitado { get; set; }
}
