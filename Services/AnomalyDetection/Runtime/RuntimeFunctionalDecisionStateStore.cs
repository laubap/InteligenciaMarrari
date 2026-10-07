using System.Collections.Concurrent;
using IAApi.Benchmark.ExperimentalIncidents;
using IAApi.Benchmark.RegimeShadow.DualBaseline;
using IAApi.RegimePolicy;
using IAApi.Services.AnomalyDetection.Runtime.Regime;

namespace IAApi.Services.AnomalyDetection.Runtime;

/// <summary>
/// Estado efÃªmero, por cliente/tag, usado pela primeira integraÃ§Ã£o runtime da
/// lÃ³gica funcional B3.18. NÃ£o persiste nada no banco e nÃ£o altera o baseline
/// operacional da aplicaÃ§Ã£o.
/// </summary>
public sealed class RuntimeFunctionalDecisionStateStore
{
    private readonly ConcurrentDictionary<string, RuntimeFunctionalTagState>
        _states = new(StringComparer.OrdinalIgnoreCase);

    internal RuntimeFunctionalTagState GetOrCreate(
        string clienteId,
        string tagName)
    {
        var key = Key(clienteId, tagName);

        return _states.GetOrAdd(
            key,
            _ => RuntimeFunctionalTagState.Create(clienteId, tagName));
    }

    public void Remove(string clienteId, string tagName) =>
        _states.TryRemove(Key(clienteId, tagName), out _);

    public void Clear() => _states.Clear();

    public int Count => _states.Count;

    private static string Key(string clienteId, string tagName) =>
        $"{clienteId}\u001f{tagName}";
}

internal sealed class RuntimeFunctionalTagState
{
    private RuntimeFunctionalTagState(
        RegimeShadowTagState regime,
        DualBaselineTagState dualBaseline)
    {
        Regime = regime;
        DualBaseline = dualBaseline;
    }

    public object Gate { get; } = new();

    public long Sequence { get; set; }

    public RegimeShadowTagState Regime { get; set; }

    public DualBaselineTagState DualBaseline { get; }

    public ExperimentalIncidentAggregateState? Incident { get; set; }

    public bool DecisionEpisodeActive { get; set; }

    public string? DecisionEpisodeId { get; set; }

    public bool StableUnresolvedStarted { get; set; }

    public bool IncreasingNoiseActive { get; set; }

    public int IncreasingNoiseConsecutiveMisses { get; set; }

    public static RuntimeFunctionalTagState Create(
        string clienteId,
        string tagName)
    {
        var activeId =
            $"runtime-active:{clienteId}:{tagName}";

        return new RuntimeFunctionalTagState(
            new RegimeShadowTagState(
                RegimePolicyState.Initial(activeId),
                CandidateEvidenceState.Empty),
            new DualBaselineTagState());
    }
}
