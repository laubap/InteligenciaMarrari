namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Representa uma árvore de isolamento.
///
/// A árvore é construída por divisões aleatórias das características.
/// Pontos raros normalmente são isolados em menos divisões,
/// resultando em um caminho menor.
/// </summary>
public class IsolationTree
{
    private readonly Random _random;

    public IsolationTreeNode Raiz { get; }

    public int ProfundidadeMaxima { get; }

    public int QuantidadeCaracteristicas { get; }

    public int QuantidadeAmostras { get; }

    /// <summary>
    /// Construtor usado durante o treinamento de uma nova árvore.
    /// </summary>
    public IsolationTree(
        IReadOnlyList<IReadOnlyList<double>> amostras,
        int profundidadeMaxima,
        Random random)
    {
        if (amostras == null)
        {
            throw new ArgumentNullException(
                nameof(amostras)
            );
        }

        if (random == null)
        {
            throw new ArgumentNullException(
                nameof(random)
            );
        }

        if (amostras.Count == 0)
        {
            throw new ArgumentException(
                "A árvore precisa receber pelo menos uma amostra.",
                nameof(amostras)
            );
        }

        if (profundidadeMaxima < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profundidadeMaxima),
                "A profundidade máxima não pode ser negativa."
            );
        }

        var quantidadeCaracteristicas =
            amostras[0]?.Count ?? 0;

        if (quantidadeCaracteristicas == 0)
        {
            throw new ArgumentException(
                "As amostras precisam possuir ao menos uma característica.",
                nameof(amostras)
            );
        }

        ValidarAmostras(
            amostras,
            quantidadeCaracteristicas
        );

        _random = random;

        ProfundidadeMaxima =
            profundidadeMaxima;

        QuantidadeCaracteristicas =
            quantidadeCaracteristicas;

        QuantidadeAmostras =
            amostras.Count;

        Raiz = ConstruirNo(
            amostras,
            profundidadeAtual: 0
        );
    }

    /// <summary>
    /// Construtor utilizado ao carregar uma árvore salva em disco.
    /// </summary>
    private IsolationTree(
        IsolationTreeNode raiz,
        int profundidadeMaxima,
        int quantidadeCaracteristicas,
        int quantidadeAmostras)
    {
        Raiz = raiz
            ?? throw new ArgumentNullException(
                nameof(raiz)
            );

        if (profundidadeMaxima < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profundidadeMaxima)
            );
        }

        if (quantidadeCaracteristicas <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantidadeCaracteristicas)
            );
        }

        if (quantidadeAmostras <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantidadeAmostras)
            );
        }

        ProfundidadeMaxima =
            profundidadeMaxima;

        QuantidadeCaracteristicas =
            quantidadeCaracteristicas;

        QuantidadeAmostras =
            quantidadeAmostras;

        /*
         * O Random não será usado durante a inferência.
         * Mesmo assim, inicializamos para manter o objeto válido.
         */
        _random = new Random();
    }

    /// <summary>
    /// Calcula quantos níveis da árvore foram necessários
    /// para isolar uma determinada amostra.
    /// </summary>
    public double CalcularComprimentoCaminho(
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
            QuantidadeCaracteristicas
        )
        {
            throw new ArgumentException(
                $"Quantidade inválida de características. " +
                $"Esperado: {QuantidadeCaracteristicas}. " +
                $"Recebido: {caracteristicas.Count}.",
                nameof(caracteristicas)
            );
        }

        foreach (var valor in caracteristicas)
        {
            if (!double.IsFinite(valor))
            {
                throw new ArgumentException(
                    "A amostra contém valores inválidos.",
                    nameof(caracteristicas)
                );
            }
        }

        return Raiz.CalcularComprimentoCaminho(
            caracteristicas
        );
    }

    private IsolationTreeNode ConstruirNo(
        IReadOnlyList<IReadOnlyList<double>> amostras,
        int profundidadeAtual)
    {
        /*
         * Interrompe a construção quando:
         *
         * 1. atingimos a profundidade máxima;
         * 2. existe apenas uma amostra;
         * 3. todas as amostras são idênticas;
         * 4. nenhuma característica permite divisão.
         */
        if (
            profundidadeAtual >= ProfundidadeMaxima ||
            amostras.Count <= 1 ||
            TodasAmostrasIguais(amostras)
        )
        {
            return IsolationTreeNode.CriarFolha(
                quantidadeAmostras:
                    amostras.Count,

                profundidade:
                    profundidadeAtual,

                quantidadeCaracteristicas:
                    QuantidadeCaracteristicas
            );
        }

        var caracteristicasDivisiveis =
            BuscarCaracteristicasDivisiveis(
                amostras
            );

        if (caracteristicasDivisiveis.Count == 0)
        {
            return IsolationTreeNode.CriarFolha(
                quantidadeAmostras:
                    amostras.Count,

                profundidade:
                    profundidadeAtual,

                quantidadeCaracteristicas:
                    QuantidadeCaracteristicas
            );
        }

        var indiceCaracteristica =
            caracteristicasDivisiveis[
                _random.Next(
                    caracteristicasDivisiveis.Count
                )
            ];

        var minimo = amostras.Min(
            amostra =>
                amostra[indiceCaracteristica]
        );

        var maximo = amostras.Max(
            amostra =>
                amostra[indiceCaracteristica]
        );

        var valorDivisao =
            GerarValorDivisao(
                minimo,
                maximo
            );

        var esquerda =
            new List<IReadOnlyList<double>>();

        var direita =
            new List<IReadOnlyList<double>>();

        foreach (var amostra in amostras)
        {
            if (
                amostra[indiceCaracteristica] <
                valorDivisao
            )
            {
                esquerda.Add(amostra);
            }
            else
            {
                direita.Add(amostra);
            }
        }

        /*
         * Proteção contra divisões inválidas que deixem
         * um dos lados sem nenhuma amostra.
         */
        if (
            esquerda.Count == 0 ||
            direita.Count == 0
        )
        {
            return IsolationTreeNode.CriarFolha(
                quantidadeAmostras:
                    amostras.Count,

                profundidade:
                    profundidadeAtual,

                quantidadeCaracteristicas:
                    QuantidadeCaracteristicas
            );
        }

        var noEsquerdo = ConstruirNo(
            esquerda,
            profundidadeAtual + 1
        );

        var noDireito = ConstruirNo(
            direita,
            profundidadeAtual + 1
        );

        return IsolationTreeNode.CriarNoInterno(
            indiceCaracteristica:
                indiceCaracteristica,

            valorDivisao:
                valorDivisao,

            quantidadeAmostras:
                amostras.Count,

            profundidade:
                profundidadeAtual,

            quantidadeCaracteristicas:
                QuantidadeCaracteristicas,

            esquerda:
                noEsquerdo,

            direita:
                noDireito
        );
    }

    private List<int> BuscarCaracteristicasDivisiveis(
        IReadOnlyList<IReadOnlyList<double>> amostras)
    {
        var resultado =
            new List<int>();

        for (
            var indice = 0;
            indice < QuantidadeCaracteristicas;
            indice++
        )
        {
            var minimo =
                double.PositiveInfinity;

            var maximo =
                double.NegativeInfinity;

            foreach (var amostra in amostras)
            {
                var valor =
                    amostra[indice];

                if (valor < minimo)
                {
                    minimo = valor;
                }

                if (valor > maximo)
                {
                    maximo = valor;
                }
            }

            if (
                double.IsFinite(minimo) &&
                double.IsFinite(maximo) &&
                maximo > minimo
            )
            {
                resultado.Add(indice);
            }
        }

        return resultado;
    }

    private double GerarValorDivisao(
        double minimo,
        double maximo)
    {
        if (
            !double.IsFinite(minimo) ||
            !double.IsFinite(maximo) ||
            maximo <= minimo
        )
        {
            throw new ArgumentException(
                "Não é possível gerar uma divisão " +
                "para valores mínimos e máximos inválidos."
            );
        }

        var intervalo =
            maximo - minimo;

        var valorDivisao =
            minimo +
            (_random.NextDouble() * intervalo);

        /*
         * Evita que o corte seja exatamente igual ao
         * mínimo ou ao máximo.
         */
        if (
            valorDivisao <= minimo ||
            valorDivisao >= maximo
        )
        {
            valorDivisao =
                minimo +
                (intervalo / 2.0);
        }

        return valorDivisao;
    }

    private static bool TodasAmostrasIguais(
        IReadOnlyList<IReadOnlyList<double>> amostras)
    {
        if (amostras.Count <= 1)
        {
            return true;
        }

        var referencia =
            amostras[0];

        for (
            var indiceAmostra = 1;
            indiceAmostra < amostras.Count;
            indiceAmostra++
        )
        {
            var atual =
                amostras[indiceAmostra];

            for (
                var indiceCaracteristica = 0;
                indiceCaracteristica < referencia.Count;
                indiceCaracteristica++
            )
            {
                if (
                    atual[indiceCaracteristica] !=
                    referencia[indiceCaracteristica]
                )
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void ValidarAmostras(
        IReadOnlyList<IReadOnlyList<double>> amostras,
        int quantidadeCaracteristicas)
    {
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
                    $"A amostra de índice {indiceAmostra} é nula.",
                    nameof(amostras)
                );
            }

            if (
                amostra.Count !=
                quantidadeCaracteristicas
            )
            {
                throw new ArgumentException(
                    $"A amostra de índice {indiceAmostra} possui " +
                    $"{amostra.Count} características, mas eram " +
                    $"esperadas {quantidadeCaracteristicas}.",
                    nameof(amostras)
                );
            }

            for (
                var indiceCaracteristica = 0;
                indiceCaracteristica < amostra.Count;
                indiceCaracteristica++
            )
            {
                if (
                    !double.IsFinite(
                        amostra[indiceCaracteristica]
                    )
                )
                {
                    throw new ArgumentException(
                        $"A amostra {indiceAmostra} possui um valor " +
                        $"inválido na característica " +
                        $"{indiceCaracteristica}.",
                        nameof(amostras)
                    );
                }
            }
        }
    }

    /// <summary>
    /// Converte a árvore para um objeto serializável.
    /// </summary>
    public IsolationTreeSnapshot ExportarSnapshot()
    {
        return new IsolationTreeSnapshot
        {
            ProfundidadeMaxima =
                ProfundidadeMaxima,

            QuantidadeCaracteristicas =
                QuantidadeCaracteristicas,

            QuantidadeAmostras =
                QuantidadeAmostras,

            Raiz =
                Raiz.ExportarSnapshot()
        };
    }

    /// <summary>
    /// Reconstrói uma árvore salva anteriormente.
    /// </summary>
    public static IsolationTree CarregarSnapshot(
        IsolationTreeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot
        );

        if (snapshot.Raiz == null)
        {
            throw new InvalidOperationException(
                "O snapshot da árvore não possui raiz."
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

        if (
            snapshot.QuantidadeAmostras <= 0
        )
        {
            throw new InvalidOperationException(
                "O snapshot possui uma quantidade inválida " +
                "de amostras."
            );
        }

        var raiz =
            IsolationTreeNode.CarregarSnapshot(
                snapshot.Raiz
            );

        return new IsolationTree(
            raiz,
            snapshot.ProfundidadeMaxima,
            snapshot.QuantidadeCaracteristicas,
            snapshot.QuantidadeAmostras
        );
    }
}