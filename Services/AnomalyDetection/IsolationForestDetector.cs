using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Integra o Isolation Forest ao ensemble da IA Marrari.
///
/// Este detector não realiza treinamento.
///
/// O fluxo é:
/// 1. procura o modelo no cache em memória;
/// 2. se não encontrar, procura o snapshot salvo em disco;
/// 3. carrega o modelo;
/// 4. extrai as características do estado atual;
/// 5. calcula o score de anomalia.
///
/// O treinamento é responsabilidade exclusiva do
/// IsolationForestTrainingService.
/// </summary>
public class IsolationForestDetector : IAnomalyDetector
{
    private readonly IsolationForestOptions _options;

    private readonly IsolationForestFeatureExtractor
        _featureExtractor;

    private readonly IsolationForestPersistenceService
        _persistenceService;

    /// <summary>
    /// Cache compartilhado entre as instâncias do detector.
    ///
    /// A chave combina cliente e tag.
    /// </summary>
    private static readonly ConcurrentDictionary<
        string,
        ModeloIsolationCache
    > CacheModelos = new();

    /// <summary>
    /// Evita que duas requisições carreguem o mesmo
    /// arquivo de modelo simultaneamente.
    /// </summary>
    private static readonly ConcurrentDictionary<
        string,
        SemaphoreSlim
    > LocksCarregamento = new();

    /// <summary>
    /// Cacheia combinações de cliente/tag para as quais nenhum
    /// modelo foi encontrado. Isso evita procurar o mesmo arquivo
    /// inexistente a cada nova leitura.
    /// </summary>
    private static readonly ConcurrentDictionary<
        string,
        byte
    > ModelosInexistentes = new();

    public IsolationForestDetector(
        IOptions<IsolationForestOptions> options,
        IsolationForestFeatureExtractor featureExtractor,
        IsolationForestPersistenceService persistenceService)
    {
        _options =
            options?.Value
            ?? throw new ArgumentNullException(
                nameof(options)
            );

        _featureExtractor =
            featureExtractor
            ?? throw new ArgumentNullException(
                nameof(featureExtractor)
            );

        _persistenceService =
            persistenceService
            ?? throw new ArgumentNullException(
                nameof(persistenceService)
            );
    }

    public string Nome => "Isolation Forest";

    public double Peso => _options.Weight;

