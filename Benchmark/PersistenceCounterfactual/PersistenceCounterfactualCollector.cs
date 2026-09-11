using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.PersistenceCounterfactual;

public sealed class PersistenceCounterfactualCollector
{
    private readonly List<PersistenceCounterfactualRecord> _records = [];
    private readonly Dictionary<PersistenceUnderlyingEvidenceKind, long> _readings = [];
    private readonly Dictionary<PersistenceUnderlyingEvidenceKind, long> _sequences = [];
    private readonly Dictionary<string, SequenceState> _sequenceByTag =
        new(StringComparer.OrdinalIgnoreCase);
    private long _evaluated;
    private long _legacyAnomalies;
    private long _counterfactualAnomalies;

    public IReadOnlyList<PersistenceCounterfactualRecord> Records => _records;

    public void ResetWindow() => _sequenceByTag.Clear();

    public void Add(int fastWindowIndex, int minuteIndex, string tag, DetectorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _evaluated++;
        var persistence = Find(result, "Persist");
        var mad = Find(result, "Mediana e MAD");
        var source = Source(persistence);
        Increment(_readings, source);
        ObserveSequence(tag, minuteIndex, persistence, source);

        var legacyAnomaly = result.EhAnomalia;
        if (legacyAnomaly) _legacyAnomalies++;
        var remove = mad?.VotouAnomalia == true &&
                     persistence?.VotouAnomalia == true &&
                     source == PersistenceUnderlyingEvidenceKind.Mad &&
                     persistence?.Aplicavel == true;
        var counterfactualScore = remove
            ? Without(result.DetalhesDetectores, persistence!)
            : result.ScoreNormalizado;
        var threshold = result.Metricas.TryGetValue("limiteEnsemble", out var configured)
            ? configured : 60;
        var counterfactualAnomaly = counterfactualScore >= threshold;
        if (counterfactualAnomaly) _counterfactualAnomalies++;

        if (!legacyAnomaly && !counterfactualAnomaly) return;
        _records.Add(new PersistenceCounterfactualRecord
        {
            FastWindowIndex = fastWindowIndex,
            MinuteIndex = minuteIndex,
            Tag = tag,
            LegacyScore = result.ScoreNormalizado,
            LegacyAnomaly = legacyAnomaly,
            CounterfactualScore = counterfactualScore,
            CounterfactualAnomaly = counterfactualAnomaly,
            PersistenceContributionRemoved = remove,
            UnderlyingEvidenceKind = source,
            MadAnomalous = mad?.VotouAnomalia == true,
            PersistenceAnomalous = persistence?.VotouAnomalia == true,
            PersistenceScore = persistence?.ScoreNormalizado ?? 0,
            PersistenceConsecutivePoints = (int)Metric(persistence, "pontosConsecutivos"),
            PersistenceDurationMinutes = Metric(persistence, "duracaoMinutos"),
            AnomalousDetectors = result.DetalhesDetectores
                .Where(x => x.VotouAnomalia).Select(x => x.Nome).ToArray()
        });
    }

    public PersistenceCounterfactualSummary BuildSummary() => new()
    {
        GeneratedUtc = DateTime.UtcNow,
        EvaluatedReadings = _evaluated,
        LegacyAnomalyRecords = _legacyAnomalies,
        CounterfactualAnomalyRecords = _counterfactualAnomalies,
        RemovedAnomalyRecords = _records.Count(x =>
            x.LegacyAnomaly && !x.CounterfactualAnomaly),
        AddedAnomalyRecords = _records.Count(x =>
            !x.LegacyAnomaly && x.CounterfactualAnomaly),
        EvidenceReadings = ToStrings(_readings),
        EvidenceSequences = ToStrings(_sequences)
    };

    public static double Without(IReadOnlyCollection<DetectorDebugItem> detectors,
        DetectorDebugItem persistence)
    {
        var included = detectors.Where(x => x.Aplicavel && !ReferenceEquals(x, persistence)).ToArray();
        var denominator = included.Sum(x => x.Peso);
        return denominator <= 0 ? 0 : included.Sum(x => x.ScoreNormalizado * x.Peso) / denominator;
    }

    private void ObserveSequence(string tag, int minuteIndex, DetectorDebugItem? persistence,
        PersistenceUnderlyingEvidenceKind source)
    {
        var points = (int)Metric(persistence, "pontosConsecutivos");
        if (points <= 0)
        {
            _sequenceByTag.Remove(tag);
            return;
        }
        var starts = !_sequenceByTag.TryGetValue(tag, out var previous) ||
                     previous.MinuteIndex != minuteIndex - 1 || previous.Source != source || points <= 1;
        if (starts) Increment(_sequences, source);
        _sequenceByTag[tag] = new SequenceState(minuteIndex, source);
    }

    private static PersistenceUnderlyingEvidenceKind Source(DetectorDebugItem? item)
    {
        if (item?.Aplicavel != true) return PersistenceUnderlyingEvidenceKind.NotApplicable;
        if (Metric(item, "usaMad") == 1) return PersistenceUnderlyingEvidenceKind.Mad;
        if (Metric(item, "usaDeadbandComoFronteira") == 1)
            return PersistenceUnderlyingEvidenceKind.Deadband;
        if (Metric(item, "escalaFallback") > 0)
            return PersistenceUnderlyingEvidenceKind.StandardDeviationFallback;
        return PersistenceUnderlyingEvidenceKind.Unknown;
    }

    private static DetectorDebugItem? Find(DetectorResult result, string name) =>
        result.DetalhesDetectores.FirstOrDefault(x =>
            x.Nome.Contains(name, StringComparison.OrdinalIgnoreCase));

    private static double Metric(DetectorDebugItem? item, string name) =>
        item is not null && item.Metricas.TryGetValue(name, out var value) ? value : 0;

    private static void Increment(Dictionary<PersistenceUnderlyingEvidenceKind, long> values,
        PersistenceUnderlyingEvidenceKind key) =>
        values[key] = values.TryGetValue(key, out var current) ? current + 1 : 1;

    private static IReadOnlyDictionary<string, long> ToStrings(
        Dictionary<PersistenceUnderlyingEvidenceKind, long> values) => values
        .OrderBy(x => x.Key).ToDictionary(x => x.Key.ToString(), x => x.Value);

    private sealed record SequenceState(int MinuteIndex,
        PersistenceUnderlyingEvidenceKind Source);
}
