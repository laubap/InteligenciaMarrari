using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles;

/// <summary>
/// Resultado detalhado do pipeline de descoberta dentro de um
/// segmento temporal contínuo.
/// </summary>
public sealed record BehaviorDiscoverySegmentWorkflowResult
{
    public BehaviorHistorySegment Segment { get; init; } =
        new();

    public IReadOnlyList<BehaviorChangePointCandidate> ChangePoints { get; init; } =
        Array.Empty<BehaviorChangePointCandidate>();

    public IReadOnlyList<BehaviorCandidateEpisode> Episodes { get; init; } =
        Array.Empty<BehaviorCandidateEpisode>();

    public IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> Characteristics { get; init; } =
        Array.Empty<BehaviorCandidateEpisodeCharacteristics>();
}

/// <summary>
/// Resultado completo da descoberta e consolidação de memória
/// comportamental para uma tag em um período explícito.
/// </summary>
public sealed record BehaviorDiscoveryWorkflowResult
{
    public string ClienteId { get; init; } = "";

    public string TagName { get; init; } = "";

    public DateTime PeriodoInicio { get; init; }

    public DateTime PeriodoFim { get; init; }

    public BehaviorHistoryCollectionResult Collection { get; init; } =
        new();

    public BehaviorHistorySegmentationResult Segmentation { get; init; } =
        new();

    public IReadOnlyList<BehaviorDiscoverySegmentWorkflowResult> Segments { get; init; } =
        Array.Empty<BehaviorDiscoverySegmentWorkflowResult>();

    public BehaviorCandidateEpisodeGroupingResult Grouping { get; init; } =
        new();

    public IReadOnlyList<BehaviorDiscoveryCandidate> Candidates { get; init; } =
        Array.Empty<BehaviorDiscoveryCandidate>();

    public BehaviorMemoryConsolidationResult Consolidation { get; init; } =
        new();

    public int TotalChangePoints =>
        Segments.Sum(x =>
            x.ChangePoints.Count);

    public int TotalEpisodes =>
        Segments.Sum(x =>
            x.Episodes.Count);

    public int TotalCharacterizableEpisodes =>
        Segments.Sum(x =>
            x.Characteristics.Count(c =>
                c.IsCharacterizable));
}

/// <summary>
/// Ponto único de entrada do pipeline produtivo da memória comportamental.
///
/// Ordem:
/// histórico longo
/// -> sanitização estrutural
/// -> segmentação por gaps
/// -> change points
/// -> episódios
/// -> caracterização estatística/estrutural
/// -> agrupamento por similaridade
/// -> candidatos comportamentais
/// -> lifecycle/reconciliação/identidade/persistência.
///
/// O serviço não participa da decisão de anomalia em tempo real.
/// Ele coordena exclusivamente descoberta e consolidação histórica.
/// </summary>
public sealed class BehaviorDiscoveryWorkflowService
{
    private readonly BehaviorHistoryCollectionService _collectionService;

    private readonly BehaviorHistorySegmentationService _segmentationService;

    private readonly BehaviorChangePointDetector _changePointDetector;

    private readonly BehaviorCandidateEpisodeBuilder _episodeBuilder;

    private readonly BehaviorCandidateEpisodeCharacterizer _characterizer;

    private readonly BehaviorCandidateEpisodeSimilarityGrouper _grouper;

    private readonly BehaviorDiscoveryCandidateFactory _candidateFactory;

    private readonly BehaviorMemoryConsolidationService _consolidationService;

    public BehaviorDiscoveryWorkflowService(
        BehaviorHistoryCollectionService collectionService,
        BehaviorHistorySegmentationService segmentationService,
        BehaviorChangePointDetector changePointDetector,
        BehaviorCandidateEpisodeBuilder episodeBuilder,
        BehaviorCandidateEpisodeCharacterizer characterizer,
        BehaviorCandidateEpisodeSimilarityGrouper grouper,
        BehaviorDiscoveryCandidateFactory candidateFactory,
        BehaviorMemoryConsolidationService consolidationService)
    {
        _collectionService =
            collectionService ??
            throw new ArgumentNullException(
                nameof(collectionService));

        _segmentationService =
            segmentationService ??
            throw new ArgumentNullException(
                nameof(segmentationService));

        _changePointDetector =
            changePointDetector ??
            throw new ArgumentNullException(
                nameof(changePointDetector));

        _episodeBuilder =
            episodeBuilder ??
            throw new ArgumentNullException(
                nameof(episodeBuilder));

        _characterizer =
            characterizer ??
            throw new ArgumentNullException(
                nameof(characterizer));

        _grouper =
            grouper ??
            throw new ArgumentNullException(
                nameof(grouper));

        _candidateFactory =
            candidateFactory ??
            throw new ArgumentNullException(
                nameof(candidateFactory));

        _consolidationService =
            consolidationService ??
            throw new ArgumentNullException(
                nameof(consolidationService));
    }

    public async Task<BehaviorDiscoveryWorkflowResult> ExecuteAsync(
        string clienteId,
        string tagName,
        DateTime inicio,
        DateTime fim,
        CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var collection =
            _collectionService.Collect(
                clienteId,
                tagName,
                inicio,
                fim);

        var segmentation =
            _segmentationService.Segment(
                collection);

        var segmentResults =
            new List<BehaviorDiscoverySegmentWorkflowResult>(
                segmentation.Segments.Count);

        var allCharacteristics =
            new List<BehaviorCandidateEpisodeCharacteristics>();

        foreach (var segment in segmentation.Segments)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var changePoints =
                _changePointDetector.Detect(
                    segment);

            var episodes =
                _episodeBuilder.Build(
                    segment,
                    changePoints);

            var characteristics =
                episodes
                    .Select(episode =>
                        _characterizer.Characterize(
                            episode))
                    .ToArray();

            allCharacteristics.AddRange(
                characteristics);

            segmentResults.Add(
                new BehaviorDiscoverySegmentWorkflowResult
                {
                    Segment =
                        segment,

                    ChangePoints =
                        changePoints,

                    Episodes =
                        episodes,

                    Characteristics =
                        characteristics
                });
        }

        var grouping =
            _grouper.Group(
                allCharacteristics);

        var candidates =
            _candidateFactory.Create(
                grouping);

        var consolidation =
            await _consolidationService
                .ConsolidateAsync(
                    clienteId,
                    tagName,
                    candidates,
                    cancellationToken);

        return new BehaviorDiscoveryWorkflowResult
        {
            ClienteId =
                clienteId,

            TagName =
                tagName,

            PeriodoInicio =
                inicio,

            PeriodoFim =
                fim,

            Collection =
                collection,

            Segmentation =
                segmentation,

            Segments =
                segmentResults,

            Grouping =
                grouping,

            Candidates =
                candidates,

            Consolidation =
                consolidation
        };
    }
}
