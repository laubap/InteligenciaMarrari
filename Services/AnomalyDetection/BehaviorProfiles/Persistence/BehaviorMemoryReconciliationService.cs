using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;

public sealed record BehaviorMemoryReconciliationOptions
{
    public double MaximumScaleRatio { get; init; } = 2.0;

    public double MaximumVariationRatio { get; init; } = 2.0;

    public double MaximumDeadbandRatio { get; init; } = 2.0;

    public double MaximumTypicalStepRatio { get; init; } = 2.0;
}

public enum BehaviorMemoryReconciliationOutcome
{
    NewBehavior = 0,
    ExistingBehavior = 1,
    Ambiguous = 2
}

/// <summary>
/// Comparação explicável entre a descoberta atual e um comportamento
/// já persistido.
///
/// Somente métricas realmente presentes nos snapshots persistidos
/// participam da decisão.
/// </summary>
public sealed record BehaviorMemorySnapshotComparison
{
    public int BehaviorId { get; init; }

    public string BehaviorCode { get; init; } = "";

    public int SnapshotVersion { get; init; }

    public bool IsCompatible { get; init; }

    /// <summary>
    /// True somente quando os dois snapshots possuem todas as dimensões
    /// ricas introduzidas na B3.25. Snapshots legados continuam válidos
    /// usando automaticamente a regra anterior.
    /// </summary>
    public bool UsedRichIdentity { get; init; }

    public bool CenterStable { get; init; }

    public bool ScaleStable { get; init; }

    public bool VariationStable { get; init; }

    public bool DeadbandStable { get; init; }

    public bool? DistributionStable { get; init; }

    public bool? DynamicsStable { get; init; }

    /// <summary>
    /// Margem de segurança até os limites de compatibilidade.
    /// 100 = coincidência máxima nas dimensões avaliadas.
    /// 0 = exatamente no limite aceito.
    /// </summary>
    public double CompatibilityConfidence { get; init; }

    public double ComparisonFloor { get; init; }

    public double MeanDelta { get; init; }

    public double? MedianDelta { get; init; }

    public double? FirstQuartileDelta { get; init; }

    public double? ThirdQuartileDelta { get; init; }

    public double? InterquartileRangeDelta { get; init; }

    public double StandardDeviationRatio { get; init; }

    public double? RobustScaleRatio { get; init; }

    public double? TypicalAbsoluteStepRatio { get; init; }

    public double VariationRatio { get; init; }

    public double DeadbandRatio { get; init; }

    public string Reason { get; init; } = "";
}

public sealed record BehaviorMemoryReconciliationResult
{
    public BehaviorMemoryReconciliationOutcome Outcome { get; init; }

    public int? MatchedBehaviorId { get; init; }

    public string? MatchedBehaviorCode { get; init; }

    /// <summary>
    /// Confiança de pertencimento ao comportamento persistido.
    ///
    /// Só é preenchida quando existe exatamente um match.
    /// </summary>
    public BehaviorIdentityConfidenceResult? MatchedIdentityConfidence { get; init; }

    public TagComportamentoSnapshotIa CandidateSnapshot { get; init; } =
        new();

    public IReadOnlyList<BehaviorMemorySnapshotComparison> Comparisons { get; init; } =
        Array.Empty<BehaviorMemorySnapshotComparison>();

    /// <summary>
    /// Todos os códigos já usados no escopo cliente + tag,
    /// inclusive Deprecated. Códigos antigos não devem ser reciclados.
    /// </summary>
    public IReadOnlyList<string> ReservedIdentities { get; init; } =
        Array.Empty<string>();

    public string Reason { get; init; } = "";
}

/// <summary>
/// Reconcilia uma descoberta com memórias comportamentais já persistidas.
///
/// Importante:
/// uma descoberta NÃO precisa ser Known para ser comparada com um BEH já Known.
///
/// Isso permite aprendizado incremental:
/// uma única nova ocorrência pode reforçar uma identidade existente,
/// embora não possua evidência suficiente para criar um comportamento novo.
///
/// - exatamente um match: reutiliza BEH existente;
/// - nenhum match: comportamento potencialmente novo;
/// - múltiplos matches: ambíguo, sem escolha automática.
///
/// Deprecated não participa do matching, mas seu código continua reservado.
/// </summary>
public sealed class BehaviorMemoryReconciliationService
{
    private const double Epsilon = 1e-9;

