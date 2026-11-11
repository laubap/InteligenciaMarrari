using IAApi.IntelligenceLab.Observability;

namespace IAApi.IntelligenceLab.Observability.Application;

/// <summary>
/// Coletor opcional para a telemetria shadow multivariada. A implementação
/// deve ser fail-open: a observabilidade não pode alterar o pipeline.
/// </summary>
public interface IIntelligenceLabMultivariateShadowCollector
{
    void Record(MultivariateShadowDiagnostic diagnostic);
}
