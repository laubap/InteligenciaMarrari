namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Modelo completo do Isolation Forest.
///
/// Responsabilidades:
/// - treinar várias árvores de isolamento;
/// - avaliar novas amostras;
/// - calcular o score clássico de anomalia;
/// - exportar o modelo para um snapshot serializável;
/// - reconstruir o modelo sem realizar novo treinamento.
/// </summary>
public class IsolationForestModel
{
    private readonly List<IsolationTree> _arvores = new();

    private readonly int _quantidadeArvoresConfigurada;
    private readonly int _tamanhoAmostraConfigurado;
    private readonly int? _sementeAleatoria;

    private int _tamanhoAmostraUtilizado;
    private int _quantidadeCaracteristicas;
    private int _profundidadeMaxima;

    /// <summary>
    /// Construtor usado para criar um modelo que será treinado.
    /// </summary>
    public IsolationForestModel(
        int quantidadeArvores = 100,
        int tamanhoAmostra = 256,
        int? sementeAleatoria = null)
    {
        if (quantidadeArvores <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantidadeArvores),
                "A quantidade de árvores deve ser maior que zero."
            );
        }

        if (tamanhoAmostra < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tamanhoAmostra),
                "O tamanho da amostra deve ser de pelo menos dois pontos."
            );
        }

        _quantidadeArvoresConfigurada =
            quantidadeArvores;

        _tamanhoAmostraConfigurado =
            tamanhoAmostra;

        _sementeAleatoria =
            sementeAleatoria;
    }

    /// <summary>
    /// Construtor interno usado ao reconstruir um modelo salvo.
    /// </summary>
    private IsolationForestModel(
        IReadOnlyCollection<IsolationTree> arvores,
        int tamanhoAmostraUtilizado,
        int quantidadeCaracteristicas,
        int profundidadeMaxima,
        int? sementeAleatoria = null)
    {
        if (arvores == null)
        {
            throw new ArgumentNullException(
                nameof(arvores)
            );
        }

        if (arvores.Count == 0)
        {
            throw new ArgumentException(
                "O modelo carregado precisa possuir pelo menos uma árvore.",
                nameof(arvores)
            );
        }

        if (tamanhoAmostraUtilizado < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tamanhoAmostraUtilizado)
            );
        }

        if (quantidadeCaracteristicas <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantidadeCaracteristicas)
            );
        }

        if (profundidadeMaxima < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profundidadeMaxima)
            );
        }

        _arvores.AddRange(arvores);

        _quantidadeArvoresConfigurada =
            arvores.Count;

        _tamanhoAmostraConfigurado =
            tamanhoAmostraUtilizado;

        _tamanhoAmostraUtilizado =
            tamanhoAmostraUtilizado;

        _quantidadeCaracteristicas =
            quantidadeCaracteristicas;

        _profundidadeMaxima =
            profundidadeMaxima;

        _sementeAleatoria =
            sementeAleatoria;
    }

    public bool Treinado =>
        _arvores.Count > 0;

    public int QuantidadeArvores =>
        _arvores.Count;

    public int QuantidadeArvoresConfigurada =>
        _quantidadeArvoresConfigurada;

    public int TamanhoAmostraConfigurado =>
        _tamanhoAmostraConfigurado;

    public int TamanhoAmostraUtilizado =>
        _tamanhoAmostraUtilizado;

    public int QuantidadeCaracteristicas =>
        _quantidadeCaracteristicas;

    public int ProfundidadeMaxima =>
        _profundidadeMaxima;

    /// <summary>
    /// Treina o Isolation Forest usando os vetores informados.
    ///
    /// Cada vetor representa uma leitura histórica transformada
    /// em características numéricas.
    /// </summary>
    public void Treinar(
        IReadOnlyList<IReadOnlyList<double>> amostras)
    {
        ValidarAmostrasTreinamento(
            amostras
        );

        _arvores.Clear();

        _quantidadeCaracteristicas =
            amostras[0].Count;

        _tamanhoAmostraUtilizado =
            Math.Min(
                _tamanhoAmostraConfigurado,
                amostras.Count
            );

        /*
         * Profundidade máxima clássica:
         *
         * ceil(log2(tamanho da amostra))
         */
        _profundidadeMaxima =
            (int)Math.Ceiling(
                Math.Log2(
                    _tamanhoAmostraUtilizado
                )
            );

        var randomPrincipal =
            _sementeAleatoria.HasValue
                ? new Random(
                    _sementeAleatoria.Value
                )
                : new Random();

        for (
            var indiceArvore = 0;
            indiceArvore <
                _quantidadeArvoresConfigurada;
            indiceArvore++
        )
        {
            /*
             * Cada árvore recebe um Random independente,
             * derivado da semente principal.
             */
            var sementeArvore =
                randomPrincipal.Next();

            var randomArvore =
                new Random(
                    sementeArvore
                );

            var amostraArvore =
                SelecionarAmostraSemReposicao(
                    amostras,
                    _tamanhoAmostraUtilizado,
                    randomArvore
                );

            var arvore =
                new IsolationTree(
                    amostraArvore,
                    _profundidadeMaxima,
                    randomArvore
                );

            _arvores.Add(
                arvore
            );
        }
    }

    /// <summary>
    /// Avalia uma nova amostra usando as árvores treinadas.
    /// </summary>
    public IsolationForestPrediction Avaliar(
        IReadOnlyList<double> caracteristicas)
    {
        ValidarModeloTreinado();

        ValidarAmostraAvaliacao(
            caracteristicas
        );

        var comprimentos =
            new double[_arvores.Count];

        for (
            var indice = 0;
            indice < _arvores.Count;
            indice++
        )
        {
            comprimentos[indice] =
                _arvores[indice]
                    .CalcularComprimentoCaminho(
                        caracteristicas
                    );
        }

        var comprimentoMedio =
            comprimentos.Average();

        var scoreAnomalia =
            IsolationForestMath
                .CalcularScoreAnomalia(
                    comprimentoMedio,
                    _tamanhoAmostraUtilizado
                );

        return new IsolationForestPrediction
        {
            ScoreAnomalia =
                scoreAnomalia,

            ScoreNormalizado =
                Math.Clamp(
                    scoreAnomalia * 100.0,
                    0,
                    100
                ),

            ComprimentoMedioCaminho =
                comprimentoMedio,

            ComprimentoMinimoCaminho =
                comprimentos.Min(),

            ComprimentoMaximoCaminho =
                comprimentos.Max(),

            QuantidadeArvores =
                _arvores.Count,

            TamanhoAmostra =
                _tamanhoAmostraUtilizado,

            ProfundidadeMaxima =
                _profundidadeMaxima,

            QuantidadeCaracteristicas =
                _quantidadeCaracteristicas
        };
    }

    /// <summary>
    /// Avalia várias amostras sem reconstruir o modelo.
    /// </summary>
    public IReadOnlyList<IsolationForestPrediction> AvaliarLote(
        IReadOnlyList<IReadOnlyList<double>> amostras)
    {
        ValidarModeloTreinado();

        if (amostras == null)
        {
            throw new ArgumentNullException(
                nameof(amostras)
            );
        }

        var resultados =
            new List<IsolationForestPrediction>(
                amostras.Count
            );

        foreach (var amostra in amostras)
        {
            resultados.Add(
                Avaliar(amostra)
            );
        }

        return resultados;
    }

    /// <summary>
    /// Converte o modelo treinado em um objeto serializável.
    ///
    /// Esse objeto poderá ser salvo pelo
    /// IsolationForestPersistenceService.
    /// </summary>
    public IsolationForestSnapshot ExportarSnapshot(
        string clienteId,
        string tagName,
        int featureWindowSize,
        double anomalyThreshold,
        DateTime? dataTreinamento = null)
    {
        ValidarModeloTreinado();

        if (string.IsNullOrWhiteSpace(clienteId))
        {
            throw new ArgumentException(
                "O cliente do modelo é obrigatório.",
                nameof(clienteId)
            );
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException(
                "A tag do modelo é obrigatória.",
                nameof(tagName)
            );
        }

        if (featureWindowSize < 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(featureWindowSize),
                "A janela de características deve possuir pelo menos três pontos."
            );
        }

        if (
            !double.IsFinite(anomalyThreshold) ||
            anomalyThreshold <= 0 ||
            anomalyThreshold >= 1
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(anomalyThreshold),
                "O limite do Isolation Forest deve estar entre zero e um."
            );
        }

        return new IsolationForestSnapshot
        {
            ClienteId =
                clienteId,

            TagName =
                tagName,

            DataTreinamento =
                dataTreinamento ??
                DateTime.UtcNow,

            QuantidadeArvores =
                _arvores.Count,

            TamanhoAmostra =
                _tamanhoAmostraUtilizado,

            QuantidadeCaracteristicas =
                _quantidadeCaracteristicas,

            ProfundidadeMaxima =
                _profundidadeMaxima,

            FeatureWindowSize =
                featureWindowSize,

            AnomalyThreshold =
                anomalyThreshold,

            Arvores =
                _arvores
                    .Select(
                        arvore =>
                            arvore.ExportarSnapshot()
                    )
                    .ToList()
        };
    }

    /// <summary>
    /// Reconstrói um Isolation Forest salvo anteriormente.
    ///
    /// Nenhum treinamento é realizado neste método.
    /// </summary>
    public static IsolationForestModel CarregarSnapshot(
        IsolationForestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot
        );

        ValidarSnapshot(
            snapshot
        );

        var arvores =
            snapshot.Arvores
                .Select(
                    IsolationTree.CarregarSnapshot
                )
                .ToList();

        return new IsolationForestModel(
            arvores:
                arvores,

            tamanhoAmostraUtilizado:
                snapshot.TamanhoAmostra,

            quantidadeCaracteristicas:
                snapshot.QuantidadeCaracteristicas,

            profundidadeMaxima:
                snapshot.ProfundidadeMaxima
        );
    }

    private static IReadOnlyList<IReadOnlyList<double>>
        SelecionarAmostraSemReposicao(
            IReadOnlyList<IReadOnlyList<double>> amostras,
            int quantidade,
            Random random)
    {
        if (quantidade >= amostras.Count)
        {
            return amostras
                .Select(
                    amostra =>
                        (IReadOnlyList<double>)
                        amostra.ToArray()
                )
                .ToList();
        }

        /*
         * Embaralhamento parcial de Fisher-Yates.
         */
        var indices =
            Enumerable.Range(
                0,
                amostras.Count
            )
            .ToArray();

        for (
            var indice = 0;
            indice < quantidade;
            indice++
        )
        {
            var indiceSorteado =
                random.Next(
                    indice,
                    indices.Length
                );

            (
                indices[indice],
                indices[indiceSorteado]
            ) =
            (
                indices[indiceSorteado],
                indices[indice]
            );
        }

        var selecionadas =
            new List<IReadOnlyList<double>>(
                quantidade
            );

        for (
            var indice = 0;
            indice < quantidade;
            indice++
        )
        {
            selecionadas.Add(
                amostras[
                    indices[indice]
                ].ToArray()
            );
        }

        return selecionadas;
    }

    private static void ValidarAmostrasTreinamento(
        IReadOnlyList<IReadOnlyList<double>> amostras)
    {
        if (amostras == null)
        {
            throw new ArgumentNullException(
                nameof(amostras)
            );
        }

        if (amostras.Count < 2)
        {
            throw new ArgumentException(
                "O Isolation Forest precisa de pelo menos " +
                "duas amostras para treinamento.",
                nameof(amostras)
            );
        }

        if (amostras[0] == null)
        {
            throw new ArgumentException(
                "A primeira amostra é nula.",
                nameof(amostras)
            );
        }

        var quantidadeCaracteristicas =
            amostras[0].Count;

        if (quantidadeCaracteristicas == 0)
        {
            throw new ArgumentException(
                "As amostras precisam possuir pelo menos " +
                "uma característica.",
                nameof(amostras)
            );
        }

        for (
            var indiceAmostra = 0;
            indiceAmostra < amostras.Count;
            indiceAmostra++
        )
        {
            var amostra =
                amostras[indiceAmostra];

            if (amostra == null)
            {
                throw new ArgumentException(
                    $"A amostra {indiceAmostra} é nula.",
                    nameof(amostras)
                );
            }

            if (
                amostra.Count !=
                quantidadeCaracteristicas
            )
            {
                throw new ArgumentException(
                    $"A amostra {indiceAmostra} possui " +
                    $"{amostra.Count} características, mas " +
                    $"eram esperadas {quantidadeCaracteristicas}.",
                    nameof(amostras)
                );
            }

            for (
                var indiceCaracteristica = 0;
                indiceCaracteristica <
                    amostra.Count;
                indiceCaracteristica++
            )
            {
                if (
                    !double.IsFinite(
                        amostra[
                            indiceCaracteristica
                        ]
                    )
                )
                {
                    throw new ArgumentException(
                        $"A amostra {indiceAmostra} possui um " +
                        $"valor inválido na característica " +
                        $"{indiceCaracteristica}.",
                        nameof(amostras)
                    );
                }
            }
        }
    }

    private void ValidarAmostraAvaliacao(
        IReadOnlyList<double> caracteristicas)
    {
        if (caracteristicas == null)
        {
            throw new ArgumentNullException(
                nameof(caracteristicas)
            );
        }

        if (
            caracteristicas.Count !=
            _quantidadeCaracteristicas
        )
        {
            throw new ArgumentException(
                $"A amostra possui " +
                $"{caracteristicas.Count} características, " +
                $"mas o modelo espera " +
                $"{_quantidadeCaracteristicas}.",
                nameof(caracteristicas)
            );
        }

        for (
            var indice = 0;
            indice < caracteristicas.Count;
            indice++
        )
        {
            if (
                !double.IsFinite(
                    caracteristicas[indice]
                )
            )
            {
                throw new ArgumentException(
                    $"A característica de índice {indice} " +
                    "possui um valor inválido.",
                    nameof(caracteristicas)
                );
            }
        }
    }

    private static void ValidarSnapshot(
        IsolationForestSnapshot snapshot)
    {
        if (
            string.IsNullOrWhiteSpace(
                snapshot.ClienteId
            )
        )
        {
            throw new InvalidOperationException(
                "O snapshot não possui cliente."
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                snapshot.TagName
            )
        )
        {
            throw new InvalidOperationException(
                "O snapshot não possui tag."
            );
        }

        if (
            snapshot.Arvores == null ||
            snapshot.Arvores.Count == 0
        )
        {
            throw new InvalidOperationException(
                "O snapshot não possui árvores."
            );
        }

        if (
            snapshot.QuantidadeArvores !=
            snapshot.Arvores.Count
        )
        {
            throw new InvalidOperationException(
                "A quantidade de árvores informada no snapshot " +
                "não corresponde à quantidade armazenada."
            );
        }

        if (snapshot.TamanhoAmostra < 2)
        {
            throw new InvalidOperationException(
                "O snapshot possui um tamanho de amostra inválido."
            );
        }

        if (
            snapshot.QuantidadeCaracteristicas <= 0
        )
        {
            throw new InvalidOperationException(
                "O snapshot possui uma quantidade inválida " +
                "de características."
            );
        }

        if (snapshot.ProfundidadeMaxima < 0)
        {
            throw new InvalidOperationException(
                "O snapshot possui profundidade máxima inválida."
            );
        }

        if (snapshot.FeatureWindowSize < 3)
        {
            throw new InvalidOperationException(
                "O snapshot possui uma janela de características inválida."
            );
        }

        if (
            !double.IsFinite(
                snapshot.AnomalyThreshold
            ) ||
            snapshot.AnomalyThreshold <= 0 ||
            snapshot.AnomalyThreshold >= 1
        )
        {
            throw new InvalidOperationException(
                "O snapshot possui um limite de anomalia inválido."
            );
        }

        foreach (var arvore in snapshot.Arvores)
        {
            if (arvore == null)
            {
                throw new InvalidOperationException(
                    "O snapshot possui uma árvore nula."
                );
            }

            if (
                arvore.QuantidadeCaracteristicas !=
                snapshot.QuantidadeCaracteristicas
            )
            {
                throw new InvalidOperationException(
                    "Uma árvore possui quantidade de " +
                    "características diferente do modelo."
                );
            }

            if (arvore.Raiz == null)
            {
                throw new InvalidOperationException(
                    "Uma árvore do snapshot não possui raiz."
                );
            }
        }
    }

    private void ValidarModeloTreinado()
    {
        if (!Treinado)
        {
            throw new InvalidOperationException(
                "O modelo Isolation Forest ainda não foi treinado."
            );
        }
    }
}

/// <summary>
/// Resultado produzido pelo Isolation Forest para uma amostra.
/// </summary>
public class IsolationForestPrediction
{
    /// <summary>
    /// Score clássico do Isolation Forest, normalmente entre zero e um.
    /// Quanto maior, mais rara tende a ser a amostra.
    /// </summary>
    public double ScoreAnomalia { get; set; }

    public double ScoreNormalizado { get; set; }

    public double ComprimentoMedioCaminho { get; set; }

    public double ComprimentoMinimoCaminho { get; set; }

    public double ComprimentoMaximoCaminho { get; set; }

    public int QuantidadeArvores { get; set; }

    public int TamanhoAmostra { get; set; }

    public int ProfundidadeMaxima { get; set; }

    public int QuantidadeCaracteristicas { get; set; }
}