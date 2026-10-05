using IAApi.Services.AnomalyDetection;
using IAApi.Services.AnomalyDetection.BehaviorProfiles;
namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Assinatura estatística de uma ocorrência comportamental candidata.
///
/// Esta estrutura descreve um episódio isolado.
/// Ela ainda não representa um comportamento persistente BEH-xxx.
/// </summary>
public sealed record BehaviorCandidateEpisodeCharacteristics
{
    public int SourceSegmentIndex { get; init; }

    public int EpisodeIndex { get; init; }

    public DateTime Inicio { get; init; }

    public DateTime Fim { get; init; }

    public double DuracaoMinutos { get; init; }

    public bool IsCharacterizable { get; init; }

    public string Reason { get; init; } = "";

    public int OriginalSampleCount { get; init; }

    public int UsedSampleCount { get; init; }

    public int RemovedOutliers { get; init; }

    public double Media { get; init; }

    public double Mediana { get; init; }

    public double Minimo { get; init; }

    public double Maximo { get; init; }

    public double DesvioPadrao { get; init; }

    public double Amplitude { get; init; }

    public double PrimeiroQuartil { get; init; }

    public double TerceiroQuartil { get; init; }

    public double IntervaloInterquartil { get; init; }

    public double VariacaoMedia { get; init; }

    public double PercentualZeros { get; init; }

    public double DeadbandRecomendado { get; init; }

    public double ConfiancaDeadband { get; init; }

    /// <summary>
    /// Características estruturais e dinâmicas já calculadas pelo
    /// BehaviorProfileFeatureExtractor existente.
    ///
    /// Elas serão usadas posteriormente pela etapa de similaridade
    /// entre episódios, sem duplicar lógica de extração.
    /// </summary>
    public BehaviorProfileFeatures? ProfileFeatures { get; init; }

    public IReadOnlyList<LeituraBruta> HistoricoLimpo { get; init; } =
        Array.Empty<LeituraBruta>();
}

/// <summary>
/// Calcula uma assinatura estatística para cada episódio candidato.
///
/// A limpeza por IQR acontece somente dentro do episódio,
/// depois que mudanças de regime já foram separadas.
/// </summary>
public sealed class BehaviorCandidateEpisodeCharacterizer
{
    public const int MinimumSamples = 10;

    private readonly TagProfileStatisticsCalculator
        _statisticsCalculator;

    private readonly BehaviorProfileFeatureExtractor
        _featureExtractor = new();

    public BehaviorCandidateEpisodeCharacterizer(
        TagProfileStatisticsCalculator statisticsCalculator)
    {
        _statisticsCalculator =
            statisticsCalculator ??
            throw new ArgumentNullException(
                nameof(statisticsCalculator));
    }

