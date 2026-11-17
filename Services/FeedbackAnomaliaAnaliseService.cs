using Microsoft.EntityFrameworkCore;

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
        var feedbacks = ConsultarFeedbacks()
            .Where(x => x.ClienteId == clienteId && x.TagName == tagName)
            .ToList();

        return AnalisarFeedbacks(feedbacks, score, valorAtual);
    }

    /// <summary>
    /// Tarefa 34.1 (P42): carrega numa unica consulta os feedbacks que
    /// <see cref="GerarAnaliseFeedbacks"/> buscaria um a um, agrupados por
    /// (cliente, tag). Com <paramref name="clienteId"/>, so os desse cliente.
    /// Cada grupo mantem a ordem do Id do feedback, a mesma em que a consulta
    /// por tag devolve as linhas (varredura em ordem de rowid; ver o teste
    /// AnomaliaListarEquivalenciaTests). Essa ordem so importa no desempate do
    /// motivo mais comum.
    /// </summary>
    public IReadOnlyDictionary<(string ClienteId, string TagName), List<FeedbackComparavel>>
        CarregarFeedbacksPorTag(string? clienteId)
    {
        var consulta = ConsultarFeedbacks();

        if (clienteId is not null)
            consulta = consulta.Where(x => x.ClienteId == clienteId);

        // Comparacao ordinal, como a colacao BINARY do SQLite na consulta por tag.
        return consulta
            .OrderBy(x => x.FeedbackId)
            .ToList()
            .GroupBy(x => (x.ClienteId, x.TagName))
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Regra de semelhanca, sem acesso ao banco. <paramref name="feedbacks"/>
    /// sao os feedbacks do cliente e da tag, na ordem da consulta.
    /// </summary>
    public static FeedbackSemelhanteDto? AnalisarFeedbacks(
        IReadOnlyList<FeedbackComparavel> feedbacks,
        double score,
        double valorAtual)
    {
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

    // Mesmo join de sempre: feedback -> motivo (LEFT JOIN) -> anomalia (INNER JOIN).
    private IQueryable<FeedbackComparavel> ConsultarFeedbacks()
    {
        return _context.FeedbackAnomaliasIa
            .Include(x => x.MotivoAnomaliaIA)
            .Join(
                _context.AnomaliasDetectadas,
                feedback => feedback.AnomaliaId,
                anomalia => anomalia.Id,
                (feedback, anomalia) => new FeedbackComparavel
                {
                    FeedbackId = feedback.Id,
                    ClienteId = feedback.ClienteId,
                    TagName = feedback.TagName,
                    Motivo = feedback.MotivoAnomaliaIA != null
                        ? feedback.MotivoAnomaliaIA.Nome
                        : "Motivo não informado",
                    Score = anomalia.Score,
                    Valor = anomalia.ValorRecebido
                }
            );
    }

    private static string FormatarMotivo(string motivo)
    {
        return motivo
            .Replace("_", " ")
            .Replace("falha mecanica", "falha mecânica")
            .Replace("falha sensor", "falha de sensor");
    }
}

/// <summary>Feedback com o score e o valor da anomalia, para a regra de semelhanca.</summary>
public sealed class FeedbackComparavel
{
    public int FeedbackId { get; init; }
    public string ClienteId { get; init; } = "";
    public string TagName { get; init; } = "";
    public string Motivo { get; init; } = "";
    public double Score { get; init; }
    public double Valor { get; init; }
}

public class FeedbackSemelhanteDto
{
    public int TotalOcorrencias { get; set; }

    public string MotivoMaisComum { get; set; } = "";

    public double Percentual { get; set; }

    public string Mensagem { get; set; } = "";
}
