// Serviço que analisa a tendência de valor de uma tag com base no histórico
// de leituras fornecido pelo histórico de tags. Retorna se o valor está
// estável, em alta, em queda ou se não há histórico suficiente.
public class TendenciaValorService
{
    private readonly IHistoricoService _historicoService;

    public TendenciaValorService(IHistoricoService historicoService)
    {
        _historicoService = historicoService;
    }

    // Analisa a tendência de valor para a tag e o tipo de tag informados.
    public ResultadoTendenciaValor Analisar(
        string clienteId,
        string tagName,
        string tipoTag)
    {
        var historico = _historicoService.BuscarHistorico(
            clienteId,
        tagName,
        tipoTag
        );

        var dados = historico
            .OrderBy(x => x.DataHora)
            .ToList();

        if (dados.Count < 5)
        {
            return new ResultadoTendenciaValor
            {
                Tendencia = "insuficiente",
                Mensagem = "Histórico insuficiente no Bridge para analisar tendência do valor."
            };
        }

        var metade = dados.Count / 2;

        var primeiraMetade = dados
            .Take(metade)
            .Average(x => x.Valor);

        var segundaMetade = dados
            .Skip(metade)
            .Average(x => x.Valor);

        var diferenca = segundaMetade - primeiraMetade;

        var percentual = primeiraMetade != 0
            ? Math.Abs(diferenca / primeiraMetade) * 100
            : Math.Abs(diferenca);

        if (percentual < 10)
        {
            return new ResultadoTendenciaValor
            {
                Tendencia = "estavel",
                Mensagem = "O valor da tag permaneceu relativamente estável no histórico recente."
            };
        }

        if (diferenca > 0)
        {
            return new ResultadoTendenciaValor
            {
                Tendencia = "alta",
                Mensagem = $"O valor da tag apresenta tendência de alta. Média inicial {primeiraMetade:F2}, média recente {segundaMetade:F2}."
            };
        }

        return new ResultadoTendenciaValor
        {
            Tendencia = "queda",
            Mensagem = $"O valor da tag apresenta tendência de queda. Média inicial {primeiraMetade:F2}, média recente {segundaMetade:F2}."
        };
    }
}

public class ResultadoTendenciaValor
{
    public string Tendencia { get; set; } = "";
    public string Mensagem { get; set; } = "";
}