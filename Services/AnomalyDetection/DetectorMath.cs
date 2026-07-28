namespace IAApi.Services.AnomalyDetection;

public static class DetectorMath
{
    public static double Median(IEnumerable<double> valores)
    {
        var ordenados = valores
            .Where(double.IsFinite)
            .OrderBy(x => x)
            .ToArray();

        if (ordenados.Length == 0)
            return 0;

        var meio = ordenados.Length / 2;

        if (ordenados.Length % 2 != 0)
            return ordenados[meio];

        return (ordenados[meio - 1] + ordenados[meio]) / 2.0;
    }

    public static double Mad(
        IEnumerable<double> valores,
        double? medianaInformada = null)
    {
        var lista = valores
            .Where(double.IsFinite)
            .ToArray();

        if (lista.Length == 0)
            return 0;

        var mediana = medianaInformada ?? Median(lista);

        return Median(
            lista.Select(valor => Math.Abs(valor - mediana))
        );
    }

    public static double DesvioPadrao(IEnumerable<double> valores)
    {
        var lista = valores
            .Where(double.IsFinite)
            .ToArray();

        if (lista.Length < 2)
            return 0;

        var media = lista.Average();

        var variancia = lista
            .Select(valor => Math.Pow(valor - media, 2))
            .Average();

        return Math.Sqrt(variancia);
    }

    public static double NormalizarPorLimite(
        double valorAbsoluto,
        double limite)
    {
        if (limite <= 0 || !double.IsFinite(valorAbsoluto))
            return 0;

        return Math.Clamp(
            valorAbsoluto / limite * 100.0,
            0,
            100
        );
    }

    public static double IntervaloMedianoMinutos(
        IReadOnlyList<HistoricoDetectorPoint> historico)
    {
        if (historico.Count < 2)
            return 0;

        var intervalos = new List<double>();

        for (var i = 1; i < historico.Count; i++)
        {
            var minutos = (
                historico[i].DataHora -
                historico[i - 1].DataHora
            ).TotalMinutes;

            if (minutos > 0 && double.IsFinite(minutos))
                intervalos.Add(minutos);
        }

        return Median(intervalos);
    }

    public static double EscalaRobusta(
        IReadOnlyCollection<double> valores)
    {
        if (valores.Count == 0)
            return 0;

        var mediana = Median(valores);
        var mad = Mad(valores, mediana);

        // Conversão aproximada de MAD para escala equivalente
        // ao desvio padrão em dados aproximadamente normais.
        var escala = mad * 1.4826;

        if (escala > 0)
            return escala;

        return DesvioPadrao(valores);
    }
}