    public DetectorResult Detectar(
        DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (
            string.IsNullOrWhiteSpace(
                input.ClienteId
            )
        )
        {
            return CriarResultadoNaoAplicavel(
                "O cliente não foi informado para o Isolation Forest."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                input.TagName
            )
        )
        {
            return CriarResultadoNaoAplicavel(
                "A tag não foi informada para o Isolation Forest."
            );
        }

        /*
         * Verifica a existência do modelo antes de preparar/copiar o
         * histórico. No benchmark sem modelos treinados, isso evita
         * milhares de operações LINQ por leitura.
         */
        ModeloIsolationCarregado? modeloCarregado;

        try
        {
            modeloCarregado = ObterModelo(
                input.ClienteId.Trim(),
                input.TagName.Trim()
            );
        }
        catch (InvalidOperationException ex)
        {
            return CriarResultadoNaoAplicavel(
                $"O Isolation Forest não pôde ser executado: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            return CriarResultadoNaoAplicavel(
                $"Falha ao executar o Isolation Forest: {ex.Message}"
            );
        }

        if (modeloCarregado == null)
        {
            return CriarResultadoNaoAplicavel(
                $"Não existe modelo Isolation Forest treinado " +
                $"para o cliente '{input.ClienteId.Trim()}' " +
                $"e a tag '{input.TagName.Trim()}'. " +
                $"Execute o endpoint de treinamento antes " +
                $"de utilizar este detector."
            );
        }

        var historicoValido = input.Historico
            .Where(ponto =>
                ponto != null
                && double.IsFinite(ponto.Valor)
            )
            .GroupBy(ponto =>
                ponto.DataHora
            )
            .Select(grupo =>
                grupo.Last()
            )
            .OrderBy(ponto =>
                ponto.DataHora
            )
            .ToList();

        if (
            historicoValido.Count
            < _options.MinimumHistoryPoints
        )
        {
            return CriarResultadoNaoAplicavel(
                $"Histórico insuficiente para executar o " +
                $"Isolation Forest. " +
                $"Necessário: {_options.MinimumHistoryPoints}. " +
                $"Disponível: {historicoValido.Count}."
            );
        }

        var inputPreparado =
            new DetectorInput
            {
                ClienteId =
                    input.ClienteId.Trim(),

                TagName =
                    input.TagName.Trim(),

                TipoTag =
                    input.TipoTag,

                ValorAtual =
                    input.ValorAtual,

                DataHoraAtual =
                    input.DataHoraAtual,

                MediaHistorica =
                    input.MediaHistorica,

                DesvioPadraoHistorico =
                    input.DesvioPadraoHistorico,

                VariacaoMinimaRelevante =
                    input.VariacaoMinimaRelevante,

                ConfiancaVariacaoMinima =
                    input.ConfiancaVariacaoMinima,

                VariacaoMinimaDefinidaPeloCliente =
                    input.VariacaoMinimaDefinidaPeloCliente,

                Historico =
                    historicoValido
            };

        try
        {
            var featureAtual =
                _featureExtractor.ExtrairAtual(
                    inputPreparado,
                    modeloCarregado.FeatureWindowSize
                );

            var vetorAtual =
                featureAtual
                    .ParaVetor()
                    .ToArray();

            if (
                vetorAtual.Length == 0
                || vetorAtual.Any(valor =>
                    !double.IsFinite(valor)
                )
            )
            {
                return CriarResultadoNaoAplicavel(
                    "Não foi possível gerar características válidas " +
                    "para executar o Isolation Forest."
                );
            }

            var previsao =
                modeloCarregado.Modelo.Avaliar(
                    vetorAtual
                );

            var threshold =
                modeloCarregado.AnomalyThreshold;

            var ehAnomalia =
                previsao.ScoreAnomalia
                >= threshold;

            var scoreNormalizado =
                NormalizarScore(
                    previsao.ScoreAnomalia,
                    threshold
                );

            var origemModelo =
                modeloCarregado.Origem switch
                {
                    OrigemModeloIsolation.Cache =>
                        "cache em memória",

                    OrigemModeloIsolation.Disco =>
                        "arquivo salvo em disco",

                    _ =>
                        "origem desconhecida"
                };

            var explicacao =
                CriarExplicacao(
                    ehAnomalia,
                    previsao.ScoreAnomalia,
                    threshold,
                    origemModelo,
                    modeloCarregado.VersaoModelo
                );

            return new DetectorResult
            {
                Aplicavel = true,

                EhAnomalia =
                    ehAnomalia,

                Score =
                    previsao.ScoreAnomalia,

                ScoreNormalizado =
                    scoreNormalizado,

                Peso =
                    Peso,

                /*
                 * O Isolation Forest não utiliza limites inferior
                 * e superior como os detectores estatísticos.
                 */
                LimiteInferior = 0,

                LimiteSuperior = 0,

                Media =
                    inputPreparado.MediaHistorica,

                DesvioPadrao =
                    inputPreparado.DesvioPadraoHistorico,

                Detector =
                    Nome,

                Explicacao =
                    explicacao,

                Metricas =
                    new Dictionary<string, double>
                    {
                        ["scoreIsolation"] =
                            previsao.ScoreAnomalia,

                        ["threshold"] =
                            threshold,

                        ["quantidadeArvores"] =
                            previsao.QuantidadeArvores,

                        ["tamanhoAmostra"] =
                            previsao.TamanhoAmostra,

                        ["profundidadeMaxima"] =
                            previsao.ProfundidadeMaxima,

                        ["comprimentoMedioCaminho"] =
                            previsao.ComprimentoMedioCaminho,

                        ["comprimentoMinimoCaminho"] =
                            previsao.ComprimentoMinimoCaminho,

                        ["comprimentoMaximoCaminho"] =
                            previsao.ComprimentoMaximoCaminho,

                        ["quantidadeCaracteristicas"] =
                            previsao.QuantidadeCaracteristicas,

                        ["featureWindowSize"] =
                            modeloCarregado.FeatureWindowSize,

                        ["versaoModelo"] =
                            modeloCarregado.VersaoModelo,

                        ["quantidadePontosTreinamento"] =
                            modeloCarregado
                                .QuantidadePontosTreinamento,

                        ["modeloVeioDoCache"] =
                            modeloCarregado.Origem
                            == OrigemModeloIsolation.Cache
                                ? 1
                                : 0,

                        ["modeloVeioDoDisco"] =
                            modeloCarregado.Origem
                            == OrigemModeloIsolation.Disco
                                ? 1
                                : 0,

                        /*
                         * Este valor será sempre zero, pois o detector
                         * não realiza mais treinamento.
                         */
                        ["modeloTreinadoAgora"] = 0
                    }
            };
        }
        catch (InvalidOperationException ex)
        {
            return CriarResultadoNaoAplicavel(
                $"O Isolation Forest não pôde ser executado: " +
                $"{ex.Message}"
            );
        }
        catch (Exception ex)
        {
            return CriarResultadoNaoAplicavel(
                $"Falha ao executar o Isolation Forest: " +
                $"{ex.Message}"
            );
        }
    }

