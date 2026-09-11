using IAApi.Benchmark.BehaviorProfiles;
using IAApi.Benchmark.BehaviorProfiles.V2.Readiness;
using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.DetectorApplicabilityCounterfactual;

/// <summary>
/// Analise pura de politica. Os probes existem apenas para consultar a policy;
/// Detectar sempre lanca e nunca e chamado por este componente.
/// </summary>
public sealed class DetectorApplicabilityCounterfactualCollector
{
    public static readonly IReadOnlyList<string> DetectorNames =
    [
        "Z-Score", "MAD", "EWMA", "ROC", "Persistence", "LevelShift", "IsolationForest"
    ];

    private readonly DetectorApplicabilityPolicy _policy = new();
    private readonly IReadOnlyList<IAnomalyDetector> _probes =
        DetectorNames.Select(name => (IAnomalyDetector)new PolicyProbeDetector(name)).ToArray();
    private readonly List<DetectorApplicabilityCounterfactualRecord> _differences = [];
    private readonly Dictionary<(string Tag, int Minute), DetectorApplicabilityCounterfactualRecord>
        _differenceByReading = new();
    private readonly Dictionary<string, MutableTagSummary> _tags =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<DetectorApplicabilityCounterfactualRecord> Differences => _differences;

    public void Add(BenchmarkBehaviorProfileObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var tag = Tag(observation.Tag);
        tag.Evaluated++;

        var legacy = Profile(
            observation.InferredBehaviorProfile.Type,
            observation.InferredBehaviorProfile.Confidence,
            observation.InferredBehaviorProfile.SampleCount,
            "Inferencia Legacy observada pelo benchmark.");
        var legacyApplicable = Applicable(legacy);
        var ready = observation.DecisionReadiness.Status is
            BehaviorProfileReadinessStatus.ReadyObserved or
            BehaviorProfileReadinessStatus.ReadyDeclaredAndObserved;

        IReadOnlyList<string> prospectiveApplicable = legacyApplicable;
        if (ready)
        {
            tag.Ready++;
            var prospective = Profile(
                observation.DecisionReadiness.ProspectiveProfile,
                observation.StableBehaviorProfile.StableConfidence,
                observation.InferredBehaviorProfileV2.Features.SampleCount,
                observation.DecisionReadiness.Reason);
            prospectiveApplicable = Applicable(prospective);
        }

        var added = prospectiveApplicable.Except(legacyApplicable, StringComparer.Ordinal).ToArray();
        var removed = legacyApplicable.Except(prospectiveApplicable, StringComparer.Ordinal).ToArray();
        if (added.Length == 0 && removed.Length == 0) return;

        tag.Different++;
        var reason = $"{observation.DecisionReadiness.Status}: " +
                     $"{observation.InferredBehaviorProfile.Type} -> " +
                     $"{observation.DecisionReadiness.ProspectiveProfile}.";
        var record = new DetectorApplicabilityCounterfactualRecord
        {
            FastWindowIndex = observation.FastWindowIndex,
            Tag = observation.Tag,
            MinuteIndex = observation.MinuteIndex,
            LegacyProfile = observation.InferredBehaviorProfile.Type,
            ProspectiveProfile = observation.DecisionReadiness.ProspectiveProfile,
            Readiness = observation.DecisionReadiness.Status,
            LegacyApplicableDetectors = legacyApplicable,
            ProspectiveApplicableDetectors = prospectiveApplicable,
            AddedDetectors = added,
            RemovedDetectors = removed,
            Reason = reason
        };
        _differences.Add(record);
        _differenceByReading[(observation.Tag.ToUpperInvariant(), observation.MinuteIndex)] = record;
    }

    public void RecordOriginalDecision(string tag, int minuteIndex, DetectorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (_differenceByReading.TryGetValue((tag.ToUpperInvariant(), minuteIndex), out var record))
        {
            record.OriginalFinalAnomaly = result.EhAnomalia;
            record.OriginalFinalScore = result.ScoreNormalizado;
        }
    }

