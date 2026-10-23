namespace IAApi.IntelligenceLab.Observability;

/// <summary>
/// Estado observado da aquisição de uma amostra industrial.
/// Não expressa a decisão dos detectores nem a verdade do ensaio.
/// </summary>
public enum ReadingAcquisitionStatus
{
    Valid = 1,
    Missing = 2,
    Invalid = 3
}

/// <summary>
/// Registro de aquisição independente do diagnóstico de inferência.
/// Ausência ou rejeição nunca são representadas por um valor artificial (como zero).
/// A detecção de uma amostra ausente depende de uma futura fonte de cadência/expectativa.
/// </summary>
public sealed record ReadingAcquisitionDiagnostic
{
    public ReadingAcquisitionDiagnostic(
        DiagnosticContext context,
        string clienteId,
        string tagName,
        ReadingAcquisitionStatus status,
        double? validValue,
        DateTimeOffset? sourceTimestampUtc = null,
        string? reasonCode = null,
        string? safeReason = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ClienteId = DiagnosticContractGuard.Required(clienteId, nameof(clienteId));
        TagName = DiagnosticContractGuard.Required(tagName, nameof(tagName));

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status == ReadingAcquisitionStatus.Valid)
        {
            if (!validValue.HasValue)
            {
                throw new ArgumentException(
                    "Uma leitura válida deve informar o valor numérico.",
                    nameof(validValue));
            }

            DiagnosticContractGuard.Finite(validValue.Value, nameof(validValue));

            if (!sourceTimestampUtc.HasValue)
            {
                throw new ArgumentException(
                    "Uma leitura válida deve informar o timestamp de origem.",
                    nameof(sourceTimestampUtc));
            }

            if (reasonCode is not null || safeReason is not null)
            {
                throw new ArgumentException(
                    "Uma leitura válida não pode conter motivo de rejeição.",
                    nameof(reasonCode));
            }
        }
        else
        {
            if (validValue.HasValue)
            {
                throw new ArgumentException(
                    "Uma leitura ausente ou inválida não pode conter valor válido.",
                    nameof(validValue));
            }

            if (status == ReadingAcquisitionStatus.Missing && sourceTimestampUtc.HasValue)
            {
                throw new ArgumentException(
                    "Uma leitura ausente não possui timestamp de origem da amostra.",
                    nameof(sourceTimestampUtc));
            }

            reasonCode = DiagnosticContractGuard.Required(reasonCode!, nameof(reasonCode));
            safeReason = DiagnosticContractGuard.Required(safeReason!, nameof(safeReason));
        }

        Status = status;
        ValidValue = validValue;
        SourceTimestampUtc = sourceTimestampUtc?.ToUniversalTime();
        ReasonCode = reasonCode;
        SafeReason = safeReason;
    }

    public DiagnosticContext Context { get; }
    public string ClienteId { get; }
    public string TagName { get; }
    public ReadingAcquisitionStatus Status { get; }
    public double? ValidValue { get; }
    public DateTimeOffset? SourceTimestampUtc { get; }

    /// <summary>
    /// Código e mensagem controlados pelo coletor; não devem incluir payload bruto,
    /// detalhes internos de exceções ou informações sensíveis.
    /// </summary>
    public string? ReasonCode { get; }
    public string? SafeReason { get; }
}
