using System.Security.Cryptography;
using System.Text;
using IAApi.Services.AnomalyDetection;
using Microsoft.Extensions.Options;

namespace IAApi.Services.ModelTraining;

/// <summary>
/// Responsável exclusivamente pelo treinamento do Isolation Forest.
///
/// Fluxo:
/// 1. obtém os dados das janelas normais da tag;
/// 2. usa histórico recente como fallback quando não há janelas;
/// 3. prepara as características;
/// 4. treina as árvores;
/// 5. salva o snapshot em disco;
/// 6. atualiza o status do modelo no banco;
/// 7. remove o modelo antigo do cache.
/// </summary>
public class IsolationForestTrainingService
{
    private const string TipoModelo = "IsolationForest";

    private readonly DadosTreinamentoTagService _dadosTreinamentoService;
    private readonly IsolationForestFeatureExtractor _featureExtractor;
    private readonly IsolationForestPersistenceService _persistenceService;
    private readonly ModeloTagStatusService _statusService;
    private readonly IsolationForestOptions _options;

    public IsolationForestTrainingService(
        DadosTreinamentoTagService dadosTreinamentoService,
        IsolationForestFeatureExtractor featureExtractor,
        IsolationForestPersistenceService persistenceService,
        ModeloTagStatusService statusService,
        IOptions<IsolationForestOptions> options)
    {
        _dadosTreinamentoService =
            dadosTreinamentoService
            ?? throw new ArgumentNullException(
                nameof(dadosTreinamentoService));

        _featureExtractor =
            featureExtractor
            ?? throw new ArgumentNullException(
                nameof(featureExtractor));

        _persistenceService =
            persistenceService
            ?? throw new ArgumentNullException(
                nameof(persistenceService));

        _statusService =
            statusService
            ?? throw new ArgumentNullException(
                nameof(statusService));

        _options =
            options?.Value
            ?? throw new ArgumentNullException(
                nameof(options));
    }

    /// <summary>
    /// Treina e salva um novo Isolation Forest para uma tag.
    /// </summary>
    public async Task<ResultadoTreinamentoIsolationForest> TreinarAsync(
        string clienteId,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        ValidarParametros(clienteId, tagName);

        clienteId = clienteId.Trim();
        tagName = tagName.Trim();

        await _statusService.MarcarTreinandoAsync(
            clienteId,
            tagName,
            TipoModelo);

        try
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("TREINAMENTO DO ISOLATION FOREST");
            Console.WriteLine($"Cliente: {clienteId}");
            Console.WriteLine($"Tag: {tagName}");
            Console.WriteLine("========================================");

            /*
             * O serviço comum verifica se existem janelas normais.
             *
             * Se existirem:
             * usa os históricos dessas janelas.
             *
             * Se não existirem:
             * usa o histórico recente como fallback.
             */
            var dadosTreinamento =
                _dadosTreinamentoService.Obter(
                    clienteId,
                    tagName,
                    "perfil-comportamental"
                    );

            if (dadosTreinamento.Leituras == null ||
                dadosTreinamento.Leituras.Count == 0)
            {
                throw new InvalidOperationException(
                    "Nenhuma leitura foi encontrada para o treinamento.");
            }

            var leiturasValidas = dadosTreinamento.Leituras
                .Where(leitura =>
                    leitura != null &&
                    !float.IsNaN(leitura.Valor) &&
                    !float.IsInfinity(leitura.Valor))
                .GroupBy(leitura => leitura.DataHora)
                .Select(grupo => grupo.Last())
                .OrderBy(leitura => leitura.DataHora)
                .ToList();

            if (leiturasValidas.Count <
                _options.MinimumHistoryPoints)
            {
                throw new InvalidOperationException(
                    $"Histórico insuficiente para treinar o " +
                    $"Isolation Forest. Necessário: " +
                    $"{_options.MinimumHistoryPoints}. " +
                    $"Disponível: {leiturasValidas.Count}.");
            }

            var historicoDetector = leiturasValidas
                .Select(leitura =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = leitura.DataHora,
                        Valor = leitura.Valor
                    })
                .ToList();

            var valores = historicoDetector
                .Select(ponto => ponto.Valor)
                .Where(double.IsFinite)
                .ToArray();

            if (valores.Length <
                _options.MinimumHistoryPoints)
            {
                throw new InvalidOperationException(
                    "Não existem valores numéricos válidos " +
                    "suficientes para o treinamento.");
            }

            var media = valores.Average();

