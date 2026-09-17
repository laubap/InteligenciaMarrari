/// <summary>
/// Calcula as estatísticas utilizadas para construir um perfil comportamental
/// de tag.
///
/// Este componente não persiste dados e não conhece cliente, tag ou regime.
/// Ele apenas transforma uma série histórica em estatísticas.
///
/// A matemática preserva o comportamento atualmente utilizado pelo
/// PerfilTagService:
///
/// - limpeza de outliers por IQR;
/// - média;
/// - variância populacional;
/// - desvio padrão;
/// - mínimo, máximo e amplitude;
/// - percentual de zeros;
/// - variações absolutas consecutivas;
/// - deadbands P90, P95 e P99;
/// - confiança da sugestão de deadband;
/// - quantidade de picos fora de média +/- 3 desvios padrão.
/// </summary>
public sealed class TagProfileStatisticsCalculator
{
    private const double PercentilConservador = 0.90;
    private const double PercentilRecomendado = 0.95;
    private const double PercentilAgressivo = 0.99;

    private const double Epsilon = 1e-9;

    private readonly HistoricoLimpezaService _limpezaService;

    public TagProfileStatisticsCalculator(
        HistoricoLimpezaService limpezaService)
    {
        _limpezaService =
            limpezaService ??
            throw new ArgumentNullException(
                nameof(limpezaService));
    }

    public TagProfileStatisticsResult Calculate(
        IReadOnlyList<LeituraBruta> historico)
    {
        ArgumentNullException.ThrowIfNull(
            historico);

        if (historico.Count < 10)
        {
            throw new InvalidOperationException(
                "Histórico insuficiente para calcular estatísticas do perfil.");
        }

        var historicoOriginal =
            historico.ToList();

        var historicoLimpo =
            _limpezaService.RemoverOutliersPorIqr(
                historicoOriginal);

        if (historicoLimpo == null ||
            historicoLimpo.Count < 10)
        {
            throw new InvalidOperationException(
                "Histórico insuficiente após a limpeza de outliers.");
        }

        var totalOriginal =
            historicoOriginal.Count;

        var totalLimpo =
            historicoLimpo.Count;

        var totalRemovidos =
            totalOriginal -
            totalLimpo;

        var valores =
            historicoLimpo
                .Select(item =>
                    Convert.ToDouble(
                        item.Valor))
                .Where(double.IsFinite)
                .ToList();

        if (valores.Count < 10)
        {
            throw new InvalidOperationException(
                "Histórico insuficiente após remover valores inválidos.");
        }

        var media =
            valores.Average();

        var minimo =
            valores.Min();

        var maximo =
            valores.Max();

        /*
         * Mantém a mesma variância populacional atualmente utilizada
         * pelo PerfilTagService.
         */
        var variancia =
            valores
                .Select(valor =>
                    Math.Pow(
                        valor - media,
                        2))
                .Average();

        var desvioPadrao =
            Math.Sqrt(
                variancia);

        var amplitude =
            maximo -
            minimo;

        var percentualZeros =
            valores.Count(valor =>
                Math.Abs(valor) <= Epsilon) /
            (double)valores.Count *
            100.0;

        var variacoes =
            CalcularVariacoesAbsolutas(
                valores);

        var variacaoMedia =
            variacoes.Count > 0
                ? variacoes.Average()
                : 0;

        var deadbandConservador =
            CalcularPercentil(
                variacoes,
                PercentilConservador);

        var deadbandRecomendado =
            CalcularPercentil(
                variacoes,
                PercentilRecomendado);

        var deadbandAgressivo =
            CalcularPercentil(
                variacoes,
                PercentilAgressivo);

        if (deadbandRecomendado <= Epsilon &&
            variacaoMedia > Epsilon)
        {
            deadbandRecomendado =
                variacaoMedia;
        }

        var confiancaDeadband =
            CalcularConfiancaDeadband(
                variacoes,
                totalRemovidos,
                totalOriginal,
                deadbandRecomendado);

        var justificativaDeadband =
            CriarJustificativaDeadband(
                deadbandConservador,
                deadbandRecomendado,
                deadbandAgressivo,
                variacoes.Count,
                confiancaDeadband);

        var limitePicoSuperior =
            media +
            (3 * desvioPadrao);

        var limitePicoInferior =
            media -
            (3 * desvioPadrao);

        var quantidadePicos =
            valores.Count(valor =>
                valor > limitePicoSuperior ||
                valor < limitePicoInferior);

        return new TagProfileStatisticsResult
        {
            Media =
                media,

            Minimo =
                minimo,

            Maximo =
                maximo,

            DesvioPadrao =
                desvioPadrao,

            Amplitude =
                amplitude,

            PercentualZeros =
                percentualZeros,

            VariacaoMedia =
                variacaoMedia,

            QuantidadePicos =
                quantidadePicos,

            TotalRegistrosHistorico =
                totalOriginal,

            TotalRegistrosUsados =
                totalLimpo,

            TotalOutliersRemovidos =
                totalRemovidos,

            DeadbandConservador =
                deadbandConservador,

            DeadbandRecomendado =
                deadbandRecomendado,

            DeadbandAgressivo =
                deadbandAgressivo,

            ConfiancaDeadband =
                confiancaDeadband,

            JustificativaDeadband =
                justificativaDeadband,

            HistoricoLimpo =
                historicoLimpo
        };
    }

