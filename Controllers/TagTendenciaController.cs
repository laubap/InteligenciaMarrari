using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/tags/tendencia")]
public class TagTendenciaController : ControllerBase
{
    private readonly IHistoricoService _historicoService;
    private readonly AppDbContext _context;

    public TagTendenciaController(
        IHistoricoService historicoService,
        AppDbContext context)
    {
        _historicoService = historicoService;
        _context = context;
    }

    [HttpGet("{tagName}")]
    public IActionResult ObterTendencia(string tagName, [FromQuery] int horas = 6)
    {
        try
        {
            var historico = _historicoService
                .BuscarHistorico(tagName, "perfil-comportamental")
                .Where(x => !float.IsNaN(x.Valor))
                .OrderBy(x => x.DataHora)
                .ToList();

            if (historico.Count == 0)
            {
                return NotFound(new
                {
                    erro = "Nenhum histórico válido encontrado para esta tag."
                });
            }

            var agora = DateTime.Now;
            var inicio = agora.AddHours(-horas);

            var leituras = historico
                .Where(x => x.DataHora >= inicio)
                .ToList();

            if (leituras.Count == 0)
                leituras = historico.TakeLast(20).ToList();

            var valorAtual = leituras.Last().Valor;
            var valorInicial = leituras.First().Valor;

            var variacao = valorInicial != 0
                ? ((valorAtual - valorInicial) / Math.Abs(valorInicial)) * 100
                : 0;

            var tendencia = CalcularTendencia(leituras);

            var perfil = _context.PerfisIa
                .FirstOrDefault(x => x.TagName == tagName);

            var limiteInferior = perfil == null
                ? (double?)null
                : perfil.Media - (3 * perfil.DesvioPadrao);

            var limiteSuperior = perfil == null
                ? (double?)null
                : perfil.Media + (3 * perfil.DesvioPadrao);

            return Ok(new
            {
                tagName,
                horas,
                valorAtual,
                valorInicial,
                variacaoPercentual = Math.Round(variacao, 2),
                tendencia,
                faixaEsperadaMin = limiteInferior,
                faixaEsperadaMax = limiteSuperior,
                interpretacao = MontarInterpretacao(
                    tendencia,
                    variacao,
                    valorAtual,
                    limiteInferior,
                    limiteSuperior
                ),
                historico = leituras.Select(x => new
                {
                    dataHora = x.DataHora,
                    valor = x.Valor
                })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                erro = ex.Message
            });
        }
    }

    private string CalcularTendencia(List<LeituraBruta> leituras)
    {
        if (leituras.Count < 4)
            return "insuficiente";

        var metade = leituras.Count / 2;

        var mediaInicial = leituras
            .Take(metade)
            .Average(x => x.Valor);

        var mediaFinal = leituras
            .Skip(metade)
            .Average(x => x.Valor);

        var diferenca = mediaFinal - mediaInicial;

        if (Math.Abs(diferenca) < 0.5)
            return "estavel";

        return diferenca > 0
            ? "crescimento"
            : "queda";
    }

    private string MontarInterpretacao(
        string tendencia,
        double variacao,
        float valorAtual,
        double? limiteInferior,
        double? limiteSuperior)
    {
        var dentroDaFaixa =
            limiteInferior == null ||
            limiteSuperior == null ||
            (valorAtual >= limiteInferior && valorAtual <= limiteSuperior);

        if (tendencia == "insuficiente")
            return "Ainda não há leituras suficientes para calcular uma tendência recente confiável.";

        if (!dentroDaFaixa)
            return "O valor atual está fora da faixa esperada. Recomenda-se verificar a condição operacional da tag.";

        if (tendencia == "crescimento")
            return $"A tag apresenta tendência de crescimento recente, com variação aproximada de {Math.Round(variacao, 2)}%. O valor ainda permanece dentro da faixa esperada.";

        if (tendencia == "queda")
            return $"A tag apresenta tendência de queda recente, com variação aproximada de {Math.Round(variacao, 2)}%. O valor ainda permanece dentro da faixa esperada.";

        return "A tag permaneceu estável nas leituras recentes e está dentro da faixa esperada.";
    }
}