            var variancia = valores
                .Select(valor =>
                    Math.Pow(valor - media, 2))
                .Average();

            var desvioPadrao =
                Math.Sqrt(variancia);

            /*
             * O extrator precisa de um DetectorInput.
             *
             * ValorAtual não será usado no conjunto de treinamento,
             * mas fornecemos o último valor para manter o objeto válido.
             */
            var inputTreinamento = new DetectorInput
            {
                ClienteId = clienteId,
                TagName = tagName,
                ValorAtual = valores.Last(),
                DataHoraAtual =
                    historicoDetector.Last().DataHora,
                MediaHistorica = media,
                DesvioPadraoHistorico = desvioPadrao,
                Historico = historicoDetector
            };

            var features =
                _featureExtractor.ExtrairTreinamento(
                    inputTreinamento,
                    _options.FeatureWindowSize);

            var quantidadeMinimaFeatures = Math.Max(
                10,
                _options.MinimumHistoryPoints -
                _options.FeatureWindowSize);

            if (features.Count < quantidadeMinimaFeatures)
            {
                throw new InvalidOperationException(
                    $"Não foram produzidas características " +
                    $"suficientes para o treinamento. " +
                    $"Necessário: {quantidadeMinimaFeatures}. " +
                    $"Disponível: {features.Count}.");
            }

            var amostrasTreinamento = features
                .Select(feature =>
                    (IReadOnlyList<double>)
                    feature.ParaVetor().ToArray())
                .ToList();

            var modelo = new IsolationForestModel(
                quantidadeArvores:
                    _options.TreeCount,

                tamanhoAmostra:
                    _options.SampleSize,

                sementeAleatoria:
                    _options.RandomSeed);

            modelo.Treinar(amostrasTreinamento);

            var hashJanelas =
                CalcularHashJanelas(
                    dadosTreinamento);

            /*
             * Exporta o modelo treinado e completa
             * os metadados de rastreabilidade.
             */
            var snapshot = modelo.ExportarSnapshot(
                clienteId: clienteId,
                tagName: tagName,
                featureWindowSize:
                    _options.FeatureWindowSize,
                anomalyThreshold:
                    _options.AnomalyThreshold,
                dataTreinamento:
                    DateTime.UtcNow);

            snapshot.VersaoModelo =
                await ObterProximaVersaoAsync(
                    clienteId,
                    tagName);

            snapshot.QuantidadePontosTreinamento =
                leiturasValidas.Count;

            snapshot.HashJanelasTreinamento =
                hashJanelas;

            snapshot.UsouFallbackHistoricoRecente =
                dadosTreinamento
                    .UsouFallbackHistoricoRecente;

            snapshot.JanelasTreinamento =
                dadosTreinamento.Janelas.ToList();

            /*
             * O PersistenceService salva primeiro em arquivo
             * temporário e só depois substitui o modelo anterior.
             */
            await _persistenceService.SalvarAsync(
                snapshot,
                cancellationToken);

            /*
             * O cache antigo deve ser removido para garantir
             * que a próxima inferência carregue o novo modelo.
             */
            IsolationForestDetector.RemoverDoCache(
                clienteId,
                tagName);

            await _statusService.MarcarAtualAsync(
                clienteId,
                tagName,
                TipoModelo,
                leiturasValidas.Count,
                hashJanelas);

            var caminhoModelo =
                _persistenceService.ObterCaminhoModelo(
                    clienteId,
                    tagName);

            Console.WriteLine(
                $"Isolation Forest treinado com sucesso.");

            Console.WriteLine(
                $"Leituras usadas: {leiturasValidas.Count}");

            Console.WriteLine(
                $"Features produzidas: {features.Count}");

            Console.WriteLine(
                $"Árvores: {modelo.QuantidadeArvores}");

            Console.WriteLine(
                $"Fallback recente: " +
                $"{dadosTreinamento.UsouFallbackHistoricoRecente}");

            Console.WriteLine(
                $"Modelo salvo em: {caminhoModelo}");

            Console.WriteLine("========================================");
            Console.WriteLine();