    public DetectorApplicabilityCounterfactualSummary BuildSummary()
    {
        var tagSummaries = _tags.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => BuildTagSummary(x.Key, x.Value)).ToArray();
        var evaluated = _tags.Values.Sum(x => x.Evaluated);
        var added = Counts(_differences.SelectMany(x => x.AddedDetectors));
        var removed = Counts(_differences.SelectMany(x => x.RemovedDetectors));
        return new DetectorApplicabilityCounterfactualSummary
        {
            GeneratedUtc = DateTime.UtcNow,
            EvaluatedReadings = evaluated,
            ReadyReadings = _tags.Values.Sum(x => x.Ready),
            DifferentApplicabilityReadings = _differences.Count,
            PotentiallyAffectedPercent = evaluated == 0 ? 0 : _differences.Count * 100.0 / evaluated,
            MostFrequentlyAddedDetector = MostFrequent(added),
            MostFrequentlyRemovedDetector = MostFrequent(removed),
            AddedDetectorCounts = added,
            RemovedDetectorCounts = removed,
            DifferencesByProspectiveProfile = Counts(_differences.Select(x => x.ProspectiveProfile.ToString())),
            DifferencesDuringOriginalAnomaly = _differences.Count(x => x.OriginalFinalAnomaly == true),
            Tags = tagSummaries
        };
    }

    private DetectorApplicabilityTagSummary BuildTagSummary(string tag, MutableTagSummary counts)
    {
        var rows = _differences.Where(x => x.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.FastWindowIndex).ThenBy(x => x.MinuteIndex).ToArray();
        return new DetectorApplicabilityTagSummary
        {
            Tag = tag,
            EvaluatedReadings = counts.Evaluated,
            ReadyReadings = counts.Ready,
            DifferentApplicabilityReadings = counts.Different,
            AddedDetectorCounts = Counts(rows.SelectMany(x => x.AddedDetectors)),
            RemovedDetectorCounts = Counts(rows.SelectMany(x => x.RemovedDetectors)),
            MaximumChangeDurations = Durations(rows),
            DifferenceReasons = Counts(rows.Select(x => x.Reason))
        };
    }

    private IReadOnlyList<string> Applicable(TagBehaviorProfile profile) => _probes
        .Where(probe => _policy.Evaluate(probe, profile).IsAllowed)
        .Select(probe => probe.Nome).ToArray();

    private static TagBehaviorProfile Profile(TagBehaviorType type, double confidence,
        int sampleCount, string reason) => new()
    {
        Type = type,
        Confidence = confidence,
        SampleCount = sampleCount,
        Reason = reason
    };

    private MutableTagSummary Tag(string tag)
    {
        if (!_tags.TryGetValue(tag, out var value))
            _tags[tag] = value = new MutableTagSummary();
        return value;
    }

    private static IReadOnlyDictionary<string, long> Counts(IEnumerable<string> values) => values
        .GroupBy(x => x, StringComparer.Ordinal)
        .OrderBy(x => x.Key, StringComparer.Ordinal)
        .ToDictionary(x => x.Key, x => (long)x.Count(), StringComparer.Ordinal);

    private static string? MostFrequent(IReadOnlyDictionary<string, long> values) => values
        .OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)
        .Select(x => x.Key).FirstOrDefault();

    private static IReadOnlyList<DetectorApplicabilityChangeDuration> Durations(
        IReadOnlyList<DetectorApplicabilityCounterfactualRecord> rows)
    {
        var maxima = new Dictionary<string, (string[] Added, string[] Removed, int Max)>();
        DetectorApplicabilityCounterfactualRecord? previous = null;
        string? previousKey = null;
        var run = 0;
        foreach (var row in rows)
        {
            var key = string.Join('|', row.AddedDetectors) + ">" + string.Join('|', row.RemovedDetectors);
            var consecutive = previous is not null && previous.FastWindowIndex == row.FastWindowIndex &&
                              row.MinuteIndex == previous.MinuteIndex + 1 && key == previousKey;
            run = consecutive ? run + 1 : 1;
            if (!maxima.TryGetValue(key, out var current) || run > current.Max)
                maxima[key] = (row.AddedDetectors.ToArray(), row.RemovedDetectors.ToArray(), run);
            previous = row;
            previousKey = key;
        }
        return maxima.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x =>
            new DetectorApplicabilityChangeDuration
            {
                AddedDetectors = x.Value.Added,
                RemovedDetectors = x.Value.Removed,
                MaximumDurationMinutes = x.Value.Max
            }).ToArray();
    }

    private sealed class MutableTagSummary
    {
        public long Evaluated;
        public long Ready;
        public long Different;
    }

    private sealed class PolicyProbeDetector(string name) : IAnomalyDetector
    {
        public string Nome { get; } = name;
        public double Peso => 0;
        public DetectorResult Detectar(DetectorInput input) => throw new InvalidOperationException(
            "Probe contrafactual nunca pode executar detector.");
    }
}
