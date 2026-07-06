public class FeedbackAnomaliaAnaliseService
{
    private readonly AppDbContext _context;

    public FeedbackAnomaliaAnaliseService(AppDbContext context)
    {
        _context = context;
    }

    public FeedbackSemelhanteDto? GerarAnaliseFeedbacks(
        string clienteId,
        string tagName,
        double score,
        double valorAtual)
    {
        var feedbacks = (
            from feedback in _context.FeedbackAnomaliasIa
            join anomalia in _context.AnomaliasDetectadas
                on feedback.AnomaliaId equals anomalia.Id
            where feedback.ClienteId == clienteId
                  && feedback.TagName == tagName
            select new
            {
                feedback.Motivo,
                feedback.Descricao,
                feedback.AcaoTomada,
                anomalia.Score,
                Valor = anomalia.ValorRecebido
            }
        ).ToList();

        if (feedbacks.Count == 0)
            return null;

        var similares = feedbacks
            .Where(x =>
                Math.Abs(x.Score - score) <= 2 ||
                Math.Abs(x.Valor - valorAtual) <= Math.Abs(valorAtual * 0.15))
            .ToList();

        if (similares.Count == 0)
            return null;

        var motivoMaisComum = similares
            .GroupBy(x => x.Motivo)
            .OrderByDescending(x => x.Count())
            .First();

        var percentual = Math.Round(
            (double)motivoMaisComum.Count() / similares.Count * 100,
            1
        );

        return new FeedbackSemelhanteDto
        {
            TotalOcorrencias = similares.Count,
            MotivoMaisComum = FormatarMotivo(motivoMaisComum.Key),
            Percentual = percentual,
            Mensagem = "Esse foi o motivo mais frequente registrado pelos operadores em casos semelhantes."
        };
    }

    private string FormatarMotivo(string motivo)
    {
        return motivo
            .Replace("_", " ")
            .Replace("falha mecanica", "falha mecânica")
            .Replace("falha sensor", "falha de sensor");
    }
}

public class FeedbackSemelhanteDto
{
    public int TotalOcorrencias { get; set; }
    public string MotivoMaisComum { get; set; } = "";
    public double Percentual { get; set; }
    public string Mensagem { get; set; } = "";
}