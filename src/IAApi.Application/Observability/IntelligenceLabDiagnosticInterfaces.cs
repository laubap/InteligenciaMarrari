using IAApi.IntelligenceLab.Observability;

namespace IAApi.IntelligenceLab.Observability.Application;

/// <summary>
/// Contrato para a futura coleta incremental do diagnóstico de uma leitura.
/// A implementação será adicionada quando a instrumentação do pipeline for
/// explicitamente autorizada em etapa posterior.
/// </summary>
public interface IIntelligenceLabDiagnosticCollector
{
    IIntelligenceLabDiagnosticSession Begin(
        DiagnosticContext context,
        DiagnosticVersionMetadata version);
}

public interface IIntelligenceLabDiagnosticSession
{
    void RecordStage(DiagnosticStageObservation observation);

    IntelligenceLabReadingDiagnostic Complete(
        IntelligenceLabReadingDiagnostic diagnostic);

    IntelligenceLabReadingDiagnostic MarkIncomplete(
        IntelligenceLabReadingDiagnostic diagnostic,
        DiagnosticCompleteness completeness);
}

/// <summary>
/// Fronteira para publicação futura em fila, outbox ou armazenamento. Nenhuma
/// implementação é registrada nesta etapa, preservando o runtime atual.
/// </summary>
public interface IIntelligenceLabDiagnosticPublisher
{
    ValueTask<DiagnosticPublicationResult> PublishAsync(
        IntelligenceLabReadingDiagnostic diagnostic,
        CancellationToken cancellationToken = default);
}
