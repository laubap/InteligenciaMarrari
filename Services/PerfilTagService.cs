// Serviço responsável por treinar ou atualizar o perfil estatístico de uma tag.
// Ele busca o histórico de leituras, utiliza o calculador estatístico compartilhado
// e persiste o perfil comportamental no banco.
public class PerfilTagService
{
    private const double Epsilon = 1e-9;

    private readonly IHistoricoService _historicoService;
    private readonly AppDbContext _context;
    private readonly TagProfileStatisticsCalculator _statisticsCalculator;

    public PerfilTagService(
        IHistoricoService historicoService,
        AppDbContext context,
        HistoricoLimpezaService limpezaService)
    {
        _historicoService =
            historicoService;

        _context =
            context;

        /*
         * Mantemos o mesmo contrato público do PerfilTagService.
         *
         * Dessa forma não é necessário alterar Program.cs,
         * DI ou consumidores existentes nesta etapa.
         */
        _statisticsCalculator =
            new TagProfileStatisticsCalculator(
                limpezaService);
    }

    public TagPerfilIa TreinarPerfil(
        string clienteId,
        string tagName)
    {
        if (string.IsNullOrWhiteSpace(
                clienteId))
        {
            throw new ArgumentException(
                "O clienteId é obrigatório.",
                nameof(clienteId));
        }

        if (string.IsNullOrWhiteSpace(
                tagName))
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

        /*
         * A matemática estatística anteriormente implementada
         * diretamente neste serviço agora fica centralizada no
         * TagProfileStatisticsCalculator.
         *
         * Ele mantém:
         *
         * - limpeza IQR;
         * - média;
         * - variância populacional;
         * - desvio padrão;
         * - mínimo/máximo;
         * - amplitude;
         * - percentual de zeros;
         * - variações consecutivas;
         * - P90/P95/P99;
         * - confiança de deadband;
         * - quantidade de picos.
         */
        var statistics =
            _statisticsCalculator.Calculate(
                historico);

        Console.WriteLine(
            $"Histórico original: " +
            $"{statistics.TotalRegistrosHistorico:N0} registros");

        Console.WriteLine(
            $"Histórico limpo: " +
            $"{statistics.TotalRegistrosUsados:N0} registros");

        Console.WriteLine(
            $"Outliers removidos: " +
            $"{statistics.TotalOutliersRemovidos:N0}");

        var perfilExistente =
            _context.PerfisIa
                .FirstOrDefault(
                    perfil =>
                        perfil.ClienteId ==
                        clienteId &&
                        perfil.TagName ==
                        tagName);

        if (perfilExistente != null)
        {
            AtualizarPerfil(
                perfilExistente,
                statistics);

            _context.SaveChanges();

            ExibirSugestaoDeadband(
                tagName,
                statistics.DeadbandConservador,
                statistics.DeadbandRecomendado,
                statistics.DeadbandAgressivo,
                statistics.ConfiancaDeadband);

            return perfilExistente;
        }

        var perfil =
            new TagPerfilIa
            {
                ClienteId =
                    clienteId,

                TagName =
                    tagName,

                Media =
                    statistics.Media,

                Minimo =
                    statistics.Minimo,

                Maximo =
                    statistics.Maximo,

                DesvioPadrao =
                    statistics.DesvioPadrao,

                Amplitude =
                    statistics.Amplitude,

                PercentualZeros =
                    statistics.PercentualZeros,

                VariacaoMedia =
                    statistics.VariacaoMedia,

                QuantidadePicos =
                    statistics.QuantidadePicos,

                TotalRegistrosHistorico =
                    statistics.TotalRegistrosHistorico,

                TotalRegistrosUsados =
                    statistics.TotalRegistrosUsados,

                TotalOutliersRemovidos =
                    statistics.TotalOutliersRemovidos,

                VariacaoMinimaRelevante =
                    statistics.DeadbandRecomendado,

                DeadbandSugerido =
                    statistics.DeadbandRecomendado,

                ConfiancaDeadband =
                    statistics.ConfiancaDeadband,

                VariacaoMinimaCalculadaAutomaticamente =
                    statistics.DeadbandRecomendado >
                    Epsilon,

                JustificativaDeadbandSugerido =
                    statistics.JustificativaDeadband,

                DataTreinamento =
                    DateTime.Now
            };

        _context.PerfisIa.Add(
            perfil);

        _context.SaveChanges();

        ExibirSugestaoDeadband(
            tagName,
            statistics.DeadbandConservador,
            statistics.DeadbandRecomendado,
            statistics.DeadbandAgressivo,
            statistics.ConfiancaDeadband);

        return perfil;
    }

    private static void AtualizarPerfil(
        TagPerfilIa perfil,
        TagProfileStatisticsResult statistics)
    {
        perfil.Media =
            statistics.Media;

        perfil.Minimo =
            statistics.Minimo;

        perfil.Maximo =
            statistics.Maximo;

        perfil.DesvioPadrao =
            statistics.DesvioPadrao;

        perfil.Amplitude =
            statistics.Amplitude;

        perfil.PercentualZeros =
            statistics.PercentualZeros;

        perfil.VariacaoMedia =
            statistics.VariacaoMedia;

        perfil.QuantidadePicos =
            statistics.QuantidadePicos;

        perfil.TotalRegistrosHistorico =
            statistics.TotalRegistrosHistorico;

        perfil.TotalRegistrosUsados =
            statistics.TotalRegistrosUsados;

        perfil.TotalOutliersRemovidos =
            statistics.TotalOutliersRemovidos;

        perfil.VariacaoMinimaRelevante =
            statistics.DeadbandRecomendado;

        perfil.DeadbandSugerido =
            statistics.DeadbandRecomendado;

        perfil.ConfiancaDeadband =
            statistics.ConfiancaDeadband;

        perfil.VariacaoMinimaCalculadaAutomaticamente =
            statistics.DeadbandRecomendado >
            Epsilon;

        perfil.JustificativaDeadbandSugerido =
            statistics.JustificativaDeadband;

        perfil.DataTreinamento =
            DateTime.Now;
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