            return new ResultadoTreinamentoIsolationForest
            {
                Sucesso = true,
                ClienteId = clienteId,
                TagName = tagName,
                TipoModelo = TipoModelo,
                VersaoModelo = snapshot.VersaoModelo,
                DataTreinamento = snapshot.DataTreinamento,

                QuantidadePontosTreinamento =
                    leiturasValidas.Count,

                QuantidadeFeatures =
                    features.Count,

                QuantidadeArvores =
                    modelo.QuantidadeArvores,

                TamanhoAmostra =
                    modelo.TamanhoAmostraUtilizado,

                QuantidadeCaracteristicas =
                    modelo.QuantidadeCaracteristicas,

                ProfundidadeMaxima =
                    modelo.ProfundidadeMaxima,

                QuantidadeJanelas =
                    dadosTreinamento.Janelas.Count,

                UsouFallbackHistoricoRecente =
                    dadosTreinamento
                        .UsouFallbackHistoricoRecente,

                HashJanelasTreinamento =
                    hashJanelas,

                CaminhoModelo =
                    caminhoModelo,

                Mensagem =
                    dadosTreinamento
                        .UsouFallbackHistoricoRecente
                        ? "Modelo treinado usando o histórico recente, pois não existem janelas normais ativas."
                        : "Modelo treinado usando as janelas de funcionamento normal configuradas para a tag."
            };
        }
        catch (Exception ex)
        {
            await _statusService.MarcarErroAsync(
                clienteId,
                tagName,
                TipoModelo,
                ex.Message);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("ERRO NO TREINAMENTO DO ISOLATION FOREST");
            Console.WriteLine($"Cliente: {clienteId}");
            Console.WriteLine($"Tag: {tagName}");
            Console.WriteLine($"Erro: {ex.Message}");
            Console.WriteLine("========================================");
            Console.WriteLine();

            throw;
        }
    }

    /// <summary>
    /// Cria um hash estável das janelas utilizadas.
    ///
    /// Quando uma janela é adicionada, removida ou alterada,
    /// o hash muda, indicando que o modelo está desatualizado.
    /// </summary>
    private static string CalcularHashJanelas(
        ResultadoDadosTreinamento dados)
    {
        var builder = new StringBuilder();

        builder.Append(
            dados.UsouFallbackHistoricoRecente
                ? "FALLBACK|"
                : "JANELAS|");

        foreach (var janela in dados.Janelas
                     .OrderBy(janela => janela.Inicio)
                     .ThenBy(janela => janela.Fim)
                     .ThenBy(janela => janela.JanelaId))
        {
            builder.Append(janela.JanelaId);
            builder.Append('|');

            builder.Append(
                janela.Inicio.ToUniversalTime()
                    .ToString("O"));

            builder.Append('|');

            builder.Append(
                janela.Fim.ToUniversalTime()
                    .ToString("O"));

            builder.Append('|');

            builder.Append(
                janela.Descricao?.Trim() ?? "");

            builder.Append('|');

            builder.Append(
                janela.ModoOperacao?.Trim() ?? "");

            builder.Append(';');
        }

        var bytes =
            Encoding.UTF8.GetBytes(
                builder.ToString());

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    /*
     * Nesta primeira versão, a versão é baseada no snapshot salvo.
     *
     * Se não houver arquivo anterior, começa em 1.
     */
    private async Task<int> ObterProximaVersaoAsync(
        string clienteId,
        string tagName)
    {
        var snapshotAtual =
            await _persistenceService.CarregarAsync(
                clienteId,
                tagName);

        if (snapshotAtual == null)
        {
            return 1;
        }

        return Math.Max(
            1,
            snapshotAtual.VersaoModelo + 1);
    }

    private static void ValidarParametros(
        string clienteId,
        string tagName)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            throw new ArgumentException(
                "O identificador do cliente é obrigatório.",
                nameof(clienteId));
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException(
                "O nome da tag é obrigatório.",
                nameof(tagName));
        }
    }
}

/// <summary>
/// Informações retornadas depois do treinamento.
/// </summary>
public class ResultadoTreinamentoIsolationForest
{
    public bool Sucesso { get; set; }

    public string ClienteId { get; set; } = "";

    public string TagName { get; set; } = "";

    public string TipoModelo { get; set; } = "";

    public int VersaoModelo { get; set; }

    public DateTime DataTreinamento { get; set; }

    public int QuantidadePontosTreinamento { get; set; }

    public int QuantidadeFeatures { get; set; }

    public int QuantidadeArvores { get; set; }

    public int TamanhoAmostra { get; set; }

    public int QuantidadeCaracteristicas { get; set; }

    public int ProfundidadeMaxima { get; set; }

    public int QuantidadeJanelas { get; set; }

    public bool UsouFallbackHistoricoRecente { get; set; }

    public string HashJanelasTreinamento { get; set; } = "";

    public string CaminhoModelo { get; set; } = "";

    public string Mensagem { get; set; } = "";
}