    /// <summary>
    /// Obtém o modelo pelo cache ou pelo snapshot salvo.
    ///
    /// Este método nunca treina um modelo novo.
    /// </summary>
    private ModeloIsolationCarregado? ObterModelo(
        string clienteId,
        string tagName)
    {
        var chave =
            CriarChaveModelo(
                clienteId,
                tagName
            );

        /*
         * Se já foi confirmado que não existe modelo para esta
         * combinação de cliente e tag, não tenta acessar o disco
         * novamente.
         */
        if (ModelosInexistentes.ContainsKey(chave))
        {
            return null;
        }

        /*
         * Primeiro tenta reutilizar o modelo da memória.
         */
        if (
            CacheModelos.TryGetValue(
                chave,
                out var cache
            )
            && ModeloEmCacheValido(cache)
        )
        {
            return ConverterCacheParaModeloCarregado(
                cache
            );
        }

        /*
         * Se havia um cache incompatível, ele é removido.
         */
        CacheModelos.TryRemove(
            chave,
            out _
        );

        var trava =
            LocksCarregamento.GetOrAdd(
                chave,
                _ => new SemaphoreSlim(1, 1)
            );

        trava.Wait();

        try
        {
            /*
             * Outra requisição pode ter carregado o modelo
             * enquanto esta aguardava pela trava.
             */
            if (
                CacheModelos.TryGetValue(
                    chave,
                    out cache
                )
                && ModeloEmCacheValido(cache)
            )
            {
                return ConverterCacheParaModeloCarregado(
                    cache
                );
            }

            var snapshot =
                _persistenceService
                    .CarregarAsync(
                        clienteId,
                        tagName
                    )
                    .GetAwaiter()
                    .GetResult();

            if (snapshot == null)
            {
                /*
                 * Registra a ausência em memória para que as próximas
                 * leituras da mesma tag não tentem carregar novamente
                 * um arquivo que já sabemos que não existe.
                 */
                ModelosInexistentes.TryAdd(
                    chave,
                    0
                );

                return null;
            }

            ValidarSnapshot(
                snapshot,
                clienteId,
                tagName
            );

            var modelo =
                IsolationForestModel
                    .CarregarSnapshot(
                        snapshot
                    );

            var novoCache =
                new ModeloIsolationCache
                {
                    Modelo =
                        modelo,

                    FeatureWindowSize =
                        snapshot.FeatureWindowSize,

                    AnomalyThreshold =
                        snapshot.AnomalyThreshold,

                    DataTreinamento =
                        snapshot.DataTreinamento,

                    VersaoModelo =
                        snapshot.VersaoModelo,

                    QuantidadePontosTreinamento =
                        snapshot
                            .QuantidadePontosTreinamento
                };

            CacheModelos[chave] =
                novoCache;

            /*
             * Caso a ausência desse modelo tenha sido cacheada antes,
             * remove a marcação agora que um snapshot válido existe.
             */
            ModelosInexistentes.TryRemove(
                chave,
                out _
            );

            Console.WriteLine(
                $"Isolation Forest carregado do disco: " +
                $"{clienteId}/{tagName}. " +
                $"Versão: {snapshot.VersaoModelo}. " +
                $"Treinamento: " +
                $"{snapshot.DataTreinamento:yyyy-MM-dd HH:mm:ss}."
            );

            return new ModeloIsolationCarregado
            {
                Modelo =
                    modelo,

                FeatureWindowSize =
                    snapshot.FeatureWindowSize,

                AnomalyThreshold =
                    snapshot.AnomalyThreshold,

                DataTreinamento =
                    snapshot.DataTreinamento,

                VersaoModelo =
                    snapshot.VersaoModelo,

                QuantidadePontosTreinamento =
                    snapshot
                        .QuantidadePontosTreinamento,

                Origem =
                    OrigemModeloIsolation.Disco
            };
        }
        finally
        {
            trava.Release();
        }
    }

