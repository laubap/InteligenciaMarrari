using IAApi.IntelligenceLab.Observability;

namespace IAApi.Infrastructure.Observability;

/// <summary>
/// Armazenamento temporario e limitado de diagnosticos para ensaios locais.
/// Nao possui DI, endpoint, persistencia ou integracao com o pipeline.
/// Uma nova instancia com enabled=true deve ser criada explicitamente.
/// </summary>
public sealed class BoundedDiagnosticBuffer
{
    private readonly object _sync = new();
    private readonly Queue<IntelligenceLabReadingDiagnostic> _diagnostics = new();
    private readonly Queue<MultivariateShadowDiagnostic> _multivariateShadowDiagnostics = new();
    private long _evictedCount;
    private long _multivariateShadowEvictedCount;

    public BoundedDiagnosticBuffer(
        int capacity = 100,
        bool enabled = false,
        IntelligenceLabHealthMetrics? health = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
        Enabled = enabled;
        Health = health ?? new IntelligenceLabHealthMetrics();
    }

    public int Capacity { get; }

    public bool Enabled { get; }

    public IntelligenceLabHealthMetrics Health { get; }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _diagnostics.Count;
            }
        }
    }

    /// <summary>Quantidade de diagnosticos antigos removidos ao atingir a capacidade.</summary>
    public long EvictedCount
    {
        get
        {
            lock (_sync)
            {
                return _evictedCount;
            }
        }
    }

    public int MultivariateShadowCount
    {
        get
        {
            lock (_sync)
            {
                return _multivariateShadowDiagnostics.Count;
            }
        }
    }

    public long MultivariateShadowEvictedCount
    {
        get
        {
            lock (_sync)
            {
                return _multivariateShadowEvictedCount;
            }
        }
    }

    /// <summary>
    /// Retorna false quando desabilitado ou quando a referencia e nula.
    /// Nao interpreta scores, nao altera o diagnostico e nao publica dados.
    /// </summary>
    public bool TryAdd(IntelligenceLabReadingDiagnostic? diagnostic)
    {
        if (!Enabled)
        {
            Health.RecordSkippedDisabled();
            return false;
        }

        if (diagnostic is null)
        {
            Health.RecordRejected();
            return false;
        }

        lock (_sync)
        {
            if (_diagnostics.Count == Capacity)
            {
                _diagnostics.Dequeue();
                _evictedCount++;
            }

            _diagnostics.Enqueue(diagnostic);
            Health.RecordStored();
            return true;
        }
    }

    /// <summary>Devolve uma copia somente leitura, independente de novas gravacoes.</summary>
    public IReadOnlyList<IntelligenceLabReadingDiagnostic> Snapshot()
    {
        lock (_sync)
        {
            return Array.AsReadOnly(_diagnostics.ToArray());
        }
    }

    /// <summary>
    /// Retém uma trilha shadow separada da análise univariada. A capacidade é
    /// a mesma do buffer principal e a fila é independente para não expulsar
    /// diagnósticos univariados ao formar vetores posteriores.
    /// </summary>
    public bool TryAddMultivariateShadow(MultivariateShadowDiagnostic? diagnostic)
    {
        if (!Enabled)
        {
            Health.RecordSkippedDisabled();
            return false;
        }

        if (diagnostic is null)
        {
            Health.RecordRejected();
            return false;
        }

        lock (_sync)
        {
            if (_multivariateShadowDiagnostics.Count == Capacity)
            {
                _multivariateShadowDiagnostics.Dequeue();
                _multivariateShadowEvictedCount++;
            }

            _multivariateShadowDiagnostics.Enqueue(diagnostic);
            Health.RecordStored();
            return true;
        }
    }

    public IReadOnlyList<MultivariateShadowDiagnostic> SnapshotMultivariateShadow()
    {
        lock (_sync)
        {
            return Array.AsReadOnly(_multivariateShadowDiagnostics.ToArray());
        }
    }
}