    private static List<double> CalcularVariacoesAbsolutas(
        IReadOnlyList<double> valores)
    {
        var variacoes =
            new List<double>(
                Math.Max(
                    0,
                    valores.Count - 1));

        for (var indice = 1;
             indice < valores.Count;
             indice++)
        {
            var variacao =
                Math.Abs(
                    valores[indice] -
                    valores[indice - 1]);

            if (double.IsFinite(
                    variacao))
            {
                variacoes.Add(
                    variacao);
            }
        }

        return variacoes;
    }

    private static double CalcularPercentil(
        IReadOnlyCollection<double> valores,
        double percentil)
    {
        if (valores.Count == 0)
        {
            return 0;
        }

        var ordenados =
            valores
                .Where(double.IsFinite)
                .OrderBy(valor =>
                    valor)
                .ToArray();

        if (ordenados.Length == 0)
        {
            return 0;
        }

        if (ordenados.Length == 1)
        {
            return ordenados[0];
        }

        var percentilLimitado =
            Math.Clamp(
                percentil,
                0,
                1);

        var posicao =
            percentilLimitado *
            (ordenados.Length - 1);

        var indiceInferior =
            (int)Math.Floor(
                posicao);

        var indiceSuperior =
            (int)Math.Ceiling(
                posicao);

        if (indiceInferior ==
            indiceSuperior)
        {
            return ordenados[
                indiceInferior];
        }

        var fracao =
            posicao -
            indiceInferior;

        return
            ordenados[indiceInferior] +
            ((ordenados[indiceSuperior] -
              ordenados[indiceInferior]) *
             fracao);
    }

    private static double CalcularConfiancaDeadband(
        IReadOnlyCollection<double> variacoes,
        int totalRemovidos,
        int totalOriginal,
        double deadbandRecomendado)
    {
        var quantidadeVariacoes =
            variacoes.Count;

        if (quantidadeVariacoes < 9 ||
            deadbandRecomendado <= Epsilon)
        {
            return 0;
        }

        double confianca =
            quantidadeVariacoes switch
            {
                >= 10_000 => 98,
                >= 5_000 => 96,
                >= 1_000 => 94,
                >= 500 => 90,
                >= 100 => 85,
                >= 30 => 75,
                _ => 65
            };

        if (totalOriginal > 0)
        {
            var percentualRemovido =
                totalRemovidos /
                (double)totalOriginal *
                100.0;

            if (percentualRemovido > 20)
            {
                confianca -=
                    20;
            }
            else if (percentualRemovido > 10)
            {
                confianca -=
                    10;
            }
            else if (percentualRemovido > 5)
            {
                confianca -=
                    5;
            }
        }

        var percentualVariacoesZero =
            variacoes.Count(valor =>
                Math.Abs(valor) <= Epsilon) /
            (double)quantidadeVariacoes *
            100.0;

        if (percentualVariacoesZero > 95)
        {
            confianca -=
                25;
        }
        else if (percentualVariacoesZero > 80)
        {
            confianca -=
                15;
        }
        else if (percentualVariacoesZero > 60)
        {
            confianca -=
                8;
        }

        var quantidadeDistintaAproximada =
            variacoes
                .Select(valor =>
                    Math.Round(
                        valor,
                        9))
                .Distinct()
                .Take(20)
                .Count();

        if (quantidadeDistintaAproximada <= 2)
        {
            confianca -=
                15;
        }
        else if (quantidadeDistintaAproximada <= 5)
        {
            confianca -=
                7;
        }

        return Math.Clamp(
            confianca,
            0,
            100);
    }

    private static string CriarJustificativaDeadband(
        double deadbandConservador,
        double deadbandRecomendado,
        double deadbandAgressivo,
        int quantidadeVariacoes,
        double confiancaDeadband)
    {
        if (deadbandRecomendado <= Epsilon)
        {
            return
                "Não foi possível sugerir uma variação mínima relevante, " +
                "pois o histórico limpo não apresentou variações " +
                "consecutivas suficientes acima de zero.";
        }

        return
            $"A sugestão foi calculada com {quantidadeVariacoes:N0} " +
            $"variações consecutivas do histórico limpo. " +
            $"O valor recomendado corresponde ao percentil 95: " +
            $"{deadbandRecomendado:G6}. " +
            $"Como referência, o perfil conservador (P90) é " +
            $"{deadbandConservador:G6} e o perfil agressivo (P99) é " +
            $"{deadbandAgressivo:G6}. " +
            $"Confiança estimada da sugestão: " +
            $"{confiancaDeadband:F1}%.";
    }
}

/// <summary>
/// Resultado estatístico calculado a partir de um histórico de tag.
/// </summary>
public sealed class TagProfileStatisticsResult
{
    public double Media { get; init; }

    public double Minimo { get; init; }

    public double Maximo { get; init; }

    public double DesvioPadrao { get; init; }

    public double Amplitude { get; init; }

    public double PercentualZeros { get; init; }

    public double VariacaoMedia { get; init; }

    public int QuantidadePicos { get; init; }

    public int TotalRegistrosHistorico { get; init; }

    public int TotalRegistrosUsados { get; init; }

    public int TotalOutliersRemovidos { get; init; }

    public double DeadbandConservador { get; init; }

    public double DeadbandRecomendado { get; init; }

    public double DeadbandAgressivo { get; init; }

    public double ConfiancaDeadband { get; init; }

    public string JustificativaDeadband { get; init; } = "";

    public IReadOnlyList<LeituraBruta> HistoricoLimpo { get; init; } =
        Array.Empty<LeituraBruta>();
}