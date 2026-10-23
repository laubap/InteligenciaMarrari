using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;

namespace IAApi.Infrastructure.Observability;

/// <summary>
/// Coletor local e passivo. Nenhum registro em DI, endpoint ou publicador.
/// Por padrao utiliza um buffer desabilitado; habilitar exige um buffer explicito.
/// </summary>
public sealed class InMemoryDiagnosticCollector : IIntelligenceLabDiagnosticCollector
{
    private readonly BoundedDiagnosticBuffer _buffer;

    public InMemoryDiagnosticCollector(BoundedDiagnosticBuffer? buffer = null)
    {
        _buffer = buffer ?? new BoundedDiagnosticBuffer();
    }

    public IIntelligenceLabDiagnosticSession Begin(
        DiagnosticContext context,
        DiagnosticVersionMetadata version)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(version);

        return new InMemoryDiagnosticSession(_buffer, context, version);
    }
}

/// <summary>
/// Sessao isolada por leitura. Nunca interpreta scores, baselines ou votos.
/// A ausencia de buffer, rejeicoes e falhas de armazenamento nao alteram o
/// diagnostico recebido. A instrumentacao do pipeline ainda NAO existe.
/// </summary>
public sealed class InMemoryDiagnosticSession : IIntelligenceLabDiagnosticSession
{
    private const int MaxStageObservations = 32;

    private readonly object _sync = new();
    private readonly BoundedDiagnosticBuffer _buffer;
    private readonly DiagnosticContext _context;
    private readonly DiagnosticVersionMetadata _version;
    private readonly List<DiagnosticStageObservation> _stages = [];
    private IntelligenceLabReadingDiagnostic? _finalDiagnostic;
    private bool _finalized;
    private int _discardedStages;
    private int _rejectedDiagnostics;
    private int _failedWrites;

    public InMemoryDiagnosticSession(
        BoundedDiagnosticBuffer buffer,
        DiagnosticContext context,
        DiagnosticVersionMetadata version)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    public IReadOnlyList<DiagnosticStageObservation> StageObservations
    {
        get
        {
            lock (_sync)
            {
                return Array.AsReadOnly(_stages.ToArray());
            }
        }
    }

    public int DiscardedStageCount
    {
        get { lock (_sync) { return _discardedStages; } }
    }

    public int RejectedDiagnosticCount
    {
        get { lock (_sync) { return _rejectedDiagnostics; } }
    }

    public int FailedWriteCount
    {
        get { lock (_sync) { return _failedWrites; } }
    }

    public void RecordStage(DiagnosticStageObservation observation)
    {
        if (!_buffer.Enabled || observation is null)
        {
            return;
        }

        try
        {
            lock (_sync)
            {
                if (_finalized)
                {
                    return;
                }

                if (_stages.Count == MaxStageObservations)
                {
                    _discardedStages++;
                    return;
                }

                _stages.Add(observation);
            }
        }
        catch (Exception)
        {
            // A telemetria nunca interrompe a analise operacional.
        }
    }

    public IntelligenceLabReadingDiagnostic Complete(
        IntelligenceLabReadingDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        lock (_sync)
        {
            if (_finalized)
            {
                return _finalDiagnostic ?? diagnostic;
            }

            // Impede associar uma leitura de outro ensaio/sessao ao buffer.
            if (!Equals(diagnostic.Context, _context) ||
                !Equals(diagnostic.Version, _version))
            {
                _rejectedDiagnostics++;
                return diagnostic;
            }

            _finalized = true;
            _finalDiagnostic = diagnostic;

            try
            {
                _buffer.TryAdd(diagnostic);
            }
            catch (Exception)
            {
                // Falha de armazenamento nao modifica a decisao nem o retorno.
                _failedWrites++;
            }

            return diagnostic;
        }
    }

    public IntelligenceLabReadingDiagnostic MarkIncomplete(
        IntelligenceLabReadingDiagnostic diagnostic,
        DiagnosticCompleteness completeness)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (completeness is null ||
            completeness.Status == DiagnosticCompletenessStatus.Complete)
        {
            return diagnostic;
        }

        try
        {
            // A copia preserva os dados calculados pela IA; apenas a
            // classificacao de completude e alterada, sem mutar a origem.
            var revised = new IntelligenceLabReadingDiagnostic(
                diagnostic.Context,
                diagnostic.Version,
                diagnostic.ClienteId,
                diagnostic.TagName,
                diagnostic.ReadingTimestampUtc,
                diagnostic.ReadingValue,
                completeness,
                diagnostic.Timing,
                baseline: diagnostic.Baseline,
                historyQuality: diagnostic.HistoryQuality,
                detectors: diagnostic.Detectors,
                ensemble: diagnostic.Ensemble,
                runtimeDecision: diagnostic.RuntimeDecision,
                regime: diagnostic.Regime,
                behaviorMemory: diagnostic.BehaviorMemory,
                multivariate: diagnostic.Multivariate,
                failure: diagnostic.Failure,
                completedAtUtc: diagnostic.CompletedAtUtc);

            return Complete(revised);
        }
        catch (Exception)
        {
            // Falha diagnostica nao deve contaminar a analise operacional.
            return diagnostic;
        }
    }
}
