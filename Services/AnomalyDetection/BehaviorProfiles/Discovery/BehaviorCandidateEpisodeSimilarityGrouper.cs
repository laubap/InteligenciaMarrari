namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Grupo provisório de episódios mutuamente semelhantes.
///
/// Ainda não representa um comportamento persistente BEH-xxx.
/// Um grupo pode inclusive conter apenas uma ocorrência.
/// </summary>
public sealed record BehaviorCandidateEpisodeSimilarityGroup
{
    public int GroupIndex { get; init; }

    public IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> Members { get; init; } =
        Array.Empty<BehaviorCandidateEpisodeCharacteristics>();

    public int OccurrenceCount =>
        Members.Count;
}

/// <summary>
/// Resultado da formação inicial de grupos de episódios semelhantes.
/// </summary>
public sealed record BehaviorCandidateEpisodeGroupingResult
{
    public IReadOnlyList<BehaviorCandidateEpisodeSimilarityGroup> Groups { get; init; } =
        Array.Empty<BehaviorCandidateEpisodeSimilarityGroup>();

    public IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> ExcludedEpisodes { get; init; } =
        Array.Empty<BehaviorCandidateEpisodeCharacteristics>();
}

/// <summary>
/// Agrupa episódios candidatos usando compatibilidade completa.
///
/// Um episódio somente entra em um grupo existente quando é semelhante
/// a TODOS os membros já presentes no grupo.
///
/// Isso evita o encadeamento transitivo indevido:
///
/// A ≈ B
/// B ≈ C
/// A != C
///
/// Nesse caso, A/B podem formar um grupo, mas C não é absorvido
/// automaticamente apenas porque se parece com B.
///
/// Este componente:
/// - não cria BEH-xxx;
/// - não classifica normal/anormal;
/// - não persiste dados;
/// - não exige recorrência mínima.
/// </summary>
public sealed class BehaviorCandidateEpisodeSimilarityGrouper
{
    private readonly BehaviorCandidateEpisodeSimilarityComparer
        _comparer;

    public BehaviorCandidateEpisodeSimilarityGrouper(
        BehaviorCandidateEpisodeSimilarityComparer comparer)
    {
        _comparer =
            comparer ??
            throw new ArgumentNullException(
                nameof(comparer));
    }

    public BehaviorCandidateEpisodeGroupingResult Group(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> episodes)
    {
        ArgumentNullException.ThrowIfNull(
            episodes);

        var orderedEpisodes =
            episodes
                .Where(episode =>
                    episode is not null)
                .OrderBy(episode =>
                    episode.SourceSegmentIndex)
                .ThenBy(episode =>
                    episode.EpisodeIndex)
                .ThenBy(episode =>
                    episode.Inicio)
                .ToArray();

        var groups =
            new List<List<BehaviorCandidateEpisodeCharacteristics>>();

        var excluded =
            new List<BehaviorCandidateEpisodeCharacteristics>();

        foreach (var episode in orderedEpisodes)
        {
            if (!IsEligible(
                    episode))
            {
                excluded.Add(
                    episode);

                continue;
            }

            var compatibleGroup =
                FindCompatibleGroup(
                    groups,
                    episode);

            if (compatibleGroup is null)
            {
                groups.Add(
                    new List<BehaviorCandidateEpisodeCharacteristics>
                    {
                        episode
                    });

                continue;
            }

            compatibleGroup.Add(
                episode);
        }

        var resultGroups =
            groups
                .Select(
                    (members, index) =>
                        new BehaviorCandidateEpisodeSimilarityGroup
                        {
                            GroupIndex =
                                index,

                            Members =
                                members.ToArray()
                        })
                .ToArray();

        return new BehaviorCandidateEpisodeGroupingResult
        {
            Groups =
                resultGroups,

            ExcludedEpisodes =
                excluded.ToArray()
        };
    }

    private List<BehaviorCandidateEpisodeCharacteristics>?
        FindCompatibleGroup(
            IReadOnlyList<List<BehaviorCandidateEpisodeCharacteristics>> groups,
            BehaviorCandidateEpisodeCharacteristics candidate)
    {
        foreach (var group in groups)
        {
            var compatibleWithAll =
                group.All(
                    existing =>
                    {
                        var comparison =
                            _comparer.Compare(
                                existing,
                                candidate);

                        return
                            comparison.CanCompare &&
                            comparison.IsSimilar;
                    });

            if (compatibleWithAll)
            {
                return group;
            }
        }

        return null;
    }

    private static bool IsEligible(
        BehaviorCandidateEpisodeCharacteristics episode)
    {
        return
            episode.IsCharacterizable &&
            episode.ProfileFeatures is not null;
    }
}
