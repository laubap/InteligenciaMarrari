using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV06.1 — avalia, em COUNTERFACTUAL, como uma evidência
/// multivariada poderia influenciar a decisão operacional.
///
/// Esta classe NÃO modifica RuntimeAnomalyDecision e NÃO produz efeitos
/// externos. Ela existe para observar a regra de fusão antes de qualquer
/// rollout real.
/// </summary>
public sealed class MultivariateDecisionCounterfactualEvaluator
{
    public MultivariateDecisionCounterfactual Evaluate(
        RuntimeAnomalyDecision currentDecision,
        MultivariateDecisionFusionOptions options,
        string? clienteId,
        string? currentTagName,
        DateTime currentDataHora,
        IReadOnlyList<MultivariateDetectionResult>? multivariateResults)
    {
        ArgumentNullException.ThrowIfNull(currentDecision);
        ArgumentNullException.ThrowIfNull(options);

        var original =
            Base(
                currentDecision);

        if (!options.Enabled)
        {
            return original with
            {
                Source =
                    "MultivariateFusionDisabled",

                Reasons =
                    [
                        "CounterfactualFusionDisabled",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        if (options.CanaryOnly &&
            !IsClientAllowed(
                clienteId,
                options.AllowedClientIds))
        {
            return original with
            {
                Source =
                    "MultivariateFusionCanaryExcluded",

                Reasons =
                    [
                        "ClientNotEnabledForMultivariateFusion",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        if (string.IsNullOrWhiteSpace(clienteId) ||
            string.IsNullOrWhiteSpace(currentTagName))
        {
            return original with
            {
                Source =
                    "MultivariateFusionInvalidContext",

                Reasons =
                    [
                        "MissingClientOrTagContext",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        if (multivariateResults is null ||
            multivariateResults.Count == 0)
        {
            return original with
            {
                Source =
                    "NoMultivariateEvidence",

                Reasons =
                    [
                        "NoFinalizedMultivariateResult",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        var currentMinute =
            MultivariateShadowRuntimeCoordinator
                .CriarMinuteIndex(
                    currentDataHora);

        var maxAge =
            Math.Max(
                0,
                options.MaxEvidenceAgeMinutes);

        var eligible =
            multivariateResults
                .Where(result =>
                    result is not null &&
                    result.Aplicavel &&
                    result.EhAnomalia)
                .Where(result =>
                    string.Equals(
                        result.ClienteId?.Trim(),
                        clienteId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                .Where(result =>
                    result.TagsUtilizadas.Any(tag =>
                        string.Equals(
                            tag?.Trim(),
                            currentTagName.Trim(),
                            StringComparison.OrdinalIgnoreCase)))
                .Select(result =>
                    new
                    {
                        Result =
                            result,

                        Age =
                            currentMinute -
                            result.MinuteIndex
                    })
                .Where(item =>
                    item.Age >= 0 &&
                    item.Age <= maxAge)
                .OrderByDescending(item =>
                    item.Result.ScoreNormalizado)
                .ThenByDescending(item =>
                    item.Result.MahalanobisSquared)
                .FirstOrDefault();

        if (eligible is null)
        {
            return original with
            {
                Source =
                    "NoFreshMultivariateAnomaly",

                Reasons =
                    [
                        "NoApplicableFreshAnomalousGroupForCurrentTag",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        var evidence =
            new MultivariateDecisionEvidence
            {
                ClienteId =
                    eligible.Result.ClienteId,

                GroupName =
                    eligible.Result.GroupName,

                MinuteIndex =
                    eligible.Result.MinuteIndex,

                AgeMinutes =
                    eligible.Age,

                Score =
                    eligible.Result.ScoreNormalizado,

                Threshold =
                    eligible.Result.LimiteScore,

                MahalanobisSquared =
                    eligible.Result.MahalanobisSquared,

                Tags =
                    eligible.Result.TagsUtilizadas
                        .ToArray()
            };

        /*
         * Regra conservadora:
         * se o runtime já está emitindo anomalia, o multivariado apenas
         * corrobora. Não criamos nova evidência/LLM só por isso.
         */
        if (currentDecision.EmitAnomaly)
        {
            return original with
            {
                Source =
                    "MultivariateCorroborationCounterfactual",

                Reasons =
                    [
                        "FreshMultivariateAnomalyObserved",
                        "RuntimeAlreadyEmitsAnomaly",
                        "NoAdditionalOperationalEffectProposed"
                    ],

                Evidence =
                    evidence
            };
        }

        /*
         * Única promoção simulada nesta etapa:
         * runtime atual = normal/suprimido
         * + anomalia multivariada aplicável
         * + mesmo cliente
         * + tag atual participa do vetor
         * + evidência temporalmente fresca.
         *
         * Ainda é SOMENTE counterfactual.
         */
        return original with
        {
            WouldEmitAnomaly =
                true,

            WouldEmitNewEvidence =
                true,

            WouldChangeDecision =
                true,

            Source =
                "MultivariateNewAnomalyCounterfactual",

            Reasons =
                [
                    "FreshMultivariateAnomalyObserved",
                    "CurrentTagParticipatesInFinalizedVector",
                    "MultivariateWouldPromoteOperationalAnomaly",
                    "CounterfactualOnlyRuntimeDecisionNotChanged"
                ],

            Evidence =
                evidence
        };
    }

    private static MultivariateDecisionCounterfactual Base(
        RuntimeAnomalyDecision currentDecision)
    {
        return new MultivariateDecisionCounterfactual
        {
            OriginalEmitAnomaly =
                currentDecision.EmitAnomaly,

            OriginalEmitNewEvidence =
                currentDecision.EmitNewEvidence,

            WouldEmitAnomaly =
                currentDecision.EmitAnomaly,

            WouldEmitNewEvidence =
                currentDecision.EmitNewEvidence,

            WouldChangeDecision =
                false,

            OriginalSource =
                currentDecision.Source,

            Source =
                "RuntimeDecisionPreserved",

            Reasons =
                ["RuntimeDecisionPreserved"]
        };
    }

    private static bool IsClientAllowed(
        string? clienteId,
        IReadOnlyCollection<string>? allowedClientIds)
    {
        if (string.IsNullOrWhiteSpace(clienteId) ||
            allowedClientIds is null ||
            allowedClientIds.Count == 0)
        {
            return false;
        }

        var normalized =
            clienteId.Trim();

        return allowedClientIds.Any(allowed =>
            !string.IsNullOrWhiteSpace(allowed) &&
            string.Equals(
                allowed.Trim(),
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record MultivariateDecisionCounterfactual
{
    public bool OriginalEmitAnomaly { get; init; }

    public bool OriginalEmitNewEvidence { get; init; }

    public bool WouldEmitAnomaly { get; init; }

    public bool WouldEmitNewEvidence { get; init; }

    public bool WouldChangeDecision { get; init; }

    public string OriginalSource { get; init; } =
        "";

    public string Source { get; init; } =
        "";

    public IReadOnlyList<string> Reasons { get; init; } =
        Array.Empty<string>();

    public MultivariateDecisionEvidence? Evidence { get; init; }
}

public sealed record MultivariateDecisionEvidence
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public long MinuteIndex { get; init; }

    public long AgeMinutes { get; init; }

    public double Score { get; init; }

    public double Threshold { get; init; }

    public double MahalanobisSquared { get; init; }

    public IReadOnlyList<string> Tags { get; init; } =
        Array.Empty<string>();
}