    private readonly AppDbContext _context;

    private readonly BehaviorMemoryPersistencePlanBuilder _snapshotBuilder;

    private readonly BehaviorMemoryReconciliationOptions _options;

    public BehaviorMemoryReconciliationService(
        AppDbContext context,
        BehaviorMemoryPersistencePlanBuilder? snapshotBuilder = null,
        BehaviorMemoryReconciliationOptions? options = null)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));

        _snapshotBuilder =
            snapshotBuilder ??
            new BehaviorMemoryPersistencePlanBuilder();

        _options =
            options ??
            new BehaviorMemoryReconciliationOptions();

        ValidateOptions(
            _options);
    }

    /// <summary>
    /// Mantém compatibilidade com consumidores já existentes.
    /// A maturidade do lifecycle não é mais pré-condição para matching.
    /// </summary>
    public Task<BehaviorMemoryReconciliationResult> ReconcileAsync(
        string clienteId,
        string tagName,
        BehaviorMemoryLifecycleEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            evaluation);

        return ReconcileAsync(
            clienteId,
            tagName,
            evaluation.Candidate,
            cancellationToken);
    }

    public async Task<BehaviorMemoryReconciliationResult> ReconcileAsync(
        string clienteId,
        string tagName,
        BehaviorDiscoveryCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ValidateText(
            clienteId,
            nameof(clienteId));

        ValidateText(
            tagName,
            nameof(tagName));

        ArgumentNullException.ThrowIfNull(
            candidate);

        var eligibleOccurrences =
            candidate.Occurrences
                .Where(x =>
                    x is not null &&
                    x.IsCharacterizable)
                .OrderBy(x =>
                    x.Inicio)
                .ThenBy(x =>
                    x.SourceSegmentIndex)
                .ThenBy(x =>
                    x.EpisodeIndex)
                .ToArray();

        if (eligibleOccurrences.Length == 0)
        {
            throw new InvalidOperationException(
                "A reconciliação exige ao menos uma ocorrência caracterizável.");
        }

        var candidateSnapshot =
            _snapshotBuilder.BuildSnapshotPreview(
                eligibleOccurrences,
                version: 1);

        /*
         * Todos os códigos permanecem reservados, inclusive Candidate
         * e Deprecated, para impedir reciclagem de identidade.
         */
        var allBehaviors =
            await _context.TagsComportamentosIa
                .AsNoTracking()
                .Where(x =>
                    x.ClienteId == clienteId &&
                    x.TagName == tagName)
                .OrderBy(x =>
                    x.Id)
                .ToListAsync(
                    cancellationToken);

        var reserved =
            allBehaviors
                .Select(x =>
                    x.Codigo)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(x =>
                    x,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var knownBehaviors =
            allBehaviors
                .Where(x =>
                    string.Equals(
                        x.Status,
                        TagComportamentoIa.StatusKnown,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (knownBehaviors.Length == 0)
        {
            return NewBehavior(
                candidateSnapshot,
                reserved,
                Array.Empty<BehaviorMemorySnapshotComparison>(),
                "Nenhum comportamento Known persistido existe para esta tag.");
        }

        var knownIds =
            knownBehaviors
                .Select(x =>
                    x.Id)
                .ToArray();

        var snapshots =
            await _context.TagsComportamentosSnapshotsIa
                .AsNoTracking()
                .Where(x =>
                    knownIds.Contains(
                        x.TagComportamentoIaId))
                .ToListAsync(
                    cancellationToken);

        var latestSnapshotByBehavior =
            snapshots
                .GroupBy(x =>
                    x.TagComportamentoIaId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .OrderByDescending(x =>
                                x.Versao)
                            .ThenByDescending(x =>
                                x.DataCriacao)
                            .First());

        var evidences =
            new List<MatchEvidence>();

        foreach (var behavior in knownBehaviors)
        {
            if (!latestSnapshotByBehavior.TryGetValue(
                    behavior.Id,
                    out var persistedSnapshot))
            {
                continue;
            }

            var aggregateComparison =
                Compare(
                    behavior,
                    persistedSnapshot,
                    candidateSnapshot);

            var occurrenceConfidences =
                new List<BehaviorOccurrenceIdentityConfidence>();

            var everyOccurrenceCompatible =
                aggregateComparison.IsCompatible;

            var weakestConfidence =
                aggregateComparison.CompatibilityConfidence;

            if (aggregateComparison.IsCompatible)
            {
                foreach (var occurrence in eligibleOccurrences)
                {
                    var occurrenceSnapshot =
                        _snapshotBuilder.BuildSnapshotPreview(
                            new[]
                            {
                                occurrence
                            },
                            version: 1);

                    var occurrenceComparison =
                        Compare(
                            behavior,
                            persistedSnapshot,
                            occurrenceSnapshot);

                    if (!occurrenceComparison.IsCompatible)
                    {
                        everyOccurrenceCompatible =
                            false;
                    }

                    weakestConfidence =
                        Math.Min(
                            weakestConfidence,
                            occurrenceComparison.CompatibilityConfidence);

                    occurrenceConfidences.Add(
                        new BehaviorOccurrenceIdentityConfidence
                        {
                            SourceSegmentIndex =
                                occurrence.SourceSegmentIndex,

                            EpisodeIndex =
                                occurrence.EpisodeIndex,

                            Confidence =
                                occurrenceComparison.CompatibilityConfidence
                        });
                }
            }

            /*
             * Só substituímos a explicação agregada quando o agregado era
             * compatível e a incompatibilidade apareceu exclusivamente em
             * uma ocorrência individual.
             *
             * Se o próprio agregado já era incompatível, preservamos a
             * dimensão real apontada por Compare (ex.: distribuição).
             */
            if (aggregateComparison.IsCompatible &&
                !everyOccurrenceCompatible)
            {
                aggregateComparison =
                    aggregateComparison with
                    {
                        IsCompatible =
                            false,

                        CompatibilityConfidence =
                            Math.Min(
                                aggregateComparison.CompatibilityConfidence,
                                weakestConfidence),

                        Reason =
                            "O snapshot agregado parecia compatível, mas ao menos uma ocorrência individual não foi compatível com a memória persistida."
                    };
            }

            evidences.Add(
                new MatchEvidence(
                    aggregateComparison,
                    occurrenceConfidences,
                    Math.Min(
                        aggregateComparison.CompatibilityConfidence,
                        weakestConfidence)));
        }

        var comparisons =
            evidences
                .Select(x =>
                    x.Comparison)
                .ToArray();

        var matches =
            evidences
                .Where(x =>
                    x.Comparison.IsCompatible)
                .OrderBy(x =>
                    x.Comparison.BehaviorCode,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(x =>
                    x.Comparison.BehaviorId)
                .ToArray();

        if (matches.Length == 0)
        {
            return NewBehavior(
                candidateSnapshot,
                reserved,
                comparisons,
                "Nenhum comportamento Known existente foi compatível com a descoberta atual.");
        }

        if (matches.Length > 1)
        {
            return new BehaviorMemoryReconciliationResult
            {
                Outcome =
                    BehaviorMemoryReconciliationOutcome.Ambiguous,

                CandidateSnapshot =
                    candidateSnapshot,

                Comparisons =
                    comparisons,

                ReservedIdentities =
                    reserved,

                Reason =
                    $"A descoberta atual é compatível com {matches.Length} comportamentos existentes; nenhuma identidade foi escolhida automaticamente."
            };
        }

        var match =
            matches[0];

        var identityConfidence =
            new BehaviorIdentityConfidenceResult
            {
                CanEvaluate =
                    true,

                Confidence =
                    match.Confidence,

                Occurrences =
                    match.OccurrenceConfidences,

                PairCount =
                    0,

                WeakestPairConfidence =
                    match.Confidence,

                Reason =
                    "Confiança calculada pela margem de compatibilidade entre a nova evidência e o snapshot persistido."
            };

        return new BehaviorMemoryReconciliationResult
        {
            Outcome =
                BehaviorMemoryReconciliationOutcome.ExistingBehavior,

            MatchedBehaviorId =
                match.Comparison.BehaviorId,

            MatchedBehaviorCode =
                match.Comparison.BehaviorCode,

            MatchedIdentityConfidence =
                identityConfidence,

            CandidateSnapshot =
                candidateSnapshot,

            Comparisons =
                comparisons,

            ReservedIdentities =
                reserved,

            Reason =
                $"A descoberta atual corresponde ao comportamento persistido {match.Comparison.BehaviorCode}."
        };
    }

    private BehaviorMemorySnapshotComparison Compare(
        TagComportamentoIa behavior,
        TagComportamentoSnapshotIa persisted,
        TagComportamentoSnapshotIa current)
    {
        var useRichIdentity =
            HasCompleteRichIdentity(
                persisted) &&
            HasCompleteRichIdentity(
                current);

        /*
         * O deadband continua sendo o floor da escala clássica.
         *
         * Quando a identidade rica está disponível, RobustScale também
         * participa do floor usado para centro/distribuição, reproduzindo
         * o mesmo princípio do comparador Candidate.
         */
        var deadbandFloor =
            Math.Max(
                Math.Max(
                    Math.Abs(
                        persisted.DeadbandRecomendado),
                    Math.Abs(
                        current.DeadbandRecomendado)),
                Epsilon);

        var comparisonFloor =
            deadbandFloor;

        if (useRichIdentity)
        {
            comparisonFloor =
                Math.Max(
                    comparisonFloor,
                    Math.Max(
                        Math.Abs(
                            persisted.RobustScale!.Value),
                        Math.Abs(
                            current.RobustScale!.Value)));
        }

        comparisonFloor =
            Math.Max(
                comparisonFloor,
                Epsilon);

        var meanDelta =
            Math.Abs(
                current.Media -
                persisted.Media);

        double? medianDelta =
            null;

        if (useRichIdentity)
        {
            medianDelta =
                Math.Abs(
                    current.Mediana!.Value -
                    persisted.Mediana!.Value);
        }

        var centerStable =
            meanDelta <=
                comparisonFloor &&
            (!useRichIdentity ||
             medianDelta!.Value <=
                comparisonFloor);

        var standardDeviationRatio =
            RatioWithFloor(
                current.DesvioPadrao,
                persisted.DesvioPadrao,
                deadbandFloor);

        double? robustScaleRatio =
            null;

        if (useRichIdentity)
        {
            robustScaleRatio =
                RatioWithFloor(
                    current.RobustScale!.Value,
                    persisted.RobustScale!.Value,
                    deadbandFloor);
        }

        var scaleStable =
            standardDeviationRatio <=
                _options.MaximumScaleRatio &&
            (!useRichIdentity ||
             robustScaleRatio!.Value <=
                _options.MaximumScaleRatio);

        double? firstQuartileDelta =
            null;

        double? thirdQuartileDelta =
            null;

        double? interquartileRangeDelta =
            null;

        bool? distributionStable =
            null;

        if (useRichIdentity)
        {
            firstQuartileDelta =
                Math.Abs(
                    current.PrimeiroQuartil!.Value -
                    persisted.PrimeiroQuartil!.Value);

            thirdQuartileDelta =
                Math.Abs(
                    current.TerceiroQuartil!.Value -
                    persisted.TerceiroQuartil!.Value);

            interquartileRangeDelta =
                Math.Abs(
                    current.IntervaloInterquartil!.Value -
                    persisted.IntervaloInterquartil!.Value);

            distributionStable =
                firstQuartileDelta.Value <=
                    comparisonFloor &&
                thirdQuartileDelta.Value <=
                    comparisonFloor &&
                interquartileRangeDelta.Value <=
                    comparisonFloor;
        }

        double? typicalAbsoluteStepRatio =
            null;

        bool? dynamicsStable =
            null;

        if (useRichIdentity)
        {
            typicalAbsoluteStepRatio =
                DirectRatio(
                    current.TypicalAbsoluteStep!.Value,
                    persisted.TypicalAbsoluteStep!.Value);

            dynamicsStable =
                typicalAbsoluteStepRatio.Value <=
                    _options.MaximumTypicalStepRatio;
        }

        var variationRatio =
            DirectRatio(
                current.VariacaoMedia,
                persisted.VariacaoMedia);

        var deadbandRatio =
            DirectRatio(
                current.DeadbandRecomendado,
                persisted.DeadbandRecomendado);

        var variationStable =
            variationRatio <=
            _options.MaximumVariationRatio;

        var deadbandStable =
            deadbandRatio <=
            _options.MaximumDeadbandRatio;

        var centerDistance =
            Clamp01(
                meanDelta /
                comparisonFloor);

        if (useRichIdentity)
        {
            centerDistance =
                Math.Max(
                    centerDistance,
                    Clamp01(
                        medianDelta!.Value /
                        comparisonFloor));
        }

        var scaleDistance =
            NormalizeRatioDistance(
                standardDeviationRatio,
                _options.MaximumScaleRatio);

        if (useRichIdentity)
        {
            scaleDistance =
                Math.Max(
                    scaleDistance,
                    NormalizeRatioDistance(
                        robustScaleRatio!.Value,
                        _options.MaximumScaleRatio));
        }

        var distributionDistance =
            0.0;

        if (useRichIdentity)
        {
            distributionDistance =
                Math.Max(
                    Clamp01(
                        firstQuartileDelta!.Value /
                        comparisonFloor),
                    Math.Max(
                        Clamp01(
                            thirdQuartileDelta!.Value /
                            comparisonFloor),
                        Clamp01(
                            interquartileRangeDelta!.Value /
                            comparisonFloor)));
        }

        var dynamicsDistance =
            useRichIdentity
                ? NormalizeRatioDistance(
                    typicalAbsoluteStepRatio!.Value,
                    _options.MaximumTypicalStepRatio)
                : 0.0;

        var variationDistance =
            NormalizeRatioDistance(
                variationRatio,
                _options.MaximumVariationRatio);

        var deadbandDistance =
            NormalizeRatioDistance(
                deadbandRatio,
                _options.MaximumDeadbandRatio);

        var worstDistance =
            new[]
            {
                centerDistance,
                scaleDistance,
                distributionDistance,
                dynamicsDistance,
                variationDistance,
                deadbandDistance
            }
            .Max();

        var compatibilityConfidence =
            Math.Clamp(
                (1.0 - worstDistance) *
                100.0,
                0,
                100);

        /*
         * Snapshot legado:
         * mantém exatamente as quatro famílias já utilizadas
         * (centro, escala, variação e deadband).
         *
         * Snapshot rico:
         * acrescenta mediana, quartis/IQR, RobustScale e passo típico.
         */
        var compatible =
            centerStable &&
            scaleStable &&
            variationStable &&
            deadbandStable &&
            (!useRichIdentity ||
             distributionStable == true) &&
            (!useRichIdentity ||
             dynamicsStable == true) &&
            compatibilityConfidence >
                Epsilon;

        var incompatibleDimensions =
            new List<string>();

        if (!centerStable)
        {
            incompatibleDimensions.Add(
                "centro");
        }

        if (!scaleStable)
        {
            incompatibleDimensions.Add(
                "escala");
        }

        if (!variationStable)
        {
            incompatibleDimensions.Add(
                "variação");
        }

        if (!deadbandStable)
        {
            incompatibleDimensions.Add(
                "deadband");
        }

        if (useRichIdentity &&
            distributionStable !=
                true)
        {
            incompatibleDimensions.Add(
                "distribuição");
        }

        if (useRichIdentity &&
            dynamicsStable !=
                true)
        {
            incompatibleDimensions.Add(
                "dinâmica");
        }

        if (compatible == false &&
            incompatibleDimensions.Count == 0 &&
            compatibilityConfidence <=
                Epsilon)
        {
            incompatibleDimensions.Add(
                "margem de segurança");
        }

        return new BehaviorMemorySnapshotComparison
        {
            BehaviorId =
                behavior.Id,

            BehaviorCode =
                behavior.Codigo,

            SnapshotVersion =
                persisted.Versao,

            IsCompatible =
                compatible,

            UsedRichIdentity =
                useRichIdentity,

            CenterStable =
                centerStable,

            ScaleStable =
                scaleStable,

            VariationStable =
                variationStable,

            DeadbandStable =
                deadbandStable,

            DistributionStable =
                distributionStable,

            DynamicsStable =
                dynamicsStable,

            CompatibilityConfidence =
                compatibilityConfidence,

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

            VariationRatio =
                variationRatio,

            DeadbandRatio =
                deadbandRatio,

            Reason =
                compatible
                    ? useRichIdentity
                        ? "Snapshot atual é compatível com a memória persistida usando identidade comportamental rica."
                        : "Snapshot atual é compatível com a memória persistida usando fallback legado."
                    : $"Snapshot incompatível em: {string.Join(", ", incompatibleDimensions)}."
        };
    }

    private static bool HasCompleteRichIdentity(
        TagComportamentoSnapshotIa snapshot)
    {
        return
            IsFinite(
                snapshot.Mediana) &&
            IsFinite(
                snapshot.PrimeiroQuartil) &&
            IsFinite(
                snapshot.TerceiroQuartil) &&
            IsFiniteNonNegative(
                snapshot.IntervaloInterquartil) &&
            IsFiniteNonNegative(
                snapshot.RobustScale) &&
            IsFiniteNonNegative(
                snapshot.TypicalAbsoluteStep);
    }

    private static bool IsFinite(
        double? value)
    {
        return
            value.HasValue &&
            double.IsFinite(
                value.Value);
    }

    private static bool IsFiniteNonNegative(
        double? value)
    {
        return
            IsFinite(
                value) &&
            value!.Value >=
                0;
    }

    private static BehaviorMemoryReconciliationResult NewBehavior(
        TagComportamentoSnapshotIa candidateSnapshot,
        IReadOnlyList<string> reserved,
        IReadOnlyList<BehaviorMemorySnapshotComparison> comparisons,
        string reason)
    {
        return new BehaviorMemoryReconciliationResult
        {
            Outcome =
                BehaviorMemoryReconciliationOutcome.NewBehavior,

            CandidateSnapshot =
                candidateSnapshot,

            Comparisons =
                comparisons,

            ReservedIdentities =
                reserved,

            Reason =
                reason
        };
    }

    private static double RatioWithFloor(
        double first,
        double second,
        double floor)
    {
        var firstMagnitude =
            Math.Max(
                Math.Abs(
                    first),
                floor);

        var secondMagnitude =
            Math.Max(
                Math.Abs(
                    second),
                floor);

        return
            Math.Max(
                firstMagnitude,
                secondMagnitude) /
            Math.Min(
                firstMagnitude,
                secondMagnitude);
    }

    private static double DirectRatio(
        double first,
        double second)
    {
        var firstMagnitude =
            Math.Abs(
                first);

        var secondMagnitude =
            Math.Abs(
                second);

        if (firstMagnitude <= Epsilon &&
            secondMagnitude <= Epsilon)
        {
            return 1;
        }

        if (firstMagnitude <= Epsilon ||
            secondMagnitude <= Epsilon)
        {
            return double.PositiveInfinity;
        }

        return
            Math.Max(
                firstMagnitude,
                secondMagnitude) /
            Math.Min(
                firstMagnitude,
                secondMagnitude);
    }

    private static double NormalizeRatioDistance(
        double ratio,
        double acceptedMaximum)
    {
        if (!double.IsFinite(
                ratio))
        {
            return 1;
        }

        ratio =
            Math.Max(
                ratio,
                1.0);

        if (acceptedMaximum <=
            1.0 + Epsilon)
        {
            return
                ratio <=
                    acceptedMaximum + Epsilon
                    ? 0
                    : 1;
        }

        return Clamp01(
            (ratio - 1.0) /
            (acceptedMaximum - 1.0));
    }

    private static double Clamp01(
        double value)
    {
        if (!double.IsFinite(
                value))
        {
            return 1;
        }

        return Math.Clamp(
            value,
            0,
            1);
    }

    private static void ValidateOptions(
        BehaviorMemoryReconciliationOptions options)
    {
        ValidateRatio(
            options.MaximumScaleRatio,
            nameof(options.MaximumScaleRatio));

        ValidateRatio(
            options.MaximumVariationRatio,
            nameof(options.MaximumVariationRatio));

        ValidateRatio(
            options.MaximumDeadbandRatio,
            nameof(options.MaximumDeadbandRatio));

        ValidateRatio(
            options.MaximumTypicalStepRatio,
            nameof(options.MaximumTypicalStepRatio));
    }

    private static void ValidateRatio(
        double value,
        string name)
    {
        if (!double.IsFinite(
                value) ||
            value < 1)
        {
            throw new ArgumentOutOfRangeException(
                name,
                "O limite de razão deve ser finito e maior ou igual a 1.");
        }
    }

    private static void ValidateText(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "O valor é obrigatório.",
                parameterName);
        }
    }

    private sealed record MatchEvidence(
        BehaviorMemorySnapshotComparison Comparison,
        IReadOnlyList<BehaviorOccurrenceIdentityConfidence> OccurrenceConfidences,
        double Confidence);
}
