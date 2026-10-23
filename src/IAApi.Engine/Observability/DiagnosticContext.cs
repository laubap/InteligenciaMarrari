namespace IAApi.IntelligenceLab.Observability;

/// <summary>
/// Identidade imutável de uma leitura observada pelo Intelligence Lab.
/// Os valores são independentes do contrato HTTP atual da IAApi.
/// </summary>
public sealed record DiagnosticContext
{
    private DiagnosticContext(
        Guid diagnosticId,
        string correlationId,
        string? runId,
        DateTimeOffset receivedAtUtc)
    {
        if (diagnosticId == Guid.Empty)
        {
            throw new ArgumentException(
                "DiagnosticId deve ser diferente de vazio.",
                nameof(diagnosticId));
        }

        DiagnosticContractGuard.Required(
            correlationId,
            nameof(correlationId));

        DiagnosticId = diagnosticId;
        CorrelationId = correlationId.Trim();
        RunId = string.IsNullOrWhiteSpace(runId)
            ? null
            : runId.Trim();
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
    }

    public Guid DiagnosticId { get; }

    public string CorrelationId { get; }

    /// <summary>
    /// Identificador opcional do ensaio do simulador. Não é usado pela IA
    /// para inferência nem representa a verdade do evento industrial.
    /// </summary>
    public string? RunId { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public static DiagnosticContext Create(
        string correlationId,
        string? runId,
        DateTimeOffset receivedAtUtc,
        Guid? diagnosticId = null)
    {
        return new DiagnosticContext(
            diagnosticId ?? Guid.NewGuid(),
            correlationId,
            runId,
            receivedAtUtc);
    }

    public static DiagnosticContext CreateNew(
        string? runId = null,
        DateTimeOffset? receivedAtUtc = null)
    {
        return Create(
            Guid.NewGuid().ToString("N"),
            runId,
            receivedAtUtc ?? DateTimeOffset.UtcNow);
    }
}
