namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 — diagnóstico operacional para descobrir pares de tags
/// com relação linear forte no histórico real.
///
/// Esta classe NÃO cria grupos, NÃO treina modelos e NÃO participa do runtime.
/// Serve apenas para escolher conscientemente tags candidatas a um grupo
/// multivariado.
/// </summary>
public sealed class MultivariateCorrelationDiagnosticService
{
    private const double Epsilon = 1e-12;

    private readonly IHistoricoService _historicoService;

    public MultivariateCorrelationDiagnosticService(
        IHistoricoService historicoService)
    {
        _historicoService =
            historicoService ??
            throw new ArgumentNullException(nameof(historicoService));
    }

    public MultivariateCorrelationDiagnosticResponse Analisar(
        MultivariateCorrelationDiagnosticRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ClienteId))
        {
            throw new ArgumentException(
                "ClienteId é obrigatório.");
        }

        if (request.Fim <= request.Inicio)
        {
            throw new ArgumentException(
                "Fim deve ser maior que Inicio.");
        }

        var tags =
            (request.Tags ?? [])
                .Where(tag =>
                    !string.IsNullOrWhiteSpace(tag))
                .Select(tag =>
                    tag.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (tags.Count < 2)
        {
            throw new ArgumentException(
                "Informe pelo menos duas tags.");
        }

        var minimoPontos =
            Math.Max(
                3,
                request.MinimoPontos);

        var top =
            Math.Clamp(
                request.Top,
                1,
                200);

        var series =
            new Dictionary<
                string,
                Dictionary<long, double>>(
                StringComparer.OrdinalIgnoreCase);

        var resumoTags =
            new List<MultivariateCorrelationTagSummary>();

        foreach (var tag in tags)
        {
            var historico =
                _historicoService
                    .BuscarHistoricoPorPeriodo(
                        request.ClienteId.Trim(),
                        tag,
                        request.Inicio,
                        request.Fim)
                ?? [];

            var serie =
                PrepararSeriePorMinuto(
                    historico);

            series[tag] =
                serie;

            resumoTags.Add(
                new MultivariateCorrelationTagSummary
                {
                    TagName =
                        tag,

                    LeiturasRecebidas =
                        historico.Count,

                    MinutosValidos =
                        serie.Count
                });
        }

        var pares =
            new List<
                MultivariateCorrelationPairResult>();

        for (var i = 0;
             i < tags.Count;
             i++)
        {
            for (var j = i + 1;
                 j < tags.Count;
                 j++)
            {
                var tagA =
                    tags[i];

                var tagB =
                    tags[j];

                var serieA =
                    series[tagA];

                var serieB =
                    series[tagB];

                var minutosComuns =
                    serieA.Keys
                        .Intersect(
                            serieB.Keys)
                        .OrderBy(
                            minute => minute)
                        .ToList();

                if (minutosComuns.Count <
                    minimoPontos)
                {
                    pares.Add(
                        new MultivariateCorrelationPairResult
                        {
                            TagA =
                                tagA,

                            TagB =
                                tagB,

                            PontosAlinhados =
                                minutosComuns.Count,

                            Aplicavel =
                                false,

                            Motivo =
                                $"Apenas {minutosComuns.Count} " +
                                "pontos alinhados."
                        });

                    continue;
                }

                var valoresA =
                    minutosComuns
                        .Select(minute =>
                            serieA[minute])
                        .ToArray();

                var valoresB =
                    minutosComuns
                        .Select(minute =>
                            serieB[minute])
                        .ToArray();

                var correlacao =
                    CalcularPearson(
                        valoresA,
                        valoresB);

                if (!correlacao.HasValue)
                {
                    pares.Add(
                        new MultivariateCorrelationPairResult
                        {
                            TagA =
                                tagA,

                            TagB =
                                tagB,

                            PontosAlinhados =
                                minutosComuns.Count,

                            Aplicavel =
                                false,

                            Motivo =
                                "Uma das séries não possui " +
                                "variabilidade suficiente."
                        });

                    continue;
                }

                pares.Add(
                    new MultivariateCorrelationPairResult
                    {
                        TagA =
                            tagA,

                        TagB =
                            tagB,

                        PontosAlinhados =
                            minutosComuns.Count,

                        Aplicavel =
                            true,

                        Correlacao =
                            correlacao.Value,

                        CorrelacaoAbsoluta =
                            Math.Abs(
                                correlacao.Value),

                        Classificacao =
                            Classificar(
                                correlacao.Value)
                    });
            }
        }

        var ranking =
            pares
                .Where(par =>
                    par.Aplicavel)
                .OrderByDescending(par =>
                    par.CorrelacaoAbsoluta)
                .ThenByDescending(par =>
                    par.PontosAlinhados)
                .Take(top)
                .ToList();

        return
            new MultivariateCorrelationDiagnosticResponse
            {
                ClienteId =
                    request.ClienteId.Trim(),

                Inicio =
                    request.Inicio,

                Fim =
                    request.Fim,

                TagsAnalisadas =
                    resumoTags,

                TotalParesPossiveis =
                    tags.Count *
                    (tags.Count - 1) / 2,

                ParesAplicaveis =
                    pares.Count(par =>
                        par.Aplicavel),

                Ranking =
                    ranking
            };
    }

    private static Dictionary<long, double>
        PrepararSeriePorMinuto(
            IEnumerable<LeituraBruta> historico)
    {
        return
            historico
                .Where(leitura =>
                    leitura is not null &&
                    leitura.DataHora != default &&
                    float.IsFinite(
                        leitura.Valor))
                .OrderBy(leitura =>
                    leitura.DataHora)
                .GroupBy(leitura =>
                    CriarMinuteIndex(
                        leitura.DataHora))
                .ToDictionary(
                    grupo =>
                        grupo.Key,

                    grupo =>
                        (double)grupo.Last().Valor);
    }

    private static double?
        CalcularPearson(
            IReadOnlyList<double> x,
            IReadOnlyList<double> y)
    {
        if (x.Count != y.Count ||
            x.Count < 3)
        {
            return null;
        }

        var mediaX =
            x.Average();

        var mediaY =
            y.Average();

        double somaXY = 0;
        double somaXX = 0;
        double somaYY = 0;

        for (var i = 0;
             i < x.Count;
             i++)
        {
            var dx =
                x[i] - mediaX;

            var dy =
                y[i] - mediaY;

            somaXY +=
                dx * dy;

            somaXX +=
                dx * dx;

            somaYY +=
                dy * dy;
        }

        if (somaXX <= Epsilon ||
            somaYY <= Epsilon)
        {
            return null;
        }

        var denominador =
            Math.Sqrt(
                somaXX *
                somaYY);

        if (denominador <= Epsilon ||
            !double.IsFinite(
                denominador))
        {
            return null;
        }

        var correlacao =
            somaXY /
            denominador;

        if (!double.IsFinite(
                correlacao))
        {
            return null;
        }

        return Math.Clamp(
            correlacao,
            -1,
            1);
    }

    private static string Classificar(
        double correlacao)
    {
        var absoluta =
            Math.Abs(
                correlacao);

        var intensidade =
            absoluta >= 0.90
                ? "muito-forte"
                : absoluta >= 0.80
                    ? "forte"
                    : absoluta >= 0.60
                        ? "moderada"
                        : absoluta >= 0.40
                            ? "fraca"
                            : "muito-fraca";

        var direcao =
            correlacao >= 0
                ? "positiva"
                : "negativa";

        return
            $"{intensidade}-{direcao}";
    }

    private static long CriarMinuteIndex(
        DateTime dataHora)
    {
        return
            dataHora.Ticks /
            TimeSpan.TicksPerMinute;
    }
}

