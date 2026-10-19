namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// B3.20 — ponte segura entre o DetectorResult Legacy
/// e os efeitos operacionais do runtime, com rollout canário.
///
/// Esta classe NÃO modifica DetectorResult.
/// </summary>
public sealed class RuntimeAnomalyDecisionResolver
{
    public RuntimeAnomalyDecision Resolve(
        bool legacyFinalAnomaly,
        RuntimeAnomalyDecisionOptions options,
        RuntimeFunctionalDecisionCandidate? functionalCandidate = null)
    {
        return Resolve(
            legacyFinalAnomaly,
            options,
            clientId: null,
            functionalCandidate);
    }

    public RuntimeAnomalyDecision Resolve(
        bool legacyFinalAnomaly,
        RuntimeAnomalyDecisionOptions options,
        string? clientId,
        RuntimeFunctionalDecisionCandidate? functionalCandidate = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return Legacy(
                legacyFinalAnomaly,
                source: "LegacyFeatureDisabled",
                reason: "RuntimeDecisionDisabled");
        }

        if (options.CanaryOnly &&
            !IsCanaryAllowed(
                clientId,
                options.AllowedClientIds))
        {
            return Legacy(
                legacyFinalAnomaly,
                source: "LegacyCanaryExcluded",
                reason: "ClientNotEnabledForRuntimeDecision");
        }

        if (functionalCandidate is null)
        {
            return Legacy(
                legacyFinalAnomaly,
                source: "LegacyFallback",
                reason: "FunctionalDecisionUnavailable");
        }

        return new RuntimeAnomalyDecision(
            LegacyFinalAnomaly:
                legacyFinalAnomaly,
            EmitAnomaly:
                functionalCandidate.EmitAnomaly,
            EmitNewEvidence:
                functionalCandidate.EmitNewEvidence,
            UsedLegacyFallback:
                false,
            Source:
                functionalCandidate.Source,
            Reasons:
                functionalCandidate.Reasons
                    .Concat(["RuntimeFunctionalDecisionAccepted"])
                    .ToArray());
    }

    private static bool IsCanaryAllowed(
        string? clientId,
        IReadOnlyCollection<string>? allowedClientIds)
    {
        if (string.IsNullOrWhiteSpace(clientId) ||
            allowedClientIds is null ||
            allowedClientIds.Count == 0)
        {
            return false;
        }

        var normalizedClientId =
            clientId.Trim();

        return allowedClientIds.Any(
            allowed =>
                !string.IsNullOrWhiteSpace(allowed) &&
                string.Equals(
                    allowed.Trim(),
                    normalizedClientId,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static RuntimeAnomalyDecision Legacy(
        bool legacyFinalAnomaly,
        string source,
        string reason)
    {
        return new RuntimeAnomalyDecision(
            LegacyFinalAnomaly:
                legacyFinalAnomaly,
            EmitAnomaly:
                legacyFinalAnomaly,
            EmitNewEvidence:
                legacyFinalAnomaly,
            UsedLegacyFallback:
                true,
            Source:
                source,
            Reasons:
                [reason, "LegacyPreserved"]);
    }
}