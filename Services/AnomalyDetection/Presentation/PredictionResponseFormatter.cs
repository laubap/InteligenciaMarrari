using System.Text.Json;
using IAApi.Services.AnomalyDetection.Multivariate;

namespace IAApi.Services.AnomalyDetection.Presentation;

public static class PredictionResponseFormatter
{
    public static string MapearSeveridadeRisco(
        string? classificacao)
    {
        return classificacao?.Trim().ToLowerInvariant() switch
        {
            "critico" or "crítico" =>
                "Crítica",

            "alto" =>
                "Alta",

            "medio" or "médio" =>
                "Média",

            "baixo" =>
                "Baixa",

            _ =>
                "Normal"
        };
    }

    public static string MontarMensagemMultivariada(
        AnomaliaRequest request,
        TagContextoIa? contexto,
        ResultadoRiscoProcesso riscoProcesso,
        MultivariateDecisionEvidence evidence)
    {
        var descricaoTag =
            !string.IsNullOrWhiteSpace(
                contexto?.Descricao)
                ? contexto.Descricao
                : request.TagName;

        var risco =
            !string.IsNullOrWhiteSpace(
                riscoProcesso.Classificacao)
                ? riscoProcesso.Classificacao
                : "não classificado";

        return
            $"{descricaoTag} participa de uma condição multivariada " +
            $"anômala no grupo '{evidence.GroupName}'. " +
            $"Risco: {risco}. Recomenda-se verificar em conjunto " +
            "as tags participantes e o estado operacional do processo.";
    }

    public static string MontarMensagemCliente(
        AnomaliaRequest request,
        TagContextoIa? contexto,
        bool ehAnomalia,
        double score,
        ResultadoRiscoProcesso riscoProcesso,
        ResultadoTendenciaValor tendenciaValor)
    {
        var descricaoTag =
            !string.IsNullOrWhiteSpace(
                contexto?.Descricao)
                ? contexto.Descricao
                : request.TagName;

        if (!ehAnomalia)
        {
            return
                $"{descricaoTag} está operando " +
                "dentro do comportamento esperado. " +
                "Nenhuma ação imediata é necessária.";
        }

        var risco =
            !string.IsNullOrWhiteSpace(
                riscoProcesso.Classificacao)
                ? riscoProcesso.Classificacao
                : "não classificado";

        return
            $"{descricaoTag} apresentou comportamento " +
            $"fora do padrão esperado. Risco: {risco}. " +
            "Recomenda-se verificar a condição " +
            "operacional da tag.";
    }

    public static List<string> LerLista(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return
                JsonSerializer.Deserialize<List<string>>(json)
                ?? new List<string>();
        }
        catch
        {
            return json
                .Split(';', ',')
                .Select(x => x.Trim())
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }
}