public sealed class
    MultivariateCorrelationDiagnosticRequest
{
    public string ClienteId { get; set; } =
        "";

    public DateTime Inicio { get; set; }

    public DateTime Fim { get; set; }

    public List<string> Tags { get; set; } =
        [];

    public int MinimoPontos { get; set; } =
        30;

    public int Top { get; set; } =
        20;
}

public sealed class
    MultivariateCorrelationDiagnosticResponse
{
    public string ClienteId { get; set; } =
        "";

    public DateTime Inicio { get; set; }

    public DateTime Fim { get; set; }

    public List<
        MultivariateCorrelationTagSummary>
        TagsAnalisadas { get; set; } =
            [];

    public int TotalParesPossiveis
        { get; set; }

    public int ParesAplicaveis
        { get; set; }

    public List<
        MultivariateCorrelationPairResult>
        Ranking { get; set; } =
            [];
}

public sealed class
    MultivariateCorrelationTagSummary
{
    public string TagName { get; set; } =
        "";

    public int LeiturasRecebidas
        { get; set; }

    public int MinutosValidos
        { get; set; }
}

public sealed class
    MultivariateCorrelationPairResult
{
    public string TagA { get; set; } =
        "";

    public string TagB { get; set; } =
        "";

    public int PontosAlinhados
        { get; set; }

    public bool Aplicavel
        { get; set; }

    public double? Correlacao
        { get; set; }

    public double? CorrelacaoAbsoluta
        { get; set; }

    public string? Classificacao
        { get; set; }

    public string? Motivo
        { get; set; }
}
