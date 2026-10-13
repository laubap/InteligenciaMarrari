using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Multivariate;

public sealed class PredictionMultivariateDecisionService
{
    private readonly MultivariateDecisionCounterfactualObserver?
        _counterfactualObserver;

    private readonly MultivariateOperationalFusionResolver?
        _fusionResolver;

    private readonly IOptions<MultivariateOperationalPromotionOptions>?
        _promotionOptions;

    private readonly MultivariateOperationalReadinessService?
        _readinessService;

    private readonly IOptions<MultivariateOperationalReadinessOptions>?
        _readinessOptions;

    public PredictionMultivariateDecisionService(
        MultivariateDecisionCounterfactualObserver?
            counterfactualObserver = null,
        MultivariateOperationalFusionResolver?
            fusionResolver = null,
        IOptions<MultivariateOperationalPromotionOptions>?
            promotionOptions = null,
        MultivariateOperationalReadinessService?
            readinessService = null,
        IOptions<MultivariateOperationalReadinessOptions>?
            readinessOptions = null)
    {
        _counterfactualObserver =
            counterfactualObserver;

        _fusionResolver =
            fusionResolver;

        _promotionOptions =
            promotionOptions;

        _readinessService =
            readinessService;

        _readinessOptions =
            readinessOptions;
    }

    public async Task<PredictionMultivariateDecisionResult> ResolveAsync(
        RuntimeAnomalyDecision runtimeDecision,
        string clienteId,
        string tagName,
        DateTime dataHoraAtual,
        IReadOnlyList<MultivariateDetectionResult>
            multivariateShadowResults,
        double univariateScore,
        bool multivariateInterpretationAvailable,
        CancellationToken cancellationToken = default)
    {
        MultivariateDecisionCounterfactual?
            multivariateCounterfactual =
                null;

        if (_counterfactualObserver is not null &&
            multivariateShadowResults.Count > 0)
        {
            try
            {
                multivariateCounterfactual =
                    _counterfactualObserver.Observe(
                        runtimeDecision,
                        clienteId,
                        tagName,
                        dataHoraAtual,
                        multivariateShadowResults);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"B3.24.2 MULTIVARIATE FUSION COUNTERFACTUAL FAIL-OPEN " +
                    $"Cliente={clienteId} " +
                    $"Tag={tagName} " +
                    $"Erro={ex.Message}");
            }
        }

        MultivariateOperationalFusionResult?
            multivariateOperationalFusion =
                null;

        if (_fusionResolver is not null &&
            _promotionOptions is not null &&
            multivariateCounterfactual is not null)
        {
            try
            {
                var resolvedFusion =
                    _fusionResolver.Resolve(
                        runtimeDecision,
                        multivariateCounterfactual,
                        _promotionOptions.Value,
                        clienteId,
                        univariateScore);

                var podeAplicarResolvedFusion =
                    true;

                var fusionLogSource =
                    resolvedFusion.Source;

                if (resolvedFusion.PromotionApplied)
                {
                    if (!multivariateInterpretationAvailable ||
                        resolvedFusion.MultivariateEvidence is null)
                    {
                        podeAplicarResolvedFusion =
                            false;

                        fusionLogSource =
                            "MultivariateInterpretationUnavailable";

                        Console.WriteLine(
                            $"B3.24.2 MULTIVARIATE OPERATIONAL FUSION BLOCKED " +
                            $"Cliente={clienteId} " +
                            $"Tag={tagName} " +
                            "Reason=MultivariateInterpretationUnavailable");
                    }
                    else if (_readinessService is null ||
                             _readinessOptions is null)
                    {
                        podeAplicarResolvedFusion =
                            false;

                        fusionLogSource =
                            "MultivariateOperationalReadinessUnavailable";

                        Console.WriteLine(
                            $"B3.24.2 MULTIVARIATE OPERATIONAL READINESS BLOCKED " +
                            $"Cliente={clienteId} " +
                            $"Tag={tagName} " +
                            $"Grupo={resolvedFusion.MultivariateEvidence.GroupName} " +
                            "Reason=ReadinessServiceUnavailable");
                    }
                    else
                    {
                        var readiness =
                            await _readinessService
                                .AvaliarAsync(
                                    clienteId,
                                    resolvedFusion.MultivariateEvidence.GroupName,
                                    _readinessOptions.Value,
                                    cancellationToken:
                                        cancellationToken);

                        if (!readiness.ReadyForOperationalAuthority)
                        {
                            podeAplicarResolvedFusion =
                                false;

                            fusionLogSource =
                                "MultivariateOperationalReadinessBlocked";

                            var blockers =
                                readiness.Blockers.Count == 0
                                    ? "-"
                                    : string.Join(
                                        ",",
                                        readiness.Blockers);

                            Console.WriteLine(
                                $"B3.24.2 MULTIVARIATE OPERATIONAL READINESS BLOCKED " +
                                $"Cliente={clienteId} " +
                                $"Tag={tagName} " +
                                $"Grupo={resolvedFusion.MultivariateEvidence.GroupName} " +
                                $"Blockers={blockers}");
                        }
                        else
                        {
                            Console.WriteLine(
                                $"B3.24.2 MULTIVARIATE OPERATIONAL READINESS READY " +
                                $"Cliente={clienteId} " +
                                $"Tag={tagName} " +
                                $"Grupo={resolvedFusion.MultivariateEvidence.GroupName} " +
                                $"SampleCount={readiness.ModelSampleCount}");
                        }
                    }
                }

                if (podeAplicarResolvedFusion)
                {
                    multivariateOperationalFusion =
                        resolvedFusion;

                    runtimeDecision =
                        resolvedFusion.Decision;
                }

                Console.WriteLine(
                    $"B3.24.2 MULTIVARIATE OPERATIONAL FUSION " +
                    $"Cliente={clienteId} " +
                    $"Tag={tagName} " +
                    $"Applied={multivariateOperationalFusion?.PromotionApplied ?? false} " +
                    $"Source={fusionLogSource} " +
                    $"EmitAnomaly={runtimeDecision.EmitAnomaly} " +
                    $"EmitNewEvidence={runtimeDecision.EmitNewEvidence} " +
                    $"UnivariateScore={resolvedFusion.UnivariateScore:F4} " +
                    $"MultivariateScore=" +
                    $"{resolvedFusion.MultivariateEvidence?.Score.ToString("F2") ?? "-"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"B3.24.2 MULTIVARIATE OPERATIONAL FUSION FAIL-OPEN " +
                    $"Cliente={clienteId} " +
                    $"Tag={tagName} " +
                    $"Erro={ex.Message}");
            }
        }

        return new PredictionMultivariateDecisionResult(
            runtimeDecision,
            multivariateOperationalFusion);
    }
}

public sealed record PredictionMultivariateDecisionResult(
    RuntimeAnomalyDecision RuntimeDecision,
    MultivariateOperationalFusionResult? OperationalFusion);