    /// <summary>
    /// Valida se o snapshot pode ser utilizado pelo detector.
    ///
    /// Um snapshot inválido não é apagado automaticamente.
    /// Dessa forma, nenhum arquivo de modelo é removido durante
    /// uma previsão.
    /// </summary>
    private void ValidarSnapshot(
        IsolationForestSnapshot snapshot,
        string clienteId,
        string tagName)
    {
        if (
            string.IsNullOrWhiteSpace(
                snapshot.ClienteId
            )
            || !string.Equals(
                snapshot.ClienteId,
                clienteId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                "O cliente registrado no arquivo do modelo " +
                "não corresponde ao cliente solicitado."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                snapshot.TagName
            )
            || !string.Equals(
                snapshot.TagName,
                tagName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                "A tag registrada no arquivo do modelo " +
                "não corresponde à tag solicitada."
            );
        }

        if (snapshot.FeatureWindowSize < 3)
        {
            throw new InvalidOperationException(
                "O tamanho da janela de características do modelo " +
                "é inválido."
            );
        }

        if (
            !double.IsFinite(snapshot.AnomalyThreshold) ||
            snapshot.AnomalyThreshold <= 0 ||
            snapshot.AnomalyThreshold >= 1
        )
        {
            throw new InvalidOperationException(
                "O limite de anomalia salvo no modelo é inválido."
            );
        }

        if (snapshot.QuantidadeArvores <= 0)
        {
            throw new InvalidOperationException(
                "O modelo salvo não possui árvores válidas."
            );
        }

        if (
            snapshot.QuantidadeCaracteristicas
            != 9
        )
        {
            throw new InvalidOperationException(
                $"O modelo possui " +
                $"{snapshot.QuantidadeCaracteristicas} características, " +
                $"mas o detector atual espera 9."
            );
        }

        if (snapshot.VersaoModelo <= 0)
        {
            throw new InvalidOperationException(
                "A versão registrada no modelo é inválida."
            );
        }

        if (
            snapshot.QuantidadePontosTreinamento
            <= 0
        )
        {
            throw new InvalidOperationException(
                "O modelo não possui uma quantidade válida " +
                "de pontos de treinamento."
            );
        }
    }

    /// <summary>
    /// Verifica se o modelo mantido na memória continua válido.
    /// </summary>
    private static bool ModeloEmCacheValido(
        ModeloIsolationCache cache)
    {
        if (cache.Modelo == null)
        {
            return false;
        }

        if (cache.FeatureWindowSize <= 0)
        {
            return false;
        }

        if (
            !double.IsFinite(cache.AnomalyThreshold) ||
            cache.AnomalyThreshold <= 0 ||
            cache.AnomalyThreshold >= 1
        )
        {
            return false;
        }

        if (
            cache.Modelo.QuantidadeArvores
            <= 0
        )
        {
            return false;
        }

        if (cache.VersaoModelo <= 0)
        {
            return false;
        }

        return true;
    }

    private static ModeloIsolationCarregado
        ConverterCacheParaModeloCarregado(
            ModeloIsolationCache cache)
    {
        return new ModeloIsolationCarregado
        {
            Modelo =
                cache.Modelo,

            FeatureWindowSize =
                cache.FeatureWindowSize,

            AnomalyThreshold =
                cache.AnomalyThreshold,

            DataTreinamento =
                cache.DataTreinamento,

            VersaoModelo =
                cache.VersaoModelo,

            QuantidadePontosTreinamento =
                cache.QuantidadePontosTreinamento,

            Origem =
                OrigemModeloIsolation.Cache
        };
    }

