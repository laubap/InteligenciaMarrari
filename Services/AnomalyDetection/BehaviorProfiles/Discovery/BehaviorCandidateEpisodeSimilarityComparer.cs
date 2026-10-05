using IAApi.Services.AnomalyDetection.BehaviorProfiles;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Opções conservadoras para comparar duas ocorrências comportamentais.
///
/// A regra reutiliza o princípio de repetibilidade já usado no DualBaseline:
/// - centro compatível dentro de uma tolerância robusta;
/// - escala compatível por razão máxima;
/// - distribuição compatível dentro da mesma tolerância.
///
/// A dinâmica usa inicialmente apenas o passo absoluto típico.
/// Outras features permanecem disponíveis como diagnóstico e podem ser
/// promovidas a critérios somente após validação empírica.
/// </summary>
public sealed record BehaviorCandidateEpisodeSimilarityOptions
{
    public double MaximumScaleRatio { get; init; } = 2.0;

    public double MaximumTypicalStepRatio { get; init; } = 2.0;
}

/// <summary>
/// Resultado explicável da comparação entre dois episódios candidatos.
///
/// Similaridade aqui significa somente compatibilidade estatística/estrutural.
/// Não cria BEH-xxx, não classifica normalidade e não promove memória.
/// </summary>
public sealed record BehaviorCandidateEpisodeSimilarityResult
{
    public bool CanCompare { get; init; }

    public bool IsSimilar { get; init; }

    public bool CenterStable { get; init; }

    public bool ScaleStable { get; init; }

    public bool DistributionStable { get; init; }

    public bool DynamicsStable { get; init; }

    public double ComparisonFloor { get; init; }

    public double MeanDelta { get; init; }

    public double MedianDelta { get; init; }

    public double FirstQuartileDelta { get; init; }

    public double ThirdQuartileDelta { get; init; }

    public double InterquartileRangeDelta { get; init; }

    public double StandardDeviationRatio { get; init; }

    public double RobustScaleRatio { get; init; }

    public double TypicalAbsoluteStepRatio { get; init; }

    public double ChangeRatioDelta { get; init; }

    public double ReversalRatioDelta { get; init; }

    public string Reason { get; init; } = "";
}

/// <summary>
/// Compara duas assinaturas de episódios já caracterizados.
///
/// Este componente é deliberadamente puro:
/// - não acessa banco;
/// - não agrupa episódios;
/// - não cria identificadores BEH;
/// - não decide normal/anormal.
///
/// O objetivo é responder somente:
/// "há evidência suficiente de que estes dois episódios representam
/// o mesmo padrão operacional?"
/// </summary>
public sealed class BehaviorCandidateEpisodeSimilarityComparer
{
    private const double Epsilon = 1e-12;

    private readonly BehaviorCandidateEpisodeSimilarityOptions _options;

    public BehaviorCandidateEpisodeSimilarityComparer(
        BehaviorCandidateEpisodeSimilarityOptions? options = null)
    {
        _options =
            options ??
            new BehaviorCandidateEpisodeSimilarityOptions();

        ValidateOptions(_options);
    }

