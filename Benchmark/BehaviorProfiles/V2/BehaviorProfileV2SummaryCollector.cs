using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.BehaviorProfiles.V2;

public sealed class BehaviorProfileV2SummaryCollector
{
    private readonly Dictionary<TagBehaviorType, long> _legacyCounts = [];
    private readonly Dictionary<TagBehaviorType, long> _v2Counts = [];
    private readonly Dictionary<TagBehaviorType, double> _legacyConfidence = [];
    private readonly Dictionary<TagBehaviorType, double> _v2Confidence = [];
    private readonly Dictionary<string, long> _matrix = [];
    private readonly Dictionary<string, long> _legacyTransitions = [];
    private readonly Dictionary<string, long> _v2Transitions = [];
    private readonly Dictionary<string, long> _divergences = [];
    private readonly Dictionary<string, Previous> _previousByTag =
        new(StringComparer.OrdinalIgnoreCase);
    private long _observations;
    private long _agreement;
    private long _legacyChanges;
    private long _v2Changes;
    private long _comparableTemporalTransitions;

    public void Add(BenchmarkBehaviorProfileObservation observation)
    {
        var legacy = observation.InferredBehaviorProfile.Type;
        var v2 = observation.InferredBehaviorProfileV2.Type;
        _observations++;
        Increment(_legacyCounts, legacy);
        Increment(_v2Counts, v2);
        Add(_legacyConfidence, legacy, observation.InferredBehaviorProfile.Confidence);
        Add(_v2Confidence, v2, observation.InferredBehaviorProfileV2.Confidence);
        Increment(_matrix, $"{legacy}->{v2}");

        if (legacy == v2)
            _agreement++;
        else
        {
            var reason = $"{legacy}->{v2}: {observation.InferredBehaviorProfileV2.Reason}";
            Increment(_divergences, reason);
        }

        if (_previousByTag.TryGetValue(observation.Tag, out var previous) &&
            previous.FastWindowIndex == observation.FastWindowIndex)
        {
            _comparableTemporalTransitions++;
            Increment(_legacyTransitions, $"{previous.Legacy}->{legacy}");
            Increment(_v2Transitions, $"{previous.V2}->{v2}");
            if (previous.Legacy != legacy) _legacyChanges++;
            if (previous.V2 != v2) _v2Changes++;
        }

        _previousByTag[observation.Tag] =
            new Previous(observation.FastWindowIndex, legacy, v2);
    }

    public BehaviorProfileV2Summary Build(
        Maturity.BehaviorProfileMaturitySummary? maturity = null)
    {
        return new BehaviorProfileV2Summary
        {
            ObservationCount = _observations,
            LegacyDistribution = Names(_legacyCounts),
            V2Distribution = Names(_v2Counts),
            LegacyAverageConfidenceByClass = Averages(_legacyCounts, _legacyConfidence),
            V2AverageConfidenceByClass = Averages(_v2Counts, _v2Confidence),
            LegacyClassChanges = _legacyChanges,
            V2ClassChanges = _v2Changes,
            LegacyFlipRate = Ratio(_legacyChanges, _comparableTemporalTransitions),
            V2FlipRate = Ratio(_v2Changes, _comparableTemporalTransitions),
            LegacyUnknownCount = Get(_legacyCounts, TagBehaviorType.Unknown),
            V2UnknownCount = Get(_v2Counts, TagBehaviorType.Unknown),
            LegacyUnknownRatio = Ratio(Get(_legacyCounts, TagBehaviorType.Unknown), _observations),
            V2UnknownRatio = Ratio(Get(_v2Counts, TagBehaviorType.Unknown), _observations),
            AgreementCount = _agreement,
            AgreementRatio = Ratio(_agreement, _observations),
            LegacyByV2Matrix = Ordered(_matrix),
            LegacyTemporalTransitions = Ordered(_legacyTransitions),
            V2TemporalTransitions = Ordered(_v2Transitions),
            PrincipalDivergenceReasons = _divergences
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .Take(20)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            Maturity = maturity ?? new Maturity.BehaviorProfileMaturitySummary()
        };
    }

    private static void Increment<TKey>(Dictionary<TKey, long> values, TKey key)
        where TKey : notnull => values[key] = values.GetValueOrDefault(key) + 1;
    private static void Add<TKey>(Dictionary<TKey, double> values, TKey key, double value)
        where TKey : notnull => values[key] = values.GetValueOrDefault(key) + value;
    private static long Get(Dictionary<TagBehaviorType, long> values, TagBehaviorType key) =>
        values.GetValueOrDefault(key);
    private static double Ratio(long numerator, long denominator) =>
        denominator > 0 ? numerator / (double)denominator : 0;
    private static Dictionary<string, long> Names(Dictionary<TagBehaviorType, long> values) =>
        values.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key.ToString(), pair => pair.Value);
    private static Dictionary<string, double> Averages(
        Dictionary<TagBehaviorType, long> counts,
        Dictionary<TagBehaviorType, double> sums) =>
        counts.OrderBy(pair => pair.Key).ToDictionary(
            pair => pair.Key.ToString(),
            pair => pair.Value > 0 ? sums.GetValueOrDefault(pair.Key) / pair.Value : 0);
    private static Dictionary<string, long> Ordered(Dictionary<string, long> values) =>
        values.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

    private sealed record Previous(
        int FastWindowIndex,
        TagBehaviorType Legacy,
        TagBehaviorType V2);
}
