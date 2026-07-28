using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.ModelTraining;

public class DadosTreinamentoTagService
{
    private readonly AppDbContext _context;
    private readonly IHistoricoService _historicoService;

    public DadosTreinamentoTagService(
        AppDbContext context,
        IHistoricoService historicoService)
    {
        _context = context;
        _historicoService = historicoService;
    }

    public ResultadoDadosTreinamento Obter(
        string clienteId,
        string tagName,
        string tipoTag)
    {
        var janelas = _context.JanelasTreinamentoTagsIa
            .AsNoTracking()
            .Where(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName &&
                x.Ativa)
            .OrderBy(x => x.DataHoraInicio)
            .ToList();

        if (janelas.Count == 0)
        {
            var historicoFallback =
                _historicoService.BuscarHistorico(
                    clienteId,
                    tagName,
                    tipoTag
                );

            return new ResultadoDadosTreinamento
            {
                Leituras = Limpar(historicoFallback),
                UsouFallbackHistoricoRecente = true,
                Janelas = new List<JanelaTreinamentoSnapshot>()
            };
        }

        var historicoCompleto =
            new List<LeituraBruta>();

        foreach (var janela in janelas)
        {
            if (janela.DataHoraFim <= janela.DataHoraInicio)
            {
                continue;
            }

        var leituras =
            _historicoService.BuscarHistoricoPorPeriodo(
                clienteId,
                tagName,
                janela.DataHoraInicio,
                janela.DataHoraFim
            );

            historicoCompleto.AddRange(leituras);
        }

        return new ResultadoDadosTreinamento
        {
            Leituras = Limpar(historicoCompleto),

            UsouFallbackHistoricoRecente = false,

            Janelas = janelas
                .Select(x => new JanelaTreinamentoSnapshot
                {
                    JanelaId = x.Id,
                    Inicio = x.DataHoraInicio,
                    Fim = x.DataHoraFim,
                    Descricao = x.Descricao,
                    ModoOperacao = x.ModoOperacao
                })
                .ToList()
        };
    }

    private static List<LeituraBruta> Limpar(
        IEnumerable<LeituraBruta> leituras)
    {
        return leituras
            .Where(x =>
                !float.IsNaN(x.Valor) &&
                !float.IsInfinity(x.Valor))
            .GroupBy(x => x.DataHora)
            .Select(x => x.Last())
            .OrderBy(x => x.DataHora)
            .ToList();
    }
}

public class ResultadoDadosTreinamento
{
    public List<LeituraBruta> Leituras { get; set; } = new();

    public bool UsouFallbackHistoricoRecente { get; set; }

    public List<JanelaTreinamentoSnapshot> Janelas
        { get; set; } = new();
}