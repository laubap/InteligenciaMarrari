using Microsoft.Extensions.Options;
using IAApi.Services.AnomalyDetection.Runtime;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV06.2
///
/// Ponte SOMENTE OBSERVACIONAL entre o runtime operacional atual
/// e a regra counterfactual de fusão multivariada.
///
/// Não altera RuntimeAnomalyDecision.
/// Não altera DetectorResult.
/// Não dispara risco, persistência, notificação ou LLM.
/// </summary>
public sealed class MultivariateDecisionCounterfactualObserver
{
    private readonly MultivariateDecisionCounterfactualEvaluator
        _evaluator;

    private readonly IOptions<MultivariateDecisionFusionOptions>
        _options;

    private readonly ILogger<MultivariateDecisionCounterfactualObserver>
        _logger;

    public MultivariateDecisionCounterfactualObserver(
        MultivariateDecisionCounterfactualEvaluator evaluator,
        IOptions<MultivariateDecisionFusionOptions> options,
        ILogger<MultivariateDecisionCounterfactualObserver> logger)
    {
        _evaluator =
            evaluator ??
            throw new ArgumentNullException(nameof(evaluator));

        _options =
            options ??
            throw new ArgumentNullException(nameof(options));

        _logger =
            logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public MultivariateDecisionCounterfactual Observe(
        RuntimeAnomalyDecision currentDecision,
        string clienteId,
        string tagName,
        DateTime currentDataHora,
        IReadOnlyList<MultivariateDetectionResult>? multivariateResults)
    {
        ArgumentNullException.ThrowIfNull(currentDecision);

        var result =
            _evaluator.Evaluate(
                currentDecision,
                _options.Value,
                clienteId,
                tagName,
                currentDataHora,
                multivariateResults);

        /*
         * Evita poluir o log para cada leitura sem evidência.
         * Registramos somente quando existe evidência multivariada
         * elegível e fresca para a tag atual.
         */
        if (result.Evidence is null)
        {
            return result;
        }

        _logger.LogInformation(
            "B3.24.2 MULTIVARIATE FUSION COUNTERFACTUAL " +
            "Cliente={ClienteId} Tag={TagName} " +
            "ActualEmitAnomaly={ActualEmitAnomaly} " +
            "WouldEmitAnomaly={WouldEmitAnomaly} " +
            "ActualEmitNewEvidence={ActualEmitNewEvidence} " +
            "WouldEmitNewEvidence={WouldEmitNewEvidence} " +
            "WouldChange={WouldChange} Source={Source} " +
            "Grupo={GroupName} VectorMinute={MinuteIndex} " +
            "AgeMinutes={AgeMinutes} Score={Score:F2} " +
            "Threshold={Threshold:F2} " +
            "MahalanobisSquared={MahalanobisSquared:F4}",
            clienteId,
            tagName,
            result.OriginalEmitAnomaly,
            result.WouldEmitAnomaly,
            result.OriginalEmitNewEvidence,
            result.WouldEmitNewEvidence,
            result.WouldChangeDecision,
            result.Source,
            result.Evidence.GroupName,
            result.Evidence.MinuteIndex,
            result.Evidence.AgeMinutes,
            result.Evidence.Score,
            result.Evidence.Threshold,
            result.Evidence.MahalanobisSquared);

        return result;
    }
}
