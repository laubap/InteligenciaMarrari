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

    /// <summary>
    /// Erros dos parâmetros próprios do detector (além da lista de
    /// clientes, que o validador comum confere). Vazio = válido.
    /// </summary>
    public virtual IEnumerable<string> ValidarParametros() =>
        [];
}

/// <summary>
/// Seção <c>AnomalyDetection:ValorCongelado</c> (BACKLOG 33.C).
/// Padrões = os do <c>FrozenSensorEvidenceCalculator</c> em
/// <c>RegimeShadowOptions</c> (10 pontos, 10 min, 30 pontos antes, taxa 0,60),
/// mais a guarda de "repouso conhecido" (10 min).
/// </summary>
public sealed class ValorCongeladoOptions :
    EvidenciaIndependenteOptions
{
    public const string SectionName =
        "AnomalyDetection:ValorCongelado";

    /// <summary>Mínimo de leituras idênticas seguidas (inclui a atual).</summary>
    public int MinimoPontos { get; set; } = 10;

    /// <summary>Duração mínima da sequência idêntica (última − primeira leitura).</summary>
    public double MinimoMinutos { get; set; } = 10;

    /// <summary>Leituras antes da sequência usadas para ver se a tag variava.</summary>
    public int JanelaAnteriorPontos { get; set; } = 30;

    /// <summary>
    /// Acima desta fração de passos sem variação antes da sequência, a tag
    /// já era quase constante e não se alarma.
    /// </summary>
    public double TaxaMaximaPassosIguaisAntes { get; set; } = 0.60;

    /// <summary>
    /// Guarda de "repouso conhecido": se o histórico já tem o mesmo valor
    /// parado por pelo menos estes minutos (antes da sequência atual), o
    /// valor é um repouso legítimo (bomba parada, válvula fechada) e não se
    /// alarma.
    /// </summary>
    public double MinutosRepouso { get; set; } = 10;

    public override IEnumerable<string> ValidarParametros()
    {
        if (MinimoPontos < 3)
            yield return "ValorCongelado.MinimoPontos deve ser >= 3.";

        if (!double.IsFinite(MinimoMinutos) || MinimoMinutos <= 0)
            yield return "ValorCongelado.MinimoMinutos deve ser finito e > 0.";

        if (JanelaAnteriorPontos < 3)
            yield return "ValorCongelado.JanelaAnteriorPontos deve ser >= 3.";

        if (!double.IsFinite(TaxaMaximaPassosIguaisAntes) ||
            TaxaMaximaPassosIguaisAntes < 0 ||
            TaxaMaximaPassosIguaisAntes > 1)
            yield return "ValorCongelado.TaxaMaximaPassosIguaisAntes deve estar em [0, 1].";

        if (!double.IsFinite(MinutosRepouso) || MinutosRepouso <= 0)
            yield return "ValorCongelado.MinutosRepouso deve ser finito e > 0.";
    }
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
