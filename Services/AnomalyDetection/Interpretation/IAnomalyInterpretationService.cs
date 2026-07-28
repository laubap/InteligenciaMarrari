namespace IAApi.Services.AnomalyDetection.Interpretation;

public interface IAnomalyInterpretationService
{
    Task<AnomalyInterpretationResult> InterpretarAsync(
        AnomalyInterpretationContext contexto,
        CancellationToken cancellationToken = default
    );
}