    public BehaviorCandidateEpisodeSimilarityResult Compare(
        BehaviorCandidateEpisodeCharacteristics first,
        BehaviorCandidateEpisodeCharacteristics second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (!first.IsCharacterizable ||
            !second.IsCharacterizable)
        {
            return NotComparable(
                "Ao menos um dos episódios não possui evidência estatística suficiente.");
        }

        if (first.ProfileFeatures is null ||
            second.ProfileFeatures is null)
        {
            return NotComparable(
                "Ao menos um dos episódios não possui ProfileFeatures.");
        }

        var firstFeatures =
            first.ProfileFeatures;

        var secondFeatures =
            second.ProfileFeatures;

        /*
         * Reutiliza o mesmo princípio conceitual do DualBaseline:
         *
         * floor = max(deadband, robustScale dos blocos)
         *
         * Isso evita thresholds absolutos fixos que não respeitam
         * a escala natural de cada tag.
         */
        var deadbandFloor =
            Math.Max(
                Math.Max(
                    FiniteNonNegative(first.DeadbandRecomendado),
                    FiniteNonNegative(second.DeadbandRecomendado)),
                Epsilon);

        var comparisonFloor =
            Math.Max(
                deadbandFloor,
                Math.Max(
                    FiniteNonNegative(firstFeatures.RobustScale),
                    FiniteNonNegative(secondFeatures.RobustScale)));

        comparisonFloor =
            Math.Max(
                comparisonFloor,
                Epsilon);

        var meanDelta =
            Math.Abs(
                first.Media -
                second.Media);

        var medianDelta =
            Math.Abs(
                first.Mediana -
                second.Mediana);

        var centerStable =
            meanDelta <= comparisonFloor &&
            medianDelta <= comparisonFloor;

        var standardDeviationRatio =
            ScaleRatioWithFloor(
                first.DesvioPadrao,
                second.DesvioPadrao,
                deadbandFloor);

        var robustScaleRatio =
            ScaleRatioWithFloor(
                firstFeatures.RobustScale,
                secondFeatures.RobustScale,
                deadbandFloor);

        var scaleStable =
            standardDeviationRatio <=
                _options.MaximumScaleRatio &&
            robustScaleRatio <=
                _options.MaximumScaleRatio;

        var firstQuartileDelta =
            Math.Abs(
                first.PrimeiroQuartil -
                second.PrimeiroQuartil);

        var thirdQuartileDelta =
            Math.Abs(
                first.TerceiroQuartil -
                second.TerceiroQuartil);

        var interquartileRangeDelta =
            Math.Abs(
                first.IntervaloInterquartil -
                second.IntervaloInterquartil);

        var distributionStable =
            firstQuartileDelta <= comparisonFloor &&
            thirdQuartileDelta <= comparisonFloor &&
            interquartileRangeDelta <= comparisonFloor;

        /*
         * Para a primeira versão, a dinâmica bloqueante usa somente
         * TypicalAbsoluteStep.
         *
         * ChangeRatio e ReversalRatio são registrados abaixo como
         * diagnóstico, mas ainda não recebem threshold decisório.
         * Isso evita introduzir pesos/limites arbitrários antes de
         * termos evidência empírica nos históricos reais/simulados.
         */
        var typicalAbsoluteStepRatio =
            DirectScaleRatio(
                firstFeatures.TypicalAbsoluteStep,
                secondFeatures.TypicalAbsoluteStep);

        var dynamicsStable =
            typicalAbsoluteStepRatio <=
            _options.MaximumTypicalStepRatio;

        var changeRatioDelta =
            Math.Abs(
                firstFeatures.ChangeRatio -
                secondFeatures.ChangeRatio);

        var reversalRatioDelta =
            Math.Abs(
                firstFeatures.ReversalRatio -
                secondFeatures.ReversalRatio);

        var isSimilar =
            centerStable &&
            scaleStable &&
            distributionStable &&
            dynamicsStable;

        return new BehaviorCandidateEpisodeSimilarityResult
        {
            CanCompare =
                true,

            IsSimilar =
                isSimilar,

            CenterStable =
                centerStable,

            ScaleStable =
                scaleStable,

            DistributionStable =
                distributionStable,

            DynamicsStable =
                dynamicsStable,

            ComparisonFloor =
                comparisonFloor,

            MeanDelta =
                meanDelta,

            MedianDelta =
                medianDelta,

            FirstQuartileDelta =
                firstQuartileDelta,

            ThirdQuartileDelta =
                thirdQuartileDelta,

            InterquartileRangeDelta =
                interquartileRangeDelta,

            StandardDeviationRatio =
                standardDeviationRatio,

            RobustScaleRatio =
                robustScaleRatio,

            TypicalAbsoluteStepRatio =
                typicalAbsoluteStepRatio,

            ChangeRatioDelta =
                changeRatioDelta,

            ReversalRatioDelta =
                reversalRatioDelta,

            Reason =
                BuildReason(
                    centerStable,
                    scaleStable,
                    distributionStable,
                    dynamicsStable)
        };
    }

    private static BehaviorCandidateEpisodeSimilarityResult NotComparable(
        string reason)
    {
        return new BehaviorCandidateEpisodeSimilarityResult
        {
            CanCompare =
                false,

            IsSimilar =
                false,

            Reason =
                reason
        };
    }

    private static double ScaleRatioWithFloor(
        double first,
        double second,
        double floor)
    {
        first =
            FiniteNonNegative(first);

        second =
            FiniteNonNegative(second);

        var denominator =
            Math.Max(
                Math.Min(
                    first,
                    second),
                Math.Max(
                    floor,
                    Epsilon));

        return
            Math.Max(
                first,
                second) /
            denominator;
    }

    private static double DirectScaleRatio(
        double first,
        double second)
    {
        first =
            FiniteNonNegative(first);

        second =
            FiniteNonNegative(second);

        if (first <= Epsilon &&
            second <= Epsilon)
        {
            return 1.0;
        }

        if (first <= Epsilon ||
            second <= Epsilon)
        {
            return double.PositiveInfinity;
        }

        return
            Math.Max(
                first,
                second) /
            Math.Min(
                first,
                second);
    }

    private static double FiniteNonNegative(
        double value)
    {
        if (!double.IsFinite(value) ||
            value < 0)
        {
            return 0;
        }

        return value;
    }

    private static string BuildReason(
        bool centerStable,
        bool scaleStable,
        bool distributionStable,
        bool dynamicsStable)
    {
        if (centerStable &&
            scaleStable &&
            distributionStable &&
            dynamicsStable)
        {
            return
                "Episódios compatíveis em centro, escala, distribuição e dinâmica básica.";
        }

        var reasons =
            new List<string>();

        if (!centerStable)
        {
            reasons.Add(
                "centro incompatível");
        }

        if (!scaleStable)
        {
            reasons.Add(
                "escala incompatível");
        }

        if (!distributionStable)
        {
            reasons.Add(
                "distribuição incompatível");
        }

        if (!dynamicsStable)
        {
            reasons.Add(
                "dinâmica incompatível");
        }

        return
            "Episódios não compatíveis: " +
            string.Join(
                ", ",
                reasons) +
            ".";
    }

    private static void ValidateOptions(
        BehaviorCandidateEpisodeSimilarityOptions options)
    {
        if (!double.IsFinite(
                options.MaximumScaleRatio) ||
            options.MaximumScaleRatio < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumScaleRatio deve ser finito e maior ou igual a 1.");
        }

        if (!double.IsFinite(
                options.MaximumTypicalStepRatio) ||
            options.MaximumTypicalStepRatio < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumTypicalStepRatio deve ser finito e maior ou igual a 1.");
        }
    }
}