    public BehaviorCandidateEpisodeCharacteristics Characterize(
        BehaviorCandidateEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(
            episode);

        if (episode.Historico.Count <
            MinimumSamples)
        {
            return CreateInsufficient(
                episode,
                $"Episódio possui apenas " +
                $"{episode.Historico.Count} leituras; " +
                $"mínimo necessário: {MinimumSamples}.");
        }

        TagProfileStatisticsResult statistics;

        try
        {
            statistics =
                _statisticsCalculator.Calculate(
                    episode.Historico);
        }
        catch (InvalidOperationException exception)
        {
            /*
             * Exemplo:
             *
             * o episódio tinha >=10 leituras,
             * mas após limpeza estatística restaram
             * menos amostras que o necessário.
             *
             * O episódio continua existindo historicamente,
             * apenas não possui evidência estatística suficiente
             * para participar do agrupamento.
             */
            return CreateInsufficient(
                episode,
                exception.Message);
        }

        var values =
            statistics.HistoricoLimpo
                .Select(reading =>
                    Convert.ToDouble(
                        reading.Valor))
                .Where(double.IsFinite)
                .OrderBy(value =>
                    value)
                .ToArray();

        if (values.Length <
            MinimumSamples)
        {
            return CreateInsufficient(
                episode,
                "Histórico limpo insuficiente para " +
                "caracterização comportamental.");
        }

        var median =
            DetectorMath.Median(
                values);

        var q1 =
            Percentile(
                values,
                0.25);

        var q3 =
            Percentile(
                values,
                0.75);

        /*
         * Reutiliza o extractor comportamental já existente.
         *
         * A extração acontece sobre o mesmo histórico limpo usado
         * pelas estatísticas do episódio. Dessa forma, um pico isolado
         * removido pelo IQR não passa a definir a dinâmica do comportamento.
         *
         * As features temporais de gap continuam disponíveis no contrato,
         * mas não serão usadas como critério de similaridade nesta etapa,
         * pois a segmentação temporal B3.25 já aconteceu antes.
         */
        var profileHistory =
            statistics.HistoricoLimpo
                .Select(reading =>
                    new HistoricoDetectorPoint
                    {
                        DataHora =
                            reading.DataHora,

                        Valor =
                            Convert.ToDouble(
                                reading.Valor)
                    })
                .ToArray();

        var profileFeatures =
            _featureExtractor.Extract(
                profileHistory);

        return new BehaviorCandidateEpisodeCharacteristics
        {
            SourceSegmentIndex =
                episode.SourceSegmentIndex,

            EpisodeIndex =
                episode.EpisodeIndex,

            Inicio =
                episode.Inicio,

            Fim =
                episode.Fim,

            DuracaoMinutos =
                Math.Max(
                    0,
                    episode.Duracao.TotalMinutes),

            IsCharacterizable =
                true,

            Reason =
                "Episódio possui evidência estatística suficiente.",

            OriginalSampleCount =
                statistics.TotalRegistrosHistorico,

            UsedSampleCount =
                statistics.TotalRegistrosUsados,

            RemovedOutliers =
                statistics.TotalOutliersRemovidos,

            Media =
                statistics.Media,

            Mediana =
                median,

            Minimo =
                statistics.Minimo,

            Maximo =
                statistics.Maximo,

            DesvioPadrao =
                statistics.DesvioPadrao,

            Amplitude =
                statistics.Amplitude,

            PrimeiroQuartil =
                q1,

            TerceiroQuartil =
                q3,

            IntervaloInterquartil =
                q3 - q1,

            VariacaoMedia =
                statistics.VariacaoMedia,

            PercentualZeros =
                statistics.PercentualZeros,

            DeadbandRecomendado =
                statistics.DeadbandRecomendado,

            ConfiancaDeadband =
                statistics.ConfiancaDeadband,

            ProfileFeatures =
                profileFeatures,

            HistoricoLimpo =
                statistics.HistoricoLimpo
        };
    }

    private static BehaviorCandidateEpisodeCharacteristics
        CreateInsufficient(
            BehaviorCandidateEpisode episode,
            string reason)
    {
        return new BehaviorCandidateEpisodeCharacteristics
        {
            SourceSegmentIndex =
                episode.SourceSegmentIndex,

            EpisodeIndex =
                episode.EpisodeIndex,

            Inicio =
                episode.Inicio,

            Fim =
                episode.Fim,

            DuracaoMinutos =
                Math.Max(
                    0,
                    episode.Duracao.TotalMinutes),

            IsCharacterizable =
                false,

            Reason =
                reason,

            OriginalSampleCount =
                episode.Historico.Count
        };
    }

    private static double Percentile(
        IReadOnlyList<double> orderedValues,
        double percentile)
    {
        if (orderedValues.Count == 0)
        {
            return 0;
        }

        if (orderedValues.Count == 1)
        {
            return orderedValues[0];
        }

        var position =
            percentile *
            (orderedValues.Count - 1);

        var lowerIndex =
            (int)Math.Floor(
                position);

        var upperIndex =
            (int)Math.Ceiling(
                position);

        if (lowerIndex ==
            upperIndex)
        {
            return orderedValues[
                lowerIndex];
        }

        var fraction =
            position -
            lowerIndex;

        return
            orderedValues[lowerIndex] +
            (
                orderedValues[upperIndex] -
                orderedValues[lowerIndex]
            ) *
            fraction;
    }
}