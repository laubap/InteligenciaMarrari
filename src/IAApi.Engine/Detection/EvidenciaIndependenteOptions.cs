namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Chave comum dos detectores de evidência independente
/// (BACKLOG 33, papel <see cref="DetectorEvidenceRole.IndependentEvidence"/>).
///
/// <c>Habilitado</c> é <c>false</c> por padrão e o padrão vem do código:
/// o <c>appsettings.json</c> não tem as seções. <c>ClientesHabilitados</c>
/// vazia significa nenhum cliente.
///
/// Passo 1 da tarefa 33: só a estrutura. Nenhum detector lê estas opções
/// ainda; os passos 2 (valor congelado) e 4 (ruído excessivo) registram
/// o detector na DI só com a chave ligada e usam
/// <see cref="EstaHabilitadoPara"/> para o filtro de cliente.
///
/// Rollback: chave em <c>false</c> (o detector deixa de ser registrado
/// na próxima inicialização).
/// </summary>
public abstract class EvidenciaIndependenteOptions
{
    public bool Habilitado { get; set; }

    public List<string> ClientesHabilitados { get; set; } =
        new();

    /// <summary>
    /// Verdadeiro só com a chave ligada e o cliente na lista
    /// (sem diferenciar maiúsculas/minúsculas, ignorando espaços nas pontas).
    /// </summary>
    public bool EstaHabilitadoPara(
        string? clienteId)
    {
        if (!Habilitado ||
            ClientesHabilitados is null ||
            ClientesHabilitados.Count == 0 ||
            string.IsNullOrWhiteSpace(clienteId))
        {
            return false;
        }

        var cliente = clienteId.Trim();

        foreach (var habilitado in ClientesHabilitados)
        {
            if (habilitado is not null &&
                string.Equals(
                    habilitado.Trim(),
                    cliente,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Seção <c>AnomalyDetection:ValorCongelado</c> (BACKLOG 33.C).
/// Os parâmetros do detector entram no passo 2.
/// </summary>
public sealed class ValorCongeladoOptions :
    EvidenciaIndependenteOptions
{
    public const string SectionName =
        "AnomalyDetection:ValorCongelado";
}

/// <summary>
/// Seção <c>AnomalyDetection:RuidoExcessivo</c> (BACKLOG 33.R).
/// Os parâmetros do detector entram no passo 4.
/// </summary>
public sealed class RuidoExcessivoOptions :
    EvidenciaIndependenteOptions
{
    public const string SectionName =
        "AnomalyDetection:RuidoExcessivo";
}
