// Serviço responsável por treinar ou atualizar o perfil estatístico de uma tag.
// Ele busca o histórico de leituras, remove outliers, calcula métricas estatísticas
// e persiste o perfil resultante no banco de dados.
public class PerfilTagService
{
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

    // Treina o perfil da tag para o cliente informado e retorna o objeto de perfil.
    // O método valida histórico suficiente, remove outliers e calcula média, desvio,
    // amplitude, percentual de zeros, variação média e picos.
    public TagPerfilIa TreinarPerfil(string clienteId, string tagName)
    {
        var historico = _historicoService.BuscarHistorico(tagName, "", clienteId);

        if (historico == null || historico.Count < 10)
            throw new Exception("Histórico insuficiente para treinar o perfil da tag.");

        var historicoLimpo = _limpezaService.RemoverOutliersPorIqr(historico);

        if (historicoLimpo == null || historicoLimpo.Count < 10)
            throw new Exception("Histórico insuficiente após limpeza de outliers.");

        var totalOriginal = historico.Count;
        var totalLimpo = historicoLimpo.Count;
        var totalRemovidos = totalOriginal - totalLimpo;

        Console.WriteLine($"Histórico original: {totalOriginal} registros");
        Console.WriteLine($"Histórico limpo: {totalLimpo} registros");
        Console.WriteLine($"Outliers removidos: {totalRemovidos}");

        var valores = historicoLimpo
            .Select(x => Convert.ToDouble(x.Valor))
            .ToList();

        var media = valores.Average();
        var minimo = valores.Min();
        var maximo = valores.Max();

        var variancia = valores
            .Select(v => Math.Pow(v - media, 2))
            .Average();

        var desvioPadrao = Math.Sqrt(variancia);

        var amplitude = maximo - minimo;

        var percentualZeros =
            valores.Count(v => v == 0) / (double)valores.Count * 100;

        var variacoes = new List<double>();

        for (int i = 1; i < valores.Count; i++)
        {
            variacoes.Add(Math.Abs(valores[i] - valores[i - 1]));
        }

        var variacaoMedia = variacoes.Any()
            ? variacoes.Average()
            : 0;

        var limitePicoSuperior = media + (3 * desvioPadrao);
        var limitePicoInferior = media - (3 * desvioPadrao);

        var quantidadePicos = valores.Count(v =>
            v > limitePicoSuperior ||
            v < limitePicoInferior
        );

        var perfilExistente = _context.PerfisIa
            .FirstOrDefault(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName);

        if (perfilExistente != null)
        {
            perfilExistente.Media = media;
            perfilExistente.Minimo = minimo;
            perfilExistente.Maximo = maximo;
            perfilExistente.DesvioPadrao = desvioPadrao;

            perfilExistente.Amplitude = amplitude;
            perfilExistente.PercentualZeros = percentualZeros;
            perfilExistente.VariacaoMedia = variacaoMedia;
            perfilExistente.QuantidadePicos = quantidadePicos;

            perfilExistente.TotalRegistrosHistorico = totalOriginal;
            perfilExistente.TotalRegistrosUsados = totalLimpo;
            perfilExistente.TotalOutliersRemovidos = totalRemovidos;

            perfilExistente.DataTreinamento = DateTime.Now;

            _context.SaveChanges();

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

            DataTreinamento = DateTime.Now
        };

        _context.PerfisIa.Add(perfil);
        _context.SaveChanges();

        return perfil;
    }
}