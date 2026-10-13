

namespace IAApi.Benchmark.BehaviorProfiles.Diagnostics;

/// <summary>Compacta leituras Shadow consecutivas; nao participa de decisoes.</summary>
public sealed class BehaviorProfileClosureEpisodeCollector
{
    private readonly Dictionary<string, MutableEpisode> _open =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BehaviorProfileClosureEpisode> _completed = [];

    public void ResetWindow()
    {
        Flush();
        _open.Clear();
    }

    public void Add(BenchmarkBehaviorProfileObservation o)
    {
        var key = o.Tag;
        var signature = Signature(o);
        if (_open.TryGetValue(key, out var current) &&
            current.FastWindow == o.FastWindowIndex &&
            current.EndMinute + 1 == o.MinuteIndex &&
            current.Signature == signature)
        {
            current.EndMinute = o.MinuteIndex;
            current.EndEvidence = Evidence(o);
            return;
        }
        if (current is not null) _completed.Add(current.Build());
        _open[key] = MutableEpisode.Create(o, signature, Evidence(o));
    }

    public object Build()
    {
        Flush();
        _open.Clear();
        return new
        {
            generatedUtc = DateTime.UtcNow,
            episodeCount = _completed.Count,
            episodes = _completed.OrderBy(x => x.Tag)
                .ThenBy(x => x.FastWindow).ThenBy(x => x.StartMinute).ToArray()
        };
    }

    private void Flush()
    {
        foreach (var episode in _open.Values) _completed.Add(episode.Build());
    }

    private static string Signature(BenchmarkBehaviorProfileObservation o) => string.Join('|',
        o.DeclaredTagType.Kind, o.DeclaredTagType.Status,
        o.InferredBehaviorProfileV2.Type, o.StableBehaviorProfile.StableProfile,
        string.Join(',', o.StructuralAmbiguityResolution.ValidClasses),
        o.StructuralAmbiguityResolution.PreferredClass,
        o.StructuralAmbiguityResolution.ResolutionStatus,
        o.StructuralAmbiguityResolution.DeclaredTypeContext.Relation,
        o.DecisionReadiness.Status,
        o.StructuralAmbiguityResolution.ObservedStructure,
        o.DecisionReadiness.Reason);

    private static object Evidence(BenchmarkBehaviorProfileObservation o)
    {
        var declaredAssessment = o.StructuralEvidence.Classes
            .SingleOrDefault(x => x.Class == o.DeclaredTagType.Kind);
        return new
        {
            features = o.InferredBehaviorProfileV2.Features,
            rawWinner = o.InferredBehaviorProfileV2.Winner,
            rawWinnerScore = o.InferredBehaviorProfileV2.WinnerScore,
            margin = o.InferredBehaviorProfileV2.Margin,
            declaredClassAssessment = declaredAssessment,
            discriminatingEvidence = o.StructuralAmbiguityResolution.DiscriminatingEvidence,
            remainingAmbiguities = o.StructuralAmbiguityResolution.RemainingAmbiguities
        };
    }

    private sealed class MutableEpisode
    {
        public string Signature { get; init; } = "";
        public string Tag { get; init; } = "";
        public int FastWindow { get; init; }
        public int StartMinute { get; init; }
        public int EndMinute { get; set; }
        public BenchmarkBehaviorProfileObservation Start { get; init; } = new();
        public object? StartEvidence { get; init; }
        public object? EndEvidence { get; set; }

        public static MutableEpisode Create(BenchmarkBehaviorProfileObservation o,
            string signature, object evidence) => new()
        {
            Signature = signature, Tag = o.Tag, FastWindow = o.FastWindowIndex,
            StartMinute = o.MinuteIndex, EndMinute = o.MinuteIndex, Start = o,
            StartEvidence = evidence, EndEvidence = evidence
        };

        public BehaviorProfileClosureEpisode Build() => new()
        {
            Tag = Tag, FastWindow = FastWindow, StartMinute = StartMinute,
            EndMinute = EndMinute, Duration = EndMinute - StartMinute + 1,
            DeclaredType = Start.DeclaredTagType.Kind.ToString(),
            DeclaredStatus = Start.DeclaredTagType.Status.ToString(),
            DeclaredRawValue = Start.DeclaredTagType.RawValue ?? "",
            DeclaredSource = Start.DeclaredTagType.Source,
            RawProfile = Start.InferredBehaviorProfileV2.Type.ToString(),
            StableProfile = Start.StableBehaviorProfile.StableProfile.ToString(),
            ValidClasses = Start.StructuralAmbiguityResolution.ValidClasses.Select(x => x.ToString()).ToArray(),
            PreferredClass = Start.StructuralAmbiguityResolution.PreferredClass.ToString(),
            ResolutionStatus = Start.StructuralAmbiguityResolution.ResolutionStatus.ToString(),
            DeclaredRelation = Start.StructuralAmbiguityResolution.DeclaredTypeContext.Relation.ToString(),
            Readiness = Start.DecisionReadiness.Status.ToString(),
            ObservedStructure = Start.StructuralAmbiguityResolution.ObservedStructure.ToString(),
            Reason = Start.DecisionReadiness.Reason,
            StartEvidence = StartEvidence, EndEvidence = EndEvidence
        };
    }
}
