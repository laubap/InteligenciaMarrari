using System.Threading;

namespace IAApi.Infrastructure.Observability;

/// <summary>
/// Contadores locais em memoria. Nao armazenam dados de tags nem detalhes de excecoes.
/// Um snapshot entre threads e uma fotografia eventual, nao uma transacao atomica.
/// </summary>
public sealed class IntelligenceLabHealthMetrics
{
    private long _captureAttempts;
    private long _captureFailures;
    private long _storedTotal;
    private long _rejectedTotal;
    private long _writeFailures;
    private long _skippedDisabled;
    private long _discardedStages;

    public void RecordCaptureAttempt() => Interlocked.Increment(ref _captureAttempts);
    public void RecordCaptureFailure() => Interlocked.Increment(ref _captureFailures);
    public void RecordRejected() => Interlocked.Increment(ref _rejectedTotal);
    public void RecordWriteFailure() => Interlocked.Increment(ref _writeFailures);
    public void RecordDiscardedStage() => Interlocked.Increment(ref _discardedStages);
    public void RecordSkippedDisabled() => Interlocked.Increment(ref _skippedDisabled);
    public void RecordStored() => Interlocked.Increment(ref _storedTotal);

    /// <summary>
    /// Inclui os numeros correntes do buffer fornecido. Usar o mesmo buffer do coletor.
    /// Totais sao cumulativos desde a criacao da instancia; nao representam leituras
    /// processadas antes da ativacao, nem garantem captura de todas as falhas externas.
    /// </summary>
    public IntelligenceLabHealthSnapshot Snapshot(BoundedDiagnosticBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (!ReferenceEquals(buffer.Health, this))
        {
            throw new ArgumentException("O buffer nao pertence a estes contadores.", nameof(buffer));
        }

        return new IntelligenceLabHealthSnapshot(
            buffer.Enabled,
            buffer.Capacity,
            buffer.Count,
            Interlocked.Read(ref _captureAttempts),
            Interlocked.Read(ref _storedTotal),
            buffer.EvictedCount,
            Interlocked.Read(ref _rejectedTotal),
            Interlocked.Read(ref _captureFailures),
            Interlocked.Read(ref _writeFailures),
            Interlocked.Read(ref _skippedDisabled),
            Interlocked.Read(ref _discardedStages));
    }
}

public sealed record IntelligenceLabHealthSnapshot(
    bool Enabled,
    int BufferCapacity,
    int BufferedCount,
    long CaptureAttempts,
    long StoredTotal,
    long EvictedTotal,
    long RejectedTotal,
    long CaptureFailures,
    long WriteFailures,
    long SkippedDisabled,
    long DiscardedStages);
