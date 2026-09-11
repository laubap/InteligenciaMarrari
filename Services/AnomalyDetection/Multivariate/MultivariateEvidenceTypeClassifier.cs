namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.3B
///
/// Classificação diagnóstica do TIPO de evidência multivariada.
///
/// IMPORTANTE:
/// - não altera a decisão operacional;
/// - não altera readiness;
/// - não altera threshold;
/// - não altera o modelo;
/// - não afirma causa física/industrial.
///
/// A classificação usa:
/// 1. participação relativa dos desvios individuais (z²);
/// 2. correlação histórica entre as tags utilizadas;
/// 3. direção dos desvios em relação ao sinal da correlação.
///
/// "RelationalDeviation" é reservado para pares com relação histórica
/// suficientemente forte que se movem em direção incompatível com a
/// correlação aprendida.
/// </summary>
public sealed class MultivariateEvidenceTypeClassifier
{
    private const double NumericTolerance = 1e-12;

    public MultivariateEvidenceTypeClassification Classificar(
        MultivariateDetectionResult detection,
        MultivariateGroupModel model,
        MultivariateEvidenceTypeClassifierOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(model);

        var effectiveOptions =
            options ??
            new MultivariateEvidenceTypeClassifierOptions();

        ValidarOptions(
            effectiveOptions);

        if (!detection.Aplicavel)
        {
            return Indeterminado(
                "DetectionNotApplicable");
        }

        var contributions =
            detection.Contribuicoes
                .Where(item =>
                    item is not null &&
                    !string.IsNullOrWhiteSpace(
                        item.TagName) &&
                    double.IsFinite(
                        item.ValorAtual) &&
                    double.IsFinite(
                        item.MediaTreinamento) &&
                    double.IsFinite(
                        item.DesvioPadraoTreinamento) &&
                    item.DesvioPadraoTreinamento >
                        NumericTolerance)
                .ToList();

        if (contributions.Count < 2)
        {
            return Indeterminado(
                "InsufficientUsableContributions");
        }

        if (!ModeloCompativel(
                model,
                contributions))
        {
            return Indeterminado(
                "ModelOrTagMappingInvalid");
        }

        var signed =
            contributions
                .Select(item =>
                    new SignedContribution(
                        item.TagName.Trim(),
                        (
                            item.ValorAtual -
                            item.MediaTreinamento
                        ) /
                        item.DesvioPadraoTreinamento))
                .Where(item =>
                    double.IsFinite(
                        item.SignedZ))
                .ToList();

        if (signed.Count < 2)
        {
            return Indeterminado(
                "InsufficientFiniteSignedZScores");
        }

        var energy =
            signed
                .Select(item =>
                    item.SignedZ *
                    item.SignedZ)
                .ToArray();

        var totalEnergy =
            energy.Sum();

        if (!double.IsFinite(
                totalEnergy) ||
            totalEnergy <= NumericTolerance)
        {
            return Indeterminado(
                "InsufficientDeviationEnergy");
        }

        var dominantIndex =
            0;

        for (var index = 1;
             index < energy.Length;
             index++)
        {
            if (energy[index] >
                energy[dominantIndex])
            {
                dominantIndex =
                    index;
            }
        }

        var dominantShare =
            energy[dominantIndex] /
            totalEnergy;

        var relationship =
            ObterRelacaoMaisForte(
                signed,
                model);

        var meaningfulRelationshipDirectionContradiction =
            relationship is not null &&
            relationship.AbsoluteCorrelation >=
                effectiveOptions
                    .RelationalCorrelationThreshold &&
            relationship.DirectionContradicted;

        if (dominantShare >=
            effectiveOptions.SingleTagDominanceShare)
        {
            return new MultivariateEvidenceTypeClassification
            {
                EvidenceType =
                    MultivariateEvidenceTypes
                        .SingleTagDominated,

                Reason =
                    "Uma única tag concentra a maior parte da energia de desvio individual.",

                DominantTag =
                    signed[dominantIndex]
                        .TagName,

                DominantSquaredZShare =
                    dominantShare,

                MaximumAbsoluteCorrelation =
                    relationship?
                        .AbsoluteCorrelation,

                RelationshipFirstTag =
                    relationship?
                        .FirstTag,

                RelationshipSecondTag =
                    relationship?
                        .SecondTag,

                RelationshipCorrelation =
                    relationship?
                        .Correlation,

                RelationshipDirectionContradicted =
                    relationship is null
                        ? null
                        : meaningfulRelationshipDirectionContradiction
            };
        }

        if (relationship is not null &&
            meaningfulRelationshipDirectionContradiction)
        {
            return new MultivariateEvidenceTypeClassification
            {
                EvidenceType =
                    MultivariateEvidenceTypes
                        .RelationalDeviation,

                Reason =
                    "Um par com relação histórica relevante desviou em direção incompatível com a correlação aprendida.",

                DominantTag =
                    signed[dominantIndex]
                        .TagName,

                DominantSquaredZShare =
                    dominantShare,

                MaximumAbsoluteCorrelation =
                    relationship.AbsoluteCorrelation,

                RelationshipFirstTag =
                    relationship.FirstTag,

                RelationshipSecondTag =
                    relationship.SecondTag,

                RelationshipCorrelation =
                    relationship.Correlation,

                RelationshipDirectionContradicted =
                    true
            };
        }

        var moderateDeviationCount =
            signed.Count(item =>
                Math.Abs(
                    item.SignedZ) >=
                effectiveOptions
                    .ModerateAbsoluteZThreshold);

        if (moderateDeviationCount >= 2)
        {
            return new MultivariateEvidenceTypeClassification
            {
                EvidenceType =
                    MultivariateEvidenceTypes
                        .JointDeviation,

                Reason =
                    "Duas ou mais tags apresentam desvios individuais moderados sem evidência suficiente de quebra relacional.",

                DominantTag =
                    signed[dominantIndex]
                        .TagName,

                DominantSquaredZShare =
                    dominantShare,

                MaximumAbsoluteCorrelation =
                    relationship?
                        .AbsoluteCorrelation,

                RelationshipFirstTag =
                    relationship?
                        .FirstTag,

                RelationshipSecondTag =
                    relationship?
                        .SecondTag,

                RelationshipCorrelation =
                    relationship?
                        .Correlation,

                RelationshipDirectionContradicted =
                    relationship is null
                        ? null
                        : meaningfulRelationshipDirectionContradiction
            };
        }

        return new MultivariateEvidenceTypeClassification
        {
            EvidenceType =
                MultivariateEvidenceTypes
                    .Indeterminate,

            Reason =
                "Os desvios disponíveis não sustentam uma classificação diagnóstica mais específica.",

            DominantTag =
                signed[dominantIndex]
                    .TagName,

            DominantSquaredZShare =
                dominantShare,

            MaximumAbsoluteCorrelation =
                relationship?
                    .AbsoluteCorrelation,

            RelationshipFirstTag =
                relationship?
                    .FirstTag,

            RelationshipSecondTag =
                relationship?
                    .SecondTag,

            RelationshipCorrelation =
                relationship?
                    .Correlation,

            RelationshipDirectionContradicted =
                relationship?
                    .DirectionContradicted
        };
    }

