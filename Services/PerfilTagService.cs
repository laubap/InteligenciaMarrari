// Serviço responsável por treinar ou atualizar o perfil estatístico de uma tag.
// Ele busca o histórico de leituras, remove outliers, calcula métricas estatísticas,
// sugere uma variação mínima relevante (deadband) e persiste o perfil no banco.
public class PerfilTagService
{
    private const double PercentilConservador = 0.90;
    private const double PercentilRecomendado = 0.95;
    private const double PercentilAgressivo = 0.99;
    private const double Epsilon = 1e-9;

    private readonly IHistoricoService _historicoService;
    private readonly AppDbContext _context;
    private readonly HistoricoLimpezaService _limpezaService;

    public PerfilTagService(
        IHistoricoService historicoService,
        AppDbContext context,
        HistoricoLimpezaService limpezaService)
    {
        _historicoService = historicoService;
        _context = context;
        _limpezaService = limpezaService;
    }

    public TagPerfilIa TreinarPerfil(
        string clienteId,
        string tagName)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            throw new ArgumentException(
                "O clienteId é obrigatório.",
                nameof(clienteId));
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException(
                "O nome da tag é obrigatório.",
                nameof(tagName));
        }

        var historico =
            _historicoService.BuscarHistorico(
                clienteId,
                tagName,
                "");

        if (historico == null ||
            historico.Count < 10)
        {
            throw new InvalidOperationException(
                "Histórico insuficiente para treinar o perfil da tag.");
        }

        var historicoLimpo =
            _limpezaService.RemoverOutliersPorIqr(
                historico);

        if (historicoLimpo == null ||
            historicoLimpo.Count < 10)
        {
            throw new InvalidOperationException(
                "Histórico insuficiente após a limpeza de outliers.");
        }

        var totalOriginal = historico.Count;
        var totalLimpo = historicoLimpo.Count;
        var totalRemovidos = totalOriginal - totalLimpo;

        Console.WriteLine(
            $"Histórico original: {totalOriginal:N0} registros");
        Console.WriteLine(
            $"Histórico limpo: {totalLimpo:N0} registros");
        Console.WriteLine(
            $"Outliers removidos: {totalRemovidos:N0}");

        var valores =
            historicoLimpo
                .Select(item => Convert.ToDouble(item.Valor))
                .Where(double.IsFinite)
                .ToList();

        if (valores.Count < 10)
        {
            throw new InvalidOperationException(
                "Histórico insuficiente após remover valores inválidos.");
        }

        var media = valores.Average();
        var minimo = valores.Min();
        var maximo = valores.Max();

        var variancia =
            valores
                .Select(valor => Math.Pow(valor - media, 2))
                .Average();

        var desvioPadrao = Math.Sqrt(variancia);
        var amplitude = maximo - minimo;

        var percentualZeros =
            valores.Count(valor => Math.Abs(valor) <= Epsilon) /
            (double)valores.Count *
            100.0;

        var variacoes =
            CalcularVariacoesAbsolutas(valores);

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
            deadbandRecomendado = variacaoMedia;
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
            media + (3 * desvioPadrao);

        var limitePicoInferior =
            media - (3 * desvioPadrao);

        var quantidadePicos =
            valores.Count(valor =>
                valor > limitePicoSuperior ||
                valor < limitePicoInferior);

        var perfilExistente =
            _context.PerfisIa
                .FirstOrDefault(perfil =>
                    perfil.ClienteId == clienteId &&
                    perfil.TagName == tagName);

        if (perfilExistente != null)
        {
            AtualizarPerfil(
                perfilExistente,
                media,
                minimo,
                maximo,
                desvioPadrao,
                amplitude,
                percentualZeros,
                variacaoMedia,
                quantidadePicos,
                totalOriginal,
                totalLimpo,
                totalRemovidos,
                deadbandRecomendado,
                confiancaDeadband,
                justificativaDeadband);

            _context.SaveChanges();

            ExibirSugestaoDeadband(
                tagName,
                deadbandConservador,
                deadbandRecomendado,
                deadbandAgressivo,
                confiancaDeadband);

            return perfilExistente;
        }

        var perfil = new TagPerfilIa
        {
            ClienteId = clienteId,
            TagName = tagName,

            Media = media,
            Minimo = minimo,
            Maximo = maximo,
            DesvioPadrao = desvioPadrao,

            Amplitude = amplitude,
            PercentualZeros = percentualZeros,
            VariacaoMedia = variacaoMedia,
            QuantidadePicos = quantidadePicos,

            TotalRegistrosHistorico = totalOriginal,
            TotalRegistrosUsados = totalLimpo,
            TotalOutliersRemovidos = totalRemovidos,

            VariacaoMinimaRelevante = deadbandRecomendado,
            DeadbandSugerido = deadbandRecomendado,
            ConfiancaDeadband = confiancaDeadband,
            VariacaoMinimaCalculadaAutomaticamente =
                deadbandRecomendado > Epsilon,
            JustificativaDeadbandSugerido =
                justificativaDeadband,

            DataTreinamento = DateTime.Now
        };

        _context.PerfisIa.Add(perfil);
        _context.SaveChanges();

        ExibirSugestaoDeadband(
            tagName,
            deadbandConservador,
            deadbandRecomendado,
            deadbandAgressivo,
            confiancaDeadband);

        return perfil;
    }

    private static void AtualizarPerfil(
        TagPerfilIa perfil,
        double media,
        double minimo,
        double maximo,
        double desvioPadrao,
        double amplitude,
        double percentualZeros,
        double variacaoMedia,
        int quantidadePicos,
        int totalOriginal,
        int totalLimpo,
        int totalRemovidos,
        double deadbandRecomendado,
        double confiancaDeadband,
        string justificativaDeadband)
    {
        perfil.Media = media;
        perfil.Minimo = minimo;
        perfil.Maximo = maximo;
        perfil.DesvioPadrao = desvioPadrao;

        perfil.Amplitude = amplitude;
        perfil.PercentualZeros = percentualZeros;
        perfil.VariacaoMedia = variacaoMedia;
        perfil.QuantidadePicos = quantidadePicos;

        perfil.TotalRegistrosHistorico = totalOriginal;
        perfil.TotalRegistrosUsados = totalLimpo;
        perfil.TotalOutliersRemovidos = totalRemovidos;

        perfil.VariacaoMinimaRelevante =
            deadbandRecomendado;

        perfil.DeadbandSugerido =
            deadbandRecomendado;

        perfil.ConfiancaDeadband =
            confiancaDeadband;

        perfil.VariacaoMinimaCalculadaAutomaticamente =
            deadbandRecomendado > Epsilon;

        perfil.JustificativaDeadbandSugerido =
            justificativaDeadband;

        perfil.DataTreinamento =
            DateTime.Now;
    }

    private static List<double> CalcularVariacoesAbsolutas(
        IReadOnlyList<double> valores)
    {
        var variacoes =
            new List<double>(
                Math.Max(0, valores.Count - 1));

        for (var indice = 1;
             indice < valores.Count;
             indice++)
        {
            var variacao =
                Math.Abs(
                    valores[indice] -
                    valores[indice - 1]);

            if (double.IsFinite(variacao))
            {
                variacoes.Add(variacao);
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
                .OrderBy(valor => valor)
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
            Math.Clamp(percentil, 0, 1);

        var posicao =
            percentilLimitado *
            (ordenados.Length - 1);

        var indiceInferior =
            (int)Math.Floor(posicao);

        var indiceSuperior =
            (int)Math.Ceiling(posicao);

        if (indiceInferior == indiceSuperior)
        {
            return ordenados[indiceInferior];
        }

        var fracao =
            posicao - indiceInferior;

        return ordenados[indiceInferior] +
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
                confianca -= 20;
            }
            else if (percentualRemovido > 10)
            {
                confianca -= 10;
            }
            else if (percentualRemovido > 5)
            {
                confianca -= 5;
            }
        }

        /*
         * Penaliza séries com muitas diferenças exatamente iguais a zero.
         * Nesses casos, o P95 pode ficar pouco informativo.
         */
        var percentualVariacoesZero =
            variacoes.Count(
                valor =>
                    Math.Abs(valor) <= Epsilon) /
            (double)quantidadeVariacoes *
            100.0;

        if (percentualVariacoesZero > 95)
        {
            confianca -= 25;
        }
        else if (percentualVariacoesZero > 80)
        {
            confianca -= 15;
        }
        else if (percentualVariacoesZero > 60)
        {
            confianca -= 8;
        }

        /*
         * Penaliza quando quase não há diversidade entre as variações.
         */
        var quantidadeDistintaAproximada =
            variacoes
                .Select(
                    valor =>
                        Math.Round(
                            valor,
                            9))
                .Distinct()
                .Take(20)
                .Count();

        if (quantidadeDistintaAproximada <= 2)
        {
            confianca -= 15;
        }
        else if (quantidadeDistintaAproximada <= 5)
        {
            confianca -= 7;
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

    private static void ExibirSugestaoDeadband(
        string tagName,
        double deadbandConservador,
        double deadbandRecomendado,
        double deadbandAgressivo,
        double confiancaDeadband)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            " SUGESTÃO DE VARIAÇÃO MÍNIMA RELEVANTE");
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            $"Tag: {tagName}");
        Console.WriteLine(
            $"Conservadora (P90): " +
            $"{deadbandConservador:G6}");
        Console.WriteLine(
            $"Recomendada (P95): " +
            $"{deadbandRecomendado:G6}");
        Console.WriteLine(
            $"Agressiva (P99): " +
            $"{deadbandAgressivo:G6}");
        Console.WriteLine(
            $"Confiança: " +
            $"{confiancaDeadband:F1}%");
        Console.WriteLine(
            "========================================");
    }
}