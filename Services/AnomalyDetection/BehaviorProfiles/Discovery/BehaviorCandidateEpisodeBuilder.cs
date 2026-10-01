using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Trecho de histórico delimitado por pontos candidatos de mudança.
///
/// Um episódio candidato ainda NÃO representa um comportamento
/// persistente BEH-xxx.
///
/// Ele é apenas uma ocorrência histórica que posteriormente poderá:
///
/// - ser caracterizada estatisticamente;
/// - ser comparada com outras ocorrências;
/// - ser agrupada com episódios semelhantes;
/// - contribuir para a formação de um comportamento persistente.
/// </summary>
public sealed record BehaviorCandidateEpisode
{
    /// <summary>
    /// Segmento temporal contínuo do qual este episódio foi extraído.
    /// </summary>
    public int SourceSegmentIndex { get; init; }

    /// <summary>
    /// Índice sequencial do episódio dentro do segmento.
    /// </summary>
    public int EpisodeIndex { get; init; }

    /// <summary>
    /// Índice da primeira leitura dentro do segmento original.
    /// </summary>
    public int StartReadingIndex { get; init; }

    /// <summary>
    /// Índice da última leitura dentro do segmento original.
    /// </summary>
    public int EndReadingIndex { get; init; }

    public DateTime Inicio { get; init; }

    public DateTime Fim { get; init; }

    public IReadOnlyList<LeituraBruta> Historico { get; init; } =
        Array.Empty<LeituraBruta>();

    /// <summary>
    /// Ponto de mudança que iniciou este episódio.
    ///
    /// O primeiro episódio de um segmento não possui um ponto
    /// de mudança anterior.
    /// </summary>
    public BehaviorChangePointCandidate? OpeningChangePoint { get; init; }

    public int QuantidadeLeituras =>
        Historico.Count;

    public TimeSpan Duracao =>
        Fim >= Inicio
            ? Fim - Inicio
            : TimeSpan.Zero;
}

/// <summary>
/// Constrói episódios candidatos a partir de um segmento temporal
/// e dos pontos de mudança encontrados dentro dele.
///
/// Este componente somente realiza os cortes.
///
/// Ele não:
/// - calcula estatísticas;
/// - classifica normal/anormal;
/// - agrupa episódios semelhantes;
/// - cria identificadores BEH-xxx;
/// - persiste dados.
/// </summary>
public sealed class BehaviorCandidateEpisodeBuilder
{
    public IReadOnlyList<BehaviorCandidateEpisode> Build(
        BehaviorHistorySegment segment,
        IReadOnlyList<BehaviorChangePointCandidate>? changePoints)
    {
        ArgumentNullException.ThrowIfNull(
            segment);

        var history =
            segment.Historico;

        if (history.Count == 0)
        {
            return Array.Empty<BehaviorCandidateEpisode>();
        }

        changePoints ??=
            Array.Empty<BehaviorChangePointCandidate>();

        /*
         * Consideramos somente cortes:
         *
         * - pertencentes ao segmento atual;
         * - depois da primeira leitura;
         * - antes do fim do histórico.
         *
         * ReadingIndex representa a primeira leitura do NOVO episódio.
         */
        var validChangePoints =
            changePoints
                .Where(candidate =>
                    candidate.SegmentIndex ==
                    segment.Index)
                .Where(candidate =>
                    candidate.ReadingIndex > 0 &&
                    candidate.ReadingIndex <
                    history.Count)
                .GroupBy(candidate =>
                    candidate.ReadingIndex)
                .Select(group =>
                    group
                        .OrderByDescending(candidate =>
                            candidate.RobustEffect)
                        .ThenByDescending(candidate =>
                            candidate.BoundaryJump)
                        .First())
                .OrderBy(candidate =>
                    candidate.ReadingIndex)
                .ToArray();

        if (validChangePoints.Length == 0)
        {
            return
            [
                CreateEpisode(
                    segment,
                    episodeIndex: 0,
                    startReadingIndex: 0,
                    endReadingIndex: history.Count - 1,
                    openingChangePoint: null)
            ];
        }

        var episodes =
            new List<BehaviorCandidateEpisode>(
                validChangePoints.Length + 1);

        var startReadingIndex =
            0;

        BehaviorChangePointCandidate? openingChangePoint =
            null;

        foreach (var changePoint in validChangePoints)
        {
            var endReadingIndex =
                changePoint.ReadingIndex - 1;

            /*
             * Proteção adicional.
             *
             * Não deve acontecer após a validação acima, mas evita
             * produzir episódio vazio caso a entrada futura venha
             * de outro componente.
             */
            if (endReadingIndex >=
                startReadingIndex)
            {
                episodes.Add(
                    CreateEpisode(
                        segment,
                        episodes.Count,
                        startReadingIndex,
                        endReadingIndex,
                        openingChangePoint));
            }

            /*
             * A leitura exatamente no ReadingIndex pertence ao
             * NOVO episódio.
             */
            startReadingIndex =
                changePoint.ReadingIndex;

            openingChangePoint =
                changePoint;
        }

        /*
         * Fecha o último episódio até o final do segmento.
         */
        if (startReadingIndex <
            history.Count)
        {
            episodes.Add(
                CreateEpisode(
                    segment,
                    episodes.Count,
                    startReadingIndex,
                    history.Count - 1,
                    openingChangePoint));
        }

        return episodes;
    }

    private static BehaviorCandidateEpisode CreateEpisode(
        BehaviorHistorySegment segment,
        int episodeIndex,
        int startReadingIndex,
        int endReadingIndex,
        BehaviorChangePointCandidate? openingChangePoint)
    {
        var history =
            segment.Historico;

        var count =
            endReadingIndex -
            startReadingIndex +
            1;

        var episodeHistory =
            history
                .Skip(
                    startReadingIndex)
                .Take(
                    count)
                .ToArray();

        return new BehaviorCandidateEpisode
        {
            SourceSegmentIndex =
                segment.Index,

            EpisodeIndex =
                episodeIndex,

            StartReadingIndex =
                startReadingIndex,

            EndReadingIndex =
                endReadingIndex,

            Inicio =
                episodeHistory[0]
                    .DataHora,

            Fim =
                episodeHistory[^1]
                    .DataHora,

            Historico =
                episodeHistory,

            OpeningChangePoint =
                openingChangePoint
        };
    }
}