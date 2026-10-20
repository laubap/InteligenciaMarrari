namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Coleção imutável de evidências com operações de consulta usadas
/// pelo Confidence Engine e pelos benchmarks.
///
/// A coleção não toma decisão de anomalia.
/// </summary>
public sealed class EvidenceCollection
{
    private readonly IReadOnlyList<Evidence> _items;

    public EvidenceCollection(
        IEnumerable<Evidence> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items =
            items
                .ToList()
                .AsReadOnly();
    }

    public IReadOnlyList<Evidence> Items =>
        _items;

    public IReadOnlyList<Evidence> Applicable =>
        _items
            .Where(item => item.Applicable)
            .ToList();

    public IReadOnlyList<Evidence> Positive =>
        _items
            .Where(
                item =>
                    item.Applicable &&
                    item.PositiveVote)
            .ToList();

    public int ApplicableCount =>
        _items.Count(item => item.Applicable);

    public int PositiveCount =>
        _items.Count(
            item =>
                item.Applicable &&
                item.PositiveVote);

    public int PositiveFamilyCount =>
        Positive
            .Select(item => item.Family)
            .Where(
                family =>
                    family !=
                    DetectorFamily.Unknown)
            .Distinct()
            .Count();

    public bool HasTemporalConfirmation =>
        Positive.Any(
            item =>
                item.Family ==
                DetectorFamily.TemporalConfirmation);

    public bool HasMultivariateEvidence =>
        Positive.Any(
            item =>
                item.Family ==
                DetectorFamily.Multivariate);

    public IReadOnlyDictionary<
        DetectorFamily,
        IReadOnlyList<Evidence>>
        PositiveByFamily =>
        Positive
            .GroupBy(item => item.Family)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<Evidence>)
                    group.ToList()
                        .AsReadOnly());

    public IReadOnlyList<Evidence> FromFamily(
        DetectorFamily family)
    {
        return _items
            .Where(
                item =>
                    item.Family ==
                    family)
            .ToList();
    }

    public IReadOnlyList<string> Validate()
    {
        var errors =
            new List<string>();

        for (var index = 0;
             index < _items.Count;
             index++)
        {
            var itemErrors =
                _items[index].Validate();

            foreach (var error in itemErrors)
            {
                errors.Add(
                    $"Evidence[{index}]: {error}");
            }
        }

        var duplicateDetectors =
            _items
                .Where(
                    item =>
                        !string.IsNullOrWhiteSpace(
                            item.Detector))
                .GroupBy(
                    item => item.Detector,
                    StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

        foreach (var duplicate in duplicateDetectors)
        {
            errors.Add(
                $"O detector '{duplicate}' produziu mais de uma " +
                "evidência na mesma coleção.");
        }

        return errors;
    }

    public static EvidenceCollection Empty { get; } =
        new(Array.Empty<Evidence>());
}
