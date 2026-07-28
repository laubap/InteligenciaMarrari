namespace IAApi.Services.AnomalyDetection;

public static class IsolationForestMath
{
    private const double EulerGamma =
        0.5772156649015329;

    public static double CalcularComprimentoEsperado(
        int quantidadeAmostras)
    {
        if (quantidadeAmostras <= 1)
        {
            return 0;
        }

        if (quantidadeAmostras == 2)
        {
            return 1;
        }

        var n = quantidadeAmostras;

        var harmonicoAproximado =
            Math.Log(n - 1) +
            EulerGamma;

        return
            2.0 * harmonicoAproximado -
            2.0 * (n - 1.0) / n;
    }

    public static double CalcularScoreAnomalia(
        double comprimentoMedio,
        int tamanhoAmostra)
    {
        var comprimentoEsperado =
            CalcularComprimentoEsperado(
                tamanhoAmostra
            );

        if (comprimentoEsperado <= 0)
        {
            return 0;
        }

        return Math.Pow(
            2,
            -comprimentoMedio /
            comprimentoEsperado
        );
    }
}