    private static StrongestRelationship?
        ObterRelacaoMaisForte(
            IReadOnlyList<SignedContribution> contributions,
            MultivariateGroupModel model)
    {
        var tagIndexes =
            model.Tags
                .Select(
                    (tag, index) =>
                        new
                        {
                            Tag =
                                tag,

                            Index =
                                index
                        })
                .ToDictionary(
                    item =>
                        item.Tag,
                    item =>
                        item.Index,
                    StringComparer.OrdinalIgnoreCase);

        StrongestRelationship?
            strongest =
                null;

        for (var first = 0;
             first < contributions.Count;
             first++)
        {
            for (var second = first + 1;
                 second < contributions.Count;
                 second++)
            {
                if (!tagIndexes.TryGetValue(
                        contributions[first]
                            .TagName,
                        out var firstIndex) ||
                    !tagIndexes.TryGetValue(
                        contributions[second]
                            .TagName,
                        out var secondIndex))
                {
                    continue;
                }

                var correlation =
                    model.CorrelationMatrix[
                        firstIndex][
                        secondIndex];

                if (!double.IsFinite(
                        correlation))
                {
                    continue;
                }

                var absolute =
                    Math.Abs(
                        correlation);

                if (strongest is not null &&
                    absolute <=
                        strongest
                            .AbsoluteCorrelation)
                {
                    continue;
                }

                var firstZ =
                    contributions[first]
                        .SignedZ;

                var secondZ =
                    contributions[second]
                        .SignedZ;

                /*
                 * Correlação positiva espera sinais iguais.
                 * Correlação negativa espera sinais opostos.
                 *
                 * Se um dos desvios for praticamente zero, não tratamos
                 * como contradição relacional.
                 */
                var directionContradicted =
                    Math.Abs(firstZ) >
                        NumericTolerance &&
                    Math.Abs(secondZ) >
                        NumericTolerance &&
                    (
                        correlation > 0
                            ? Math.Sign(firstZ) !=
                              Math.Sign(secondZ)
                            : correlation < 0
                                ? Math.Sign(firstZ) ==
                                  Math.Sign(secondZ)
                                : false
                    );

                strongest =
                    new StrongestRelationship(
                        contributions[first]
                            .TagName,
                        contributions[second]
                            .TagName,
                        correlation,
                        absolute,
                        directionContradicted);
            }
        }

        return strongest;
    }