    private static string CriarExplicacao(
        bool ehAnomalia,
        double scoreIsolation,
        double threshold,
        string origemModelo,
        int versaoModelo)
    {
        if (ehAnomalia)
        {
            return
                $"O padrão atual foi isolado rapidamente pelas árvores. " +
                $"O score de isolamento foi " +
                $"{scoreIsolation:F4}, acima do limite " +
                $"{threshold:F4}. " +
                $"Foi utilizado o modelo versão {versaoModelo}, " +
                $"obtido por {origemModelo}.";
        }

        return
            $"O padrão atual apresentou score de isolamento " +
            $"{scoreIsolation:F4}, abaixo do limite " +
            $"{threshold:F4}. " +
            $"Foi utilizado o modelo versão {versaoModelo}, " +
            $"obtido por {origemModelo}.";
    }

    private DetectorResult CriarResultadoNaoAplicavel(
        string explicacao)
    {
        return new DetectorResult
        {
            Aplicavel = false,

            EhAnomalia = false,

            Score = 0,

            ScoreNormalizado = 0,

            Detector =
                Nome,

            Peso =
                Peso,

            Explicacao =
                explicacao,

            Metricas =
                new Dictionary<string, double>
                {
                    ["modeloDisponivel"] = 0,
                    ["modeloTreinadoAgora"] = 0
                }
        };
    }

    private double NormalizarScore(
        double scoreIsolation,
        double threshold)
    {
        var expoente =
            -_options.NormalizationSlope
            * (
                scoreIsolation
                - threshold
            );

        /*
         * Protege contra overflow numérico no Math.Exp.
         */
        expoente =
            Math.Clamp(
                expoente,
                -700,
                700
            );

        var resultado =
            100.0
            / (
                1.0
                + Math.Exp(expoente)
            );

        return Math.Clamp(
            resultado,
            0,
            100
        );
    }

    private static string CriarChaveModelo(
        string clienteId,
        string tagName)
    {
        return
            $"{clienteId.Trim().ToUpperInvariant()}::" +
            $"{tagName.Trim().ToUpperInvariant()}";
    }

    /// <summary>
    /// Remove da memória o modelo de uma tag.
    ///
    /// O arquivo salvo em disco não é removido.
    ///
    /// Deve ser chamado pelo serviço de treinamento depois que
    /// uma nova versão for salva, para que a próxima previsão
    /// carregue o snapshot atualizado.
    /// </summary>
    public static void RemoverDoCache(
        string clienteId,
        string tagName)
    {
        if (
            string.IsNullOrWhiteSpace(
                clienteId
            )
            || string.IsNullOrWhiteSpace(
                tagName
            )
        )
        {
            return;
        }

        var chave =
            CriarChaveModelo(
                clienteId,
                tagName
            );

        CacheModelos.TryRemove(
            chave,
            out _
        );

        /*
         * Permite que a próxima detecção tente carregar novamente
         * o modelo, inclusive após um novo treinamento.
         */
        ModelosInexistentes.TryRemove(
            chave,
            out _
        );

        Console.WriteLine(
            $"Modelo Isolation Forest removido do cache: " +
            $"{clienteId}/{tagName}"
        );
    }

    /// <summary>
    /// Limpa todos os modelos carregados na memória.
    ///
    /// Os arquivos salvos em disco permanecem intactos.
    /// </summary>
    public static void LimparCache()
    {
        CacheModelos.Clear();
        ModelosInexistentes.Clear();

        Console.WriteLine(
            "Cache do Isolation Forest foi limpo."
        );
    }
}

internal class ModeloIsolationCache
{
    public IsolationForestModel Modelo { get; set; } =
        null!;

    public int FeatureWindowSize { get; set; }

    public double AnomalyThreshold { get; set; }

    public DateTime DataTreinamento { get; set; }

    public int VersaoModelo { get; set; }

    public int QuantidadePontosTreinamento { get; set; }
}

internal class ModeloIsolationCarregado
{
    public IsolationForestModel Modelo { get; set; } =
        null!;

    public int FeatureWindowSize { get; set; }

    public double AnomalyThreshold { get; set; }

    public DateTime DataTreinamento { get; set; }

    public int VersaoModelo { get; set; }

    public int QuantidadePontosTreinamento { get; set; }

    public OrigemModeloIsolation Origem { get; set; }
}

internal enum OrigemModeloIsolation
{
    Cache = 1,

    Disco = 2
}