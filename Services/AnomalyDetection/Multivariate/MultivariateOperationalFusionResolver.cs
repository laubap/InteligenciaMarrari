using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV06.3A — contrato isolado de promoção operacional multivariada.
///
/// Não modifica DetectorResult.
/// Não converte ScoreNormalizado multivariado em score univariado.
/// Não chama risco, LLM, persistência ou resposta.
/// </summary>
public sealed class MultivariateOperationalFusionResolver
{
    public MultivariateOperationalFusionResult Resolve(
        RuntimeAnomalyDecision currentDecision,
        MultivariateDecisionCounterfactual counterfactual,
        MultivariateOperationalPromotionOptions options,
        string? clienteId,
        double univariateScore)
    {
        ArgumentNullException.ThrowIfNull(currentDecision);
        ArgumentNullException.ThrowIfNull(counterfactual);
        ArgumentNullException.ThrowIfNull(options);

        var preserved =
            Preserve(
                currentDecision,
                univariateScore,
                counterfactual.Evidence,
                "RuntimeDecisionPreserved");

        if (!options.Enabled)
        {
            return preserved with
            {
                Source =
                    "MultivariateOperationalPromotionDisabled",

                Reasons =
                    [
                        "OperationalPromotionDisabled",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        if (options.CanaryOnly &&
            !IsClientAllowed(
                clienteId,
                options.AllowedClientIds))
        {
            return preserved with
            {
                Source =
                    "MultivariateOperationalPromotionCanaryExcluded",

                Reasons =
                    [
                        "ClientNotEnabledForOperationalPromotion",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        if (currentDecision.EmitAnomaly)
        {
            return preserved with
            {
                Source =
                    "MultivariateOperationalCorroboration",

                Reasons =
                    [
                        "RuntimeAlreadyEmitsAnomaly",
                        "NoOperationalPromotionRequired"
                    ]
            };
        }

        if (counterfactual.OriginalEmitAnomaly !=
                currentDecision.EmitAnomaly ||
            counterfactual.OriginalEmitNewEvidence !=
                currentDecision.EmitNewEvidence)
        {
            return preserved with
            {
                Source =
                    "MultivariateOperationalStaleCounterfactual",

                Reasons =
                    [
                        "CounterfactualDoesNotMatchCurrentRuntimeDecision",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        if (!counterfactual.WouldChangeDecision ||
            !counterfactual.WouldEmitAnomaly)
        {
            return preserved with
            {
                Source =
                    "MultivariateOperationalNoPromotionProposed",

                Reasons =
                    [
                        "CounterfactualDidNotProposeAnomalyPromotion",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        var evidence =
            counterfactual.Evidence;

        if (!IsEvidenceValid(
                evidence,
                clienteId,
                options.MaxEvidenceAgeMinutes))
        {
            return preserved with
            {
                Source =
                    "MultivariateOperationalInvalidEvidence",

                Reasons =
                    [
                        "MultivariateEvidenceFailedOperationalValidation",
                        "RuntimeDecisionPreserved"
                    ]
            };
        }

        var promotedDecision =
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly:
                    currentDecision.LegacyFinalAnomaly,

                EmitAnomaly:
                    true,

                EmitNewEvidence:
                    true,

                UsedLegacyFallback:
                    false,

                Source:
                    "MultivariateNewAnomaly",

                Reasons:
                    currentDecision.Reasons
                        .Concat(
                            [
                                "FreshMultivariateAnomalyAccepted",
                                $"MultivariateGroup:{evidence!.GroupName}",
                                "MultivariateOperationalPromotionAccepted"
                            ])
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray());

        return new MultivariateOperationalFusionResult
        {
            Decision =
                promotedDecision,

            PromotionApplied =
                true,

            UnivariateScore =
                univariateScore,

            MultivariateEvidence =
                evidence,

            Source =
                "MultivariateOperationalPromotionApplied",

            Reasons =
                [
                    "RuntimeWasNormal",
                    "CounterfactualProposedPromotion",
                    "OperationalEvidenceRevalidated",
                    "UnivariateScorePreserved",
                    "MultivariateScoreKeptSeparate"
                ]
        };
    }

    private static MultivariateOperationalFusionResult Preserve(
        RuntimeAnomalyDecision decision,
        double univariateScore,
        MultivariateDecisionEvidence? evidence,
        string source)
    {
        return new MultivariateOperationalFusionResult
        {
            Decision =
                decision,

            PromotionApplied =
                false,

            UnivariateScore =
                univariateScore,

            MultivariateEvidence =
                evidence,

            Source =
                source,

            Reasons =
                ["RuntimeDecisionPreserved"]
        };
    }

    private static bool IsEvidenceValid(
        MultivariateDecisionEvidence? evidence,
        string? clienteId,
        int maxEvidenceAgeMinutes)
    {
        if (evidence is null ||
            string.IsNullOrWhiteSpace(clienteId) ||
            string.IsNullOrWhiteSpace(evidence.ClienteId) ||
            string.IsNullOrWhiteSpace(evidence.GroupName))
        {
            return false;
        }

        if (!string.Equals(
                evidence.ClienteId.Trim(),
                clienteId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var maxAge =
            Math.Max(
                0,
                maxEvidenceAgeMinutes);

        if (evidence.AgeMinutes < 0 ||
            evidence.AgeMinutes > maxAge)
        {
            return false;
        }

        if (!double.IsFinite(evidence.Score) ||
            !double.IsFinite(evidence.Threshold) ||
            evidence.Score < evidence.Threshold)
        {
            return false;
        }

        if (!double.IsFinite(
                evidence.MahalanobisSquared) ||
            evidence.MahalanobisSquared < 0)
        {
            return false;
        }

        if (evidence.Tags is null ||
            evidence.Tags.Count == 0)
        {
            return false;
        }

        return true;
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

public sealed record MultivariateOperationalFusionResult
{
    public RuntimeAnomalyDecision Decision { get; init; } =
        default!;

    public bool PromotionApplied { get; init; }

    public double UnivariateScore { get; init; }

    public MultivariateDecisionEvidence? MultivariateEvidence { get; init; }

    public string Source { get; init; } =
        "";

    public IReadOnlyList<string> Reasons { get; init; } =
        Array.Empty<string>();
}
