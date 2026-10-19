using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection.Runtime;

public sealed class PredictionRuntimeDecisionService
{
    private readonly RuntimeFunctionalDecisionCandidateFactory
        _runtimeFunctionalDecisionCandidateFactory;

    private readonly RuntimeAnomalyDecisionResolver
        _runtimeDecisionResolver;

    private readonly IOptions<RuntimeAnomalyDecisionOptions>
        _runtimeDecisionOptions;

    private readonly RuntimeDecisionMetrics
        _runtimeDecisionMetrics;

    public PredictionRuntimeDecisionService(
        RuntimeFunctionalDecisionCandidateFactory
            runtimeFunctionalDecisionCandidateFactory,
        RuntimeAnomalyDecisionResolver
            runtimeDecisionResolver,
        IOptions<RuntimeAnomalyDecisionOptions>
            runtimeDecisionOptions,
        RuntimeDecisionMetrics
            runtimeDecisionMetrics)
    {
        _runtimeFunctionalDecisionCandidateFactory =
            runtimeFunctionalDecisionCandidateFactory;

        _runtimeDecisionResolver =
            runtimeDecisionResolver;

        _runtimeDecisionOptions =
            runtimeDecisionOptions;

        _runtimeDecisionMetrics =
            runtimeDecisionMetrics;
    }

    public RuntimeAnomalyDecision Resolve(
        DetectorResult resultadoDetector,
        RuntimeFunctionalDecisionContext? runtimeFunctionalContext,
        string clienteId)
    {
var runtimeFunctionalCandidate =
    _runtimeFunctionalDecisionCandidateFactory.Create(
        resultadoDetector.EhAnomalia,
        runtimeFunctionalContext);

        var runtimeDecision =
            _runtimeDecisionResolver.Resolve(
                resultadoDetector,
                _runtimeDecisionOptions.Value,
                clienteId,
                runtimeFunctionalCandidate);

        _runtimeDecisionMetrics.Record(
            runtimeDecision);

        return runtimeDecision;
    }
}