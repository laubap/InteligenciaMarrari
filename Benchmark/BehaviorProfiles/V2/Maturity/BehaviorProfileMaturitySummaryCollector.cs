using IAApi.Services.AnomalyDetection;

namespace IAApi.Benchmark.BehaviorProfiles.V2.Maturity;

public sealed class BehaviorProfileMaturitySummaryCollector
{
    private readonly BehaviorProfileMaturityOptions _options;
    private readonly Dictionary<TagBehaviorType, long> _stableCounts = [];
    private readonly Dictionary<BehaviorProfileMaturityLifecycle, long> _lifecycleCounts = [];
    private readonly Dictionary<string, Timeline> _timelines = [];
    private long _observations;
    private long _prematureAvoided;
    private long _rawChanges;
    private long _stableChanges;
    private long _postMaturityObservations;
    private long _postMaturityStableObservations;

    public BehaviorProfileMaturitySummaryCollector(
        BehaviorProfileMaturityOptions options)
    {
        _options = options;
    }

    public void Add(BenchmarkBehaviorProfileObservation observation)
    {
        var stable = observation.StableBehaviorProfile;
        var raw = observation.InferredBehaviorProfileV2.Type;
        _observations++;
        Increment(_stableCounts, stable.StableProfile);
        Increment(_lifecycleCounts, stable.MaturityState);
        if (raw != TagBehaviorType.Unknown && stable.StableProfile == TagBehaviorType.Unknown)
            _prematureAvoided++;

        var key = observation.FastWindowIndex + "\u001f" + observation.Tag;
        if (!_timelines.TryGetValue(key, out var timeline))
        {
            timeline = new Timeline(observation.Timestamp);
            _timelines[key] = timeline;
        }

        if (timeline.LastRaw != TagBehaviorType.Unknown &&
            raw != TagBehaviorType.Unknown &&
            timeline.LastRaw != raw)
            _rawChanges++;

        if (timeline.LastStable != TagBehaviorType.Unknown &&
            stable.StableProfile != TagBehaviorType.Unknown &&
            timeline.LastStable != stable.StableProfile)
            _stableChanges++;

        if (!timeline.MaturedAt.HasValue &&
            stable.StableProfile != TagBehaviorType.Unknown &&
            stable.MaturityState == BehaviorProfileMaturityLifecycle.Stable)
        {
            timeline.MaturedAt = observation.Timestamp;
            timeline.FirstMatureProfile = stable.StableProfile;
        }

        if (timeline.MaturedAt.HasValue)
        {
            _postMaturityObservations++;
            if (stable.StableProfile == timeline.FirstMatureProfile)
                _postMaturityStableObservations++;
        }

        timeline.LastRaw = raw;
        timeline.LastStable = stable.StableProfile;
    }

    public BehaviorProfileMaturitySummary Build()
    {
        var matured = _timelines.Values.Where(value => value.MaturedAt.HasValue).ToArray();
        return new BehaviorProfileMaturitySummary
        {
            Parameters = _options,
            ObservationCount = _observations,
            StableDistribution = _stableCounts.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            MaturityStateDistribution = _lifecycleCounts.OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            MaturedWindowCount = matured.Length,
            AverageMinutesToMaturity = matured.Length == 0
                ? 0
                : matured.Average(value =>
                    (value.MaturedAt!.Value - value.StartedAt).TotalMinutes),
            PrematureClassificationsAvoided = _prematureAvoided,
            RawProfileChanges = _rawChanges,
            StableProfileChanges = _stableChanges,
            FalseRawFlipsAvoided = Math.Max(0, _rawChanges - _stableChanges),
            UnknownFormationObservations = _stableCounts.GetValueOrDefault(TagBehaviorType.Unknown),
            UnknownFormationRatio = Ratio(
                _stableCounts.GetValueOrDefault(TagBehaviorType.Unknown),
                _observations),
            StabilityAfterMaturityRatio = Ratio(
                _postMaturityStableObservations,
                _postMaturityObservations)
        };
    }

    private static void Increment<TKey>(Dictionary<TKey, long> values, TKey key)
        where TKey : notnull => values[key] = values.GetValueOrDefault(key) + 1;
    private static double Ratio(long numerator, long denominator) =>
        denominator > 0 ? numerator / (double)denominator : 0;

    private sealed class Timeline(DateTime startedAt)
    {
        public DateTime StartedAt { get; } = startedAt;
        public DateTime? MaturedAt { get; set; }
        public TagBehaviorType FirstMatureProfile { get; set; }
        public TagBehaviorType LastRaw { get; set; }
        public TagBehaviorType LastStable { get; set; }
    }
}
