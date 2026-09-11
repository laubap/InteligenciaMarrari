using System.Globalization;
using IAApi.Services.AnomalyDetection.Interpretation;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV06.3C1
///
/// Produz interpretação determinística para uma anomalia cuja autoridade
/// operacional veio do detector multivariado.
///
/// Responsabilidades:
/// - explicar claramente que a evidência é de GRUPO;
/// - manter score univariado e multivariado semanticamente separados;
/// - não atribuir causa física que o modelo estatístico não provou;
/// - não depender de LLM.
///
/// A severidade é recebida pronta da camada de risco.
/// Esta classe não cria thresholds de severidade próprios.
/// </summary>
public sealed class MultivariateOperationalInterpretationFactory
{
    public AnomalyInterpretationResult Create(
        MultivariateDecisionEvidence evidence,
        double univariateScore,
        string severity)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (string.IsNullOrWhiteSpace(evidence.ClienteId))
        {
            throw new ArgumentException(
                "ClienteId da evidência multivariada é obrigatório.",
                nameof(evidence));
        }

        if (string.IsNullOrWhiteSpace(evidence.GroupName))
        {
            throw new ArgumentException(
                "GroupName da evidência multivariada é obrigatório.",
                nameof(evidence));
        }

        if (evidence.Tags is null ||
            evidence.Tags.Count == 0)
        {
            throw new ArgumentException(
                "A evidência multivariada deve conter pelo menos uma tag.",
                nameof(evidence));
        }

        if (!double.IsFinite(evidence.Score) ||
            !double.IsFinite(evidence.Threshold) ||
            evidence.Score < evidence.Threshold)
        {
            throw new ArgumentException(
                "A evidência multivariada não ultrapassou um limite válido.",
                nameof(evidence));
        }

        if (!double.IsFinite(evidence.MahalanobisSquared) ||
            evidence.MahalanobisSquared < 0)
        {
            throw new ArgumentException(
                "MahalanobisSquared inválido.",
                nameof(evidence));
        }

        if (!double.IsFinite(univariateScore))
        {
            throw new ArgumentOutOfRangeException(
                nameof(univariateScore),
                "O score univariado deve ser finito.");
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException(
                "A severidade operacional é obrigatória.",
                nameof(severity));
        }

        var groupName =
            evidence.GroupName.Trim();

        var tags =
            evidence.Tags
                .Where(tag =>
                    !string.IsNullOrWhiteSpace(tag))
                .Select(tag =>
                    tag.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (tags.Length == 0)
        {
            throw new ArgumentException(
                "A evidência multivariada não contém tags válidas.",
                nameof(evidence));
        }

        var scoreMultivariado =
            Formatar(evidence.Score, 2);

        var threshold =
            Formatar(evidence.Threshold, 2);

        var mahalanobis =
            Formatar(evidence.MahalanobisSquared, 4);

        var scoreUnivariado =
            Formatar(univariateScore, 4);

        return new AnomalyInterpretationResult
        {
            Resumo =
                "Anomalia multivariada detectada.",

            InterpretacaoTecnica =
                $"A combinação das tags do grupo '{groupName}' " +
                "apresentou comportamento conjunto estatisticamente anômalo. " +
                "A evidência é multivariada: uma tag isoladamente pode " +
                "permanecer próxima do seu comportamento esperado enquanto " +
                "a relação conjunta do grupo se desvia do modelo treinado.",

            Evidencias =
                new List<string>
                {
                    $"Grupo multivariado: {groupName}.",

                    $"Score multivariado: {scoreMultivariado} " +
                    $"(limite: {threshold}); " +
                    $"Mahalanobis²: {mahalanobis}.",

                    $"Tags participantes: {string.Join(", ", tags)}.",

                    $"Score univariado da tag atual: {scoreUnivariado}. " +
                    "Esse valor é mantido separado do score multivariado."
                },

            /*
             * O Mahalanobis identifica desvio estatístico conjunto,
             * não uma causa física. Não inventamos causa automaticamente.
             */
            PossiveisCausas =
                new List<string>(),

            Recomendacoes =
                new List<string>
                {
                    "Verificar em conjunto as tags participantes do grupo " +
                    "e o estado operacional do processo no mesmo intervalo.",

                    "Confirmar se houve mudança real de relação entre as " +
                    "variáveis, alteração de processo ou problema de medição."
                },

            Severidade =
                severity.Trim(),

            GeradoPorLlm =
                false,

            ModeloUtilizado =
                "Interpretação determinística multivariada",

            DataGeracao =
                DateTime.UtcNow
        };
    }

    private static string Formatar(
        double value,
        int decimals)
    {
        return value.ToString(
            $"F{decimals}",
            CultureInfo.InvariantCulture);
    }
}