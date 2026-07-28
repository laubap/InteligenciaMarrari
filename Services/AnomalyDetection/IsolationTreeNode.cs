namespace IAApi.Services.AnomalyDetection;

public class IsolationTreeNode
{
    public bool EhFolha { get; set; }

    public int IndiceCaracteristica { get; set; } = -1;

    public double ValorDivisao { get; set; }

    public IsolationTreeNode? Esquerda { get; set; }

    public IsolationTreeNode? Direita { get; set; }

    public int QuantidadeAmostras { get; set; }

    public int Profundidade { get; set; }

    public int QuantidadeCaracteristicas { get; set; }

    public static IsolationTreeNode CriarFolha(
        int quantidadeAmostras,
        int profundidade,
        int quantidadeCaracteristicas)
    {
        return new IsolationTreeNode
        {
            EhFolha = true,
            IndiceCaracteristica = -1,
            QuantidadeAmostras = quantidadeAmostras,
            Profundidade = profundidade,
            QuantidadeCaracteristicas = quantidadeCaracteristicas
        };
    }

    public static IsolationTreeNode CriarNoInterno(
        int indiceCaracteristica,
        double valorDivisao,
        int quantidadeAmostras,
        int profundidade,
        int quantidadeCaracteristicas,
        IsolationTreeNode esquerda,
        IsolationTreeNode direita)
    {
        return new IsolationTreeNode
        {
            EhFolha = false,
            IndiceCaracteristica = indiceCaracteristica,
            ValorDivisao = valorDivisao,
            QuantidadeAmostras = quantidadeAmostras,
            Profundidade = profundidade,
            QuantidadeCaracteristicas = quantidadeCaracteristicas,
            Esquerda = esquerda,
            Direita = direita
        };
    }



    public double CalcularComprimentoCaminho(
        IReadOnlyList<double> caracteristicas)
    {
        if (caracteristicas == null)
        {
            throw new ArgumentNullException(
                nameof(caracteristicas)
            );
        }

        if (caracteristicas.Count != QuantidadeCaracteristicas)
        {
            throw new ArgumentException(
                $"Quantidade inválida de características. " +
                $"Esperado: {QuantidadeCaracteristicas}. " +
                $"Recebido: {caracteristicas.Count}."
            );
        }

        if (EhFolha)
        {
            return Profundidade +
                   IsolationForestMath.CalcularComprimentoEsperado(
                       QuantidadeAmostras
                   );
        }

        if (
            IndiceCaracteristica < 0 ||
            IndiceCaracteristica >= caracteristicas.Count)
        {
            throw new InvalidOperationException(
                "O índice da característica do nó é inválido."
            );
        }

        var valor = caracteristicas[IndiceCaracteristica];

        if (!double.IsFinite(valor))
        {
            throw new ArgumentException(
                "A amostra possui uma característica inválida."
            );
        }

        if (valor < ValorDivisao)
        {
            if (Esquerda == null)
            {
                throw new InvalidOperationException(
                    "O nó esquerdo não foi configurado."
                );
            }

            return Esquerda.CalcularComprimentoCaminho(
                caracteristicas
            );
        }

        if (Direita == null)
        {
            throw new InvalidOperationException(
                "O nó direito não foi configurado."
            );
        }

        return Direita.CalcularComprimentoCaminho(
            caracteristicas
        );
    }

    public IsolationTreeNodeSnapshot ExportarSnapshot()
    {
        return new IsolationTreeNodeSnapshot
        {
            EhFolha = EhFolha,
            IndiceCaracteristica = IndiceCaracteristica,
            ValorDivisao = ValorDivisao,
            QuantidadeAmostras = QuantidadeAmostras,
            Profundidade = Profundidade,
            QuantidadeCaracteristicas = QuantidadeCaracteristicas,

            Esquerda = Esquerda?.ExportarSnapshot(),
            Direita = Direita?.ExportarSnapshot()
        };
    }

    public static IsolationTreeNode CarregarSnapshot(
        IsolationTreeNodeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var node = new IsolationTreeNode
        {
            EhFolha = snapshot.EhFolha,
            IndiceCaracteristica = snapshot.IndiceCaracteristica,
            ValorDivisao = snapshot.ValorDivisao,
            QuantidadeAmostras = snapshot.QuantidadeAmostras,
            Profundidade = snapshot.Profundidade,
            QuantidadeCaracteristicas =
                snapshot.QuantidadeCaracteristicas
        };

        if (snapshot.Esquerda != null)
        {
            node.Esquerda = CarregarSnapshot(
                snapshot.Esquerda
            );
        }

        if (snapshot.Direita != null)
        {
            node.Direita = CarregarSnapshot(
                snapshot.Direita
            );
        }

        return node;
    }
}