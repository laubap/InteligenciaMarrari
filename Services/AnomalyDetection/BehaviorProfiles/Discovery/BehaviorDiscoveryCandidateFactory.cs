using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed record BehaviorCandidateFormationOptions
{
    public int MinimumOccurrencesForRecurrence { get; init; } = 2;
}

/// <summary>
/// Assinatura representativa robusta de um grupo de episódios semelhantes.
///
/// Cada campo é a mediana do respectivo descritor entre as ocorrências
/// caracterizadas do grupo.
/// </summary>
public sealed record BehaviorCandidateRepresentativeProfile
{
    public double Media { get; init; }

    public double Mediana { get; init; }

    public double DesvioPadrao { get; init; }

    public double IntervaloInterquartil { get; init; }

    public double DeadbandRecomendado { get; init; }

    public double RobustScale { get; init; }

    public double TypicalAbsoluteStep { get; init; }

    public double ChangeRatio { get; init; }

    public double ReversalRatio { get; init; }
}

/// <summary>
/// Candidato comportamental produzido a partir de um grupo de episódios
/// mutuamente semelhantes.
///
/// Ainda não representa um comportamento persistido BEH-xxx.
/// Recorrência também não implica normalidade.
/// </summary>
public sealed record BehaviorDiscoveryCandidate
{
    public int GroupIndex { get; init; }

    public int OccurrenceCount { get; init; }

    public bool IsRecurrent { get; init; }

    public DateTime FirstOccurrenceStart { get; init; }

    public DateTime LastOccurrenceEnd { get; init; }

    public BehaviorCandidateRepresentativeProfile RepresentativeProfile { get; init; } =
        new();

    public IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> Occurrences { get; init; } =
        Array.Empty<BehaviorCandidateEpisodeCharacteristics>();

    public string Reason { get; init; } = "";
}

/// <summary>
/// Transforma grupos de episódios semelhantes em candidatos comportamentais.
///
/// Responsabilidades:
/// - preservar todas as ocorrências do grupo;
/// - identificar recorrência mínima;
/// - calcular uma assinatura representativa robusta.
///
/// Não:
/// - cria BEH-xxx;
/// - define lifecycle Known/Deprecated;
/// - classifica Normal/KnownAbnormal;
/// - persiste dados.
/// </summary>
public sealed class BehaviorDiscoveryCandidateFactory
{
    private readonly BehaviorCandidateFormationOptions _options;

    public BehaviorDiscoveryCandidateFactory(
        BehaviorCandidateFormationOptions? options = null)
    {
        _options =
            options ??
            new BehaviorCandidateFormationOptions();

        ValidateOptions(
            _options);
    }

    public IReadOnlyList<BehaviorDiscoveryCandidate> Create(
        BehaviorCandidateEpisodeGroupingResult grouping)
    {
        ArgumentNullException.ThrowIfNull(
            grouping);

        return grouping.Groups
            .OrderBy(group =>
                group.GroupIndex)
            .Select(CreateCandidate)
            .ToArray();
    }

    private BehaviorDiscoveryCandidate CreateCandidate(
        BehaviorCandidateEpisodeSimilarityGroup group)
    {
        var occurrences =
            group.Members
                .Where(occurrence =>
                    occurrence.IsCharacterizable &&
                    occurrence.ProfileFeatures is not null)
                .OrderBy(occurrence =>
                    occurrence.Inicio)
                .ThenBy(occurrence =>
                    occurrence.SourceSegmentIndex)
                .ThenBy(occurrence =>
                    occurrence.EpisodeIndex)
                .ToArray();

        if (occurrences.Length == 0)
        {
            return new BehaviorDiscoveryCandidate
            {
                GroupIndex =
                    group.GroupIndex,

                OccurrenceCount =
                    0,

                IsRecurrent =
                    false,

                Reason =
                    "Grupo não possui ocorrências caracterizáveis elegíveis."
            };
        }

        var occurrenceCount =
            occurrences.Length;

        var isRecurrent =
            occurrenceCount >=
            _options.MinimumOccurrencesForRecurrence;

        return new BehaviorDiscoveryCandidate
        {
            GroupIndex =
                group.GroupIndex,

            OccurrenceCount =
                occurrenceCount,

            IsRecurrent =
                isRecurrent,

            FirstOccurrenceStart =
                occurrences.Min(occurrence =>
                    occurrence.Inicio),

            LastOccurrenceEnd =
                occurrences.Max(occurrence =>
                    occurrence.Fim),

            RepresentativeProfile =
                BuildRepresentativeProfile(
                    occurrences),

            Occurrences =
                occurrences,

            Reason =
                isRecurrent
                    ? $"Grupo apresentou {occurrenceCount} ocorrências semelhantes e atende ao mínimo de recorrência."
                    : $"Grupo apresentou {occurrenceCount} ocorrência; mínimo para recorrência: {_options.MinimumOccurrencesForRecurrence}."
        };
    }

    private static BehaviorCandidateRepresentativeProfile
        BuildRepresentativeProfile(
            IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> occurrences)
    {
        return new BehaviorCandidateRepresentativeProfile
        {
            Media =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.Media)),

            Mediana =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.Mediana)),

            DesvioPadrao =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.DesvioPadrao)),

            IntervaloInterquartil =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.IntervaloInterquartil)),

            DeadbandRecomendado =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.DeadbandRecomendado)),

            RobustScale =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.ProfileFeatures!.RobustScale)),

            TypicalAbsoluteStep =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.ProfileFeatures!.TypicalAbsoluteStep)),

            ChangeRatio =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.ProfileFeatures!.ChangeRatio)),

            ReversalRatio =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.ProfileFeatures!.ReversalRatio))
        };
    }

    private static void ValidateOptions(
        BehaviorCandidateFormationOptions options)
    {
        if (options.MinimumOccurrencesForRecurrence < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MinimumOccurrencesForRecurrence deve ser maior ou igual a 2.");
        }
    }
}
