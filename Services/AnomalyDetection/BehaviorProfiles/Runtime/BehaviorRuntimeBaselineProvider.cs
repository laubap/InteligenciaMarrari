using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Runtime;

/// <summary>
/// Reconhece, em modo somente leitura, qual comportamento Known melhor
/// representa o episódio atual da tag.
///
/// Este componente NÃO:
/// - consulta novamente o Bridge;
/// - persiste Candidate/BEH;
/// - promove memória;
/// - altera TagPerfilIa;
/// - decide anomalia.
///
/// Ele recebe o histórico que o runtime já possui, mantém causalidade temporal,
/// identifica o episódio atual e reutiliza a reconciliação B3.25 existente.
/// </summary>
public sealed class BehaviorRuntimeBaselineProvider
{
    private readonly AppDbContext _context;

    private readonly BehaviorHistorySegmentationService
        _segmentationService;

    private readonly BehaviorChangePointDetector
        _changePointDetector;

    private readonly BehaviorCandidateEpisodeBuilder
        _episodeBuilder;

    private readonly BehaviorCandidateEpisodeCharacterizer
        _characterizer;

    private readonly BehaviorMemoryReconciliationService
        _reconciliationService;

    public BehaviorRuntimeBaselineProvider(
        AppDbContext context,
        BehaviorHistorySegmentationService segmentationService,
        BehaviorChangePointDetector changePointDetector,
        BehaviorCandidateEpisodeBuilder episodeBuilder,
        BehaviorCandidateEpisodeCharacterizer characterizer,
        BehaviorMemoryReconciliationService reconciliationService)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));

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

        _reconciliationService =
            reconciliationService ??
            throw new ArgumentNullException(
                nameof(reconciliationService));
    }

    public async Task<BehaviorRuntimeBaselineResolution>
        ResolveAsync(
            string clienteId,
            string tagName,
            IReadOnlyList<LeituraBruta> runtimeHistory,
            DateTime currentTimestamp,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                clienteId))
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "Cliente não informado para reconhecimento comportamental.");
        }

        if (string.IsNullOrWhiteSpace(
                tagName))
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "Tag não informada para reconhecimento comportamental.");
        }

        ArgumentNullException.ThrowIfNull(
            runtimeHistory);

        if (currentTimestamp == default)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "Timestamp atual inválido para reconhecimento comportamental.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        /*
         * Causalidade obrigatória:
         * nenhuma leitura posterior ao instante analisado pode participar
         * do reconhecimento do comportamento atual.
         */
        var causalHistory =
            runtimeHistory
                .Where(reading =>
                    reading is not null &&
                    reading.DataHora != default &&
                    reading.DataHora <= currentTimestamp &&
                    float.IsFinite(
                        reading.Valor))
                .OrderBy(reading =>
                    reading.DataHora)
                .ToArray();

        if (causalHistory.Length <
            BehaviorCandidateEpisodeCharacterizer.MinimumSamples)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                $"Histórico causal insuficiente para reconhecimento comportamental: " +
                $"{causalHistory.Length} leituras válidas.");
        }

        var collection =
            BuildCollection(
                clienteId,
                tagName,
                causalHistory);

        if (collection.Historico.Count <
            BehaviorCandidateEpisodeCharacterizer.MinimumSamples)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                $"Histórico sanitizado insuficiente para reconhecimento comportamental: " +
                $"{collection.Historico.Count} leituras válidas.");
        }

        var segmentation =
            _segmentationService
                .Segment(
                    collection);

        var latestSegment =
            segmentation.Segments
                .OrderBy(segment =>
                    segment.Fim)
                .LastOrDefault();

        if (latestSegment is null)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "Nenhum segmento temporal válido foi encontrado.");
        }

        var changePoints =
            _changePointDetector
                .Detect(
                    latestSegment);

        var episodes =
            _episodeBuilder
                .Build(
                    latestSegment,
                    changePoints);

        var latestEpisode =
            episodes
                .OrderBy(episode =>
                    episode.Fim)
                .ThenBy(episode =>
                    episode.EpisodeIndex)
                .LastOrDefault();

        if (latestEpisode is null)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "Nenhum episódio atual foi construído a partir do histórico recente.");
        }

        var characteristics =
            _characterizer
                .Characterize(
                    latestEpisode);

        if (!characteristics.IsCharacterizable)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "O episódio atual não possui evidência estatística suficiente. " +
                characteristics.Reason);
        }

        /*
         * ReconcileAsync utiliza somente Occurrences para montar o snapshot
         * candidato e comparar com memórias Known.
         *
         * Recorrência NÃO é requisito para reconhecer um BEH já existente:
         * uma única ocorrência atual pode corresponder a uma identidade Known.
         */
        var candidate =
            new BehaviorDiscoveryCandidate
            {
                GroupIndex =
                    0,

                OccurrenceCount =
                    1,

                IsRecurrent =
                    false,

                FirstOccurrenceStart =
                    characteristics.Inicio,

                LastOccurrenceEnd =
                    characteristics.Fim,

                Occurrences =
                [
                    characteristics
                ],

                Reason =
                    "Candidato transitório de runtime criado somente para reconhecimento read-only."
            };

        var reconciliation =
            await _reconciliationService
                .ReconcileAsync(
                    clienteId,
                    tagName,
                    candidate,
                    cancellationToken);

        if (reconciliation.Outcome !=
                BehaviorMemoryReconciliationOutcome.ExistingBehavior ||
            !reconciliation.MatchedBehaviorId.HasValue ||
            string.IsNullOrWhiteSpace(
                reconciliation.MatchedBehaviorCode))
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                reconciliation.Reason);
        }

        var matchedBehaviorId =
            reconciliation.MatchedBehaviorId.Value;

        var comparison =
            reconciliation.Comparisons
                .Where(item =>
                    item.BehaviorId ==
                    matchedBehaviorId)
                .OrderByDescending(item =>
                    item.SnapshotVersion)
                .FirstOrDefault();

        if (comparison is null ||
            !comparison.IsCompatible)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "A reconciliação retornou um comportamento existente sem comparação compatível.");
        }

        /*
         * Carrega exatamente a versão usada na comparação.
         * Isso evita trocar silenciosamente de snapshot caso outra execução
         * persista uma versão nova entre a reconciliação e esta leitura.
         */
        var snapshot =
            await _context
                .TagsComportamentosSnapshotsIa
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.TagComportamentoIaId ==
                            matchedBehaviorId &&
                        item.Versao ==
                            comparison.SnapshotVersion,
                    cancellationToken);

        if (snapshot is null)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                $"O snapshot v{comparison.SnapshotVersion} utilizado no matching não foi encontrado.");
        }

        var behavior =
            await _context
                .TagsComportamentosIa
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.Id ==
                            matchedBehaviorId &&
                        item.ClienteId ==
                            clienteId &&
                        item.TagName ==
                            tagName &&
                        item.Status ==
                            TagComportamentoIa.StatusKnown,
                    cancellationToken);

        if (behavior is null)
        {
            return BehaviorRuntimeBaselineResolution.NotRecognized(
                "A memória reconhecida deixou de estar disponível como Known.");
        }

        var canApplyBaseline =
            string.Equals(
                behavior.Classificacao,
                TagComportamentoIa.ClassificacaoNormal,
                StringComparison.OrdinalIgnoreCase);

        var detectorHistory =
            latestEpisode.Historico
                .Select(reading =>
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            reading.DataHora,

                        Valor =
                            reading.Valor
                    })
                .ToArray();

        var reason =
            canApplyBaseline
                ? $"Comportamento {behavior.Codigo} reconhecido e classificado como Normal; baseline comportamental elegível."
                : $"Comportamento {behavior.Codigo} reconhecido, porém sua classificação semântica é " +
                  $"'{behavior.Classificacao}'. Known não implica Normal; baseline não autorizado.";

        return new BehaviorRuntimeBaselineResolution
        {
            Recognized =
                true,

            CanApplyBaseline =
                canApplyBaseline,

            BehaviorId =
                behavior.Id,

            BehaviorCode =
                behavior.Codigo,

            BehaviorClassification =
                behavior.Classificacao,

            SnapshotVersion =
                snapshot.Versao,

            CompatibilityConfidence =
                comparison.CompatibilityConfidence,

            Media =
                snapshot.Media,

            DesvioPadrao =
                snapshot.DesvioPadrao,

            DeadbandRecomendado =
                snapshot.DeadbandRecomendado,

            ConfiancaDeadband =
                snapshot.ConfiancaDeadband,

            History =
                detectorHistory,

            Reason =
                reason
        };
    }

    private static BehaviorHistoryCollectionResult
        BuildCollection(
            string clienteId,
            string tagName,
            IReadOnlyList<LeituraBruta> causalHistory)
    {
        var points =
            causalHistory
                .Select(reading =>
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            reading.DataHora,

                        Valor =
                            reading.Valor
                    })
                .ToArray();

        var sanitized =
            DetectionHistorySanitizer
                .Sanitize(
                    points);

        var history =
            sanitized.History
                .Select(point =>
                    new LeituraBruta
                    {
                        DataHora =
                            point.DataHora,

                        Valor =
                            Convert.ToSingle(
                                point.Valor)
                    })
                .ToArray();

        var start =
            history.Length > 0
                ? history[0].DataHora
                : causalHistory[0].DataHora;

        var end =
            history.Length > 0
                ? history[^1].DataHora
                : causalHistory[^1].DataHora;

        return new BehaviorHistoryCollectionResult
        {
            ClienteId =
                clienteId,

            TagName =
                tagName,

            PeriodoInicio =
                start,

            PeriodoFim =
                end,

            Historico =
                history,

            Qualidade =
                sanitized.Quality
        };
    }
}

public sealed record BehaviorRuntimeBaselineResolution
{
    public bool Recognized { get; init; }

    public bool CanApplyBaseline { get; init; }

    public int? BehaviorId { get; init; }

    public string? BehaviorCode { get; init; }

    public string? BehaviorClassification { get; init; }

    public int? SnapshotVersion { get; init; }

    public double? CompatibilityConfidence { get; init; }

    public double? Media { get; init; }

    public double? DesvioPadrao { get; init; }

    public double? DeadbandRecomendado { get; init; }

    public double? ConfiancaDeadband { get; init; }

    public IReadOnlyList<HistoricoDetectorPoint> History { get; init; } =
        Array.Empty<HistoricoDetectorPoint>();

    public string Reason { get; init; } = "";

    public static BehaviorRuntimeBaselineResolution
        NotRecognized(
            string reason)
    {
        return new BehaviorRuntimeBaselineResolution
        {
            Recognized =
                false,

            CanApplyBaseline =
                false,

            Reason =
                reason
        };
    }
}
