using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;

namespace IAApi.Infrastructure.Observability;

public sealed class InMemoryMultivariateShadowDiagnosticCollector
    : IIntelligenceLabMultivariateShadowCollector
{
    private readonly BoundedDiagnosticBuffer _buffer;

    public InMemoryMultivariateShadowDiagnosticCollector(
        BoundedDiagnosticBuffer buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public void Record(MultivariateShadowDiagnostic diagnostic)
    {
        if (diagnostic is null)
        {
            return;
        }

        try
        {
            _buffer.TryAddMultivariateShadow(diagnostic);
        }
        catch (Exception)
        {
            // A trilha local nunca interfere com o runtime industrial.
        }
    }
}