    private static bool ModeloCompativel(
        MultivariateGroupModel model,
        IReadOnlyCollection<
            MultivariateTagContribution> contributions)
    {
        var dimension =
            model.Tags?.Count ??
            0;

        if (dimension < 2 ||
            model.CorrelationMatrix is null ||
            model.CorrelationMatrix.Count !=
                dimension ||
            model.CorrelationMatrix.Any(row =>
                row is null ||
                row.Count !=
                    dimension))
        {
            return false;
        }

        var modelTags =
            new HashSet<string>(
                model.Tags,
                StringComparer.OrdinalIgnoreCase);

        return contributions.All(item =>
            modelTags.Contains(
                item.TagName.Trim()));
    }

    private static void ValidarOptions(
        MultivariateEvidenceTypeClassifierOptions options)
    {
        if (!double.IsFinite(
                options
                    .SingleTagDominanceShare) ||
            options
                    .SingleTagDominanceShare <=
                0.5 ||
            options
                    .SingleTagDominanceShare >=
                1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options
                        .SingleTagDominanceShare),
                "SingleTagDominanceShare deve estar no intervalo (0.5, 1.0).");
        }

        if (!double.IsFinite(
                options
                    .RelationalCorrelationThreshold) ||
            options
                    .RelationalCorrelationThreshold <=
                0 ||
            options
                    .RelationalCorrelationThreshold >
                1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options
                        .RelationalCorrelationThreshold),
                "RelationalCorrelationThreshold deve estar no intervalo (0, 1].");
        }

        if (!double.IsFinite(
                options
                    .ModerateAbsoluteZThreshold) ||
            options
                    .ModerateAbsoluteZThreshold <=
                0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options
                        .ModerateAbsoluteZThreshold),
                "ModerateAbsoluteZThreshold deve ser maior que zero.");
        }
    }

    private static MultivariateEvidenceTypeClassification
        Indeterminado(
            string reason)
    {
        return new MultivariateEvidenceTypeClassification
        {
            EvidenceType =
                MultivariateEvidenceTypes
                    .Indeterminate,

            Reason =
                reason
        };
    }

    private sealed record SignedContribution(
        string TagName,
        double SignedZ);

    private sealed record StrongestRelationship(
        string FirstTag,
        string SecondTag,
        double Correlation,
        double AbsoluteCorrelation,
        bool DirectionContradicted);
}

public sealed class MultivariateEvidenceTypeClassifierOptions
{
    /// <summary>
    /// Fração mínima da soma de z² para considerar que uma única tag
    /// domina o evento. Default: 70%.
    /// </summary>
    public double SingleTagDominanceShare { get; set; } =
        0.70;

    /// <summary>
    /// Correlação absoluta mínima para considerar que existe relação
    /// linear histórica relevante no diagnóstico.
    /// </summary>
    public double RelationalCorrelationThreshold { get; set; } =
        0.30;

    /// <summary>
    /// |z| mínimo para contar uma tag como desvio individual moderado.
    /// </summary>
    public double ModerateAbsoluteZThreshold { get; set; } =
        1.0;
}

public static class MultivariateEvidenceTypes
{
    public const string Indeterminate =
        "Indeterminate";

    public const string SingleTagDominated =
        "SingleTagDominated";

    public const string JointDeviation =
        "JointDeviation";

    public const string RelationalDeviation =
        "RelationalDeviation";
}

public sealed class MultivariateEvidenceTypeClassification
{
    public string EvidenceType { get; init; } =
        MultivariateEvidenceTypes
            .Indeterminate;

    public string Reason { get; init; } =
        "";

    public string? DominantTag { get; init; }

    public double? DominantSquaredZShare { get; init; }

    public double? MaximumAbsoluteCorrelation { get; init; }

    public string? RelationshipFirstTag { get; init; }

    public string? RelationshipSecondTag { get; init; }

    public double? RelationshipCorrelation { get; init; }

    public bool? RelationshipDirectionContradicted { get; init; }
}
