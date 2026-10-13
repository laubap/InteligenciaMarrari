namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Funções matemáticas utilizadas pelos detectores.
///
/// Esta versão evita LINQ, OrderBy e múltiplas enumerações no caminho crítico.
/// A mediana continua sendo exata, mas usa Quickselect em vez de ordenar
/// completamente o vetor.
/// </summary>
public static class DetectorMath
{
    public static double Median(IEnumerable<double> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        if (valores is double[] array)
        {
            return Median(array);
        }

        var lista = valores as IReadOnlyCollection<double>;
        var temporario = lista is null
            ? new List<double>()
            : new List<double>(lista.Count);

        foreach (var valor in valores)
        {
            if (double.IsFinite(valor))
            {
                temporario.Add(valor);
            }
        }

        return temporario.Count == 0
            ? 0
            : Median(temporario.ToArray());
    }

    public static double Median(double[] valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        if (valores.Length == 0)
        {
            return 0;
        }

        var temporario = new double[valores.Length];
        var quantidade = 0;

        for (var i = 0; i < valores.Length; i++)
        {
            var valor = valores[i];

            if (!double.IsFinite(valor))
            {
                continue;
            }

            temporario[quantidade++] = valor;
        }

        return quantidade == 0
            ? 0
            : MedianInPlace(temporario, quantidade);
    }

    public static double Mad(
        IEnumerable<double> valores,
        double? medianaInformada = null)
    {
        ArgumentNullException.ThrowIfNull(valores);

        if (valores is double[] array)
        {
            return Mad(array, medianaInformada);
        }

        var lista = valores as IReadOnlyCollection<double>;
        var temporario = lista is null
            ? new List<double>()
            : new List<double>(lista.Count);

        foreach (var valor in valores)
        {
            if (double.IsFinite(valor))
            {
                temporario.Add(valor);
            }
        }

        return temporario.Count == 0
            ? 0
            : Mad(temporario.ToArray(), medianaInformada);
    }

    public static double Mad(
        double[] valores,
        double? medianaInformada = null)
    {
        ArgumentNullException.ThrowIfNull(valores);

        if (valores.Length == 0)
        {
            return 0;
        }

        var validos = new double[valores.Length];
        var quantidade = 0;

        for (var i = 0; i < valores.Length; i++)
        {
            var valor = valores[i];

            if (!double.IsFinite(valor))
            {
                continue;
            }

            validos[quantidade++] = valor;
        }

        if (quantidade == 0)
        {
            return 0;
        }

        var mediana = medianaInformada ??
            MedianInPlace((double[])validos.Clone(), quantidade);

        var desvios = new double[quantidade];

        for (var i = 0; i < quantidade; i++)
        {
            desvios[i] = Math.Abs(validos[i] - mediana);
        }

        return MedianInPlace(desvios, quantidade);
    }

    public static double DesvioPadrao(IEnumerable<double> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        var quantidade = 0;
        var media = 0.0;
        var m2 = 0.0;

        foreach (var valor in valores)
        {
            if (!double.IsFinite(valor))
            {
                continue;
            }

            quantidade++;
            var delta = valor - media;
            media += delta / quantidade;
            var delta2 = valor - media;
            m2 += delta * delta2;
        }

        if (quantidade < 2)
        {
            return 0;
        }

        var variancia = m2 / quantidade;
        return Math.Sqrt(Math.Max(0, variancia));
    }

    public static double NormalizarPorLimite(
        double valorAbsoluto,
        double limite)
    {
        if (limite <= 0 || !double.IsFinite(valorAbsoluto))
        {
            return 0;
        }

        return Math.Clamp(
            valorAbsoluto / limite * 100.0,
            0,
            100);
    }

    public static double IntervaloMedianoMinutos(
        IReadOnlyList<HistoricoDetectorPoint> historico)
    {
        ArgumentNullException.ThrowIfNull(historico);

        if (historico.Count < 2)
        {
            return 0;
        }

        var intervalos = new double[historico.Count - 1];
        var quantidade = 0;

        for (var i = 1; i < historico.Count; i++)
        {
            var minutos = (
                historico[i].DataHora -
                historico[i - 1].DataHora
            ).TotalMinutes;

            if (minutos <= 0 || !double.IsFinite(minutos))
            {
                continue;
            }

            intervalos[quantidade++] = minutos;
        }

        return quantidade == 0
            ? 0
            : MedianInPlace(intervalos, quantidade);
    }

    public static double EscalaRobusta(
        IReadOnlyCollection<double> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        if (valores.Count == 0)
        {
            return 0;
        }

        var array = new double[valores.Count];
        var quantidade = 0;

        foreach (var valor in valores)
        {
            if (!double.IsFinite(valor))
            {
                continue;
            }

            array[quantidade++] = valor;
        }

        if (quantidade == 0)
        {
            return 0;
        }

        if (quantidade != array.Length)
        {
            Array.Resize(ref array, quantidade);
        }

        var mediana = Median(array);
        var mad = Mad(array, mediana);
        var escala = mad * 1.4826;

        return escala > 0 && double.IsFinite(escala)
            ? escala
            : DesvioPadrao(array);
    }

    private static double MedianInPlace(
        double[] values,
        int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        var middle = length / 2;

        if ((length & 1) == 1)
        {
            return QuickSelect(values, 0, length - 1, middle);
        }

        var upper = QuickSelect(values, 0, length - 1, middle);
        var lower = QuickSelect(values, 0, middle - 1, middle - 1);

        return (lower + upper) / 2.0;
    }

    private static double QuickSelect(
        double[] values,
        int left,
        int right,
        int targetIndex)
    {
        while (true)
        {
            if (left == right)
            {
                return values[left];
            }

            var pivotIndex = EscolherPivo(values, left, right);
            pivotIndex = Particionar(values, left, right, pivotIndex);

            if (targetIndex == pivotIndex)
            {
                return values[targetIndex];
            }

            if (targetIndex < pivotIndex)
            {
                right = pivotIndex - 1;
            }
            else
            {
                left = pivotIndex + 1;
            }
        }
    }

    private static int EscolherPivo(
        double[] values,
        int left,
        int right)
    {
        var middle = left + ((right - left) / 2);

        if (values[left] > values[middle])
        {
            Trocar(values, left, middle);
        }

        if (values[left] > values[right])
        {
            Trocar(values, left, right);
        }

        if (values[middle] > values[right])
        {
            Trocar(values, middle, right);
        }

        return middle;
    }

    private static int Particionar(
        double[] values,
        int left,
        int right,
        int pivotIndex)
    {
        var pivotValue = values[pivotIndex];
        Trocar(values, pivotIndex, right);

        var storeIndex = left;

        for (var index = left; index < right; index++)
        {
            if (values[index] < pivotValue)
            {
                Trocar(values, storeIndex, index);
                storeIndex++;
            }
        }

        Trocar(values, right, storeIndex);
        return storeIndex;
    }

    private static void Trocar(
        double[] values,
        int first,
        int second)
    {
        if (first == second)
        {
            return;
        }

        (values[first], values[second]) =
            (values[second], values[first]);
    }
}