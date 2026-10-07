namespace IAApi.Services.AnomalyDetection.Multivariate;
internal sealed class OnlineMultivariateAccumulator
{
    private readonly string _groupName;
    private readonly List<string> _tags;

    private readonly double[] _mean;
    private readonly double[,] _m2;

    public long SampleCount { get; private set; }

    public OnlineMultivariateAccumulator(
        string groupName,
        IEnumerable<string> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            groupName);

        _groupName = groupName.Trim();

        _tags = tags
            .Where(tag =>
                !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_tags.Count < 2)
        {
            throw new ArgumentException(
                $"O grupo '{groupName}' precisa possuir " +
                "pelo menos duas tags.",
                nameof(tags));
        }

        _mean =
            new double[_tags.Count];

        _m2 =
            new double[
                _tags.Count,
                _tags.Count];
    }

    public bool TryAdd(
        IReadOnlyDictionary<string, double> values)
    {
        var vector =
            new double[_tags.Count];

        for (var index = 0;
             index < _tags.Count;
             index++)
        {
            if (!values.TryGetValue(
                    _tags[index],
                    out var value) ||
                !double.IsFinite(value))
            {
                return false;
            }

            vector[index] = value;
        }

        SampleCount++;

        var delta =
            new double[_tags.Count];

        for (var index = 0;
             index < _tags.Count;
             index++)
        {
            delta[index] =
                vector[index] -
                _mean[index];

            _mean[index] +=
                delta[index] /
                SampleCount;
        }

        var deltaAfterMean =
            new double[_tags.Count];

        for (var index = 0;
             index < _tags.Count;
             index++)
        {
            deltaAfterMean[index] =
                vector[index] -
                _mean[index];
        }

        for (var row = 0;
             row < _tags.Count;
             row++)
        {
            for (var column = 0;
                 column < _tags.Count;
                 column++)
            {
                _m2[row, column] +=
                    delta[row] *
                    deltaAfterMean[column];
            }
        }

        return true;
    }

    public MultivariateGroupModel CriarModelo(
        string clienteId,
        int minimumAvailableTags,
        int maxLagMinutes)
    {
        if (SampleCount < 2)
        {
            throw new InvalidOperationException(
                $"O grupo '{_groupName}' não possui " +
                "amostras suficientes.");
        }

        var dimension =
            _tags.Count;

        var covariance =
            CriarMatriz(dimension);

        var correlation =
            CriarMatriz(dimension);

        var standardDeviations =
            new double[dimension];

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = 0;
                 column < dimension;
                 column++)
            {
                covariance[row][column] =
                    _m2[row, column] /
                    (SampleCount - 1);
            }

            var variance =
                Math.Max(
                    0,
                    covariance[row][row]);

            standardDeviations[row] =
                Math.Sqrt(variance);
        }

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = 0;
                 column < dimension;
                 column++)
            {
                var denominator =
                    standardDeviations[row] *
                    standardDeviations[column];

                correlation[row][column] =
                    denominator > 1e-12
                        ? covariance[row][column] /
                          denominator
                        : row == column
                            ? 1
                            : 0;
            }
        }

        if (_mean.Any(value => !double.IsFinite(value)) ||
            standardDeviations.Any(value => !double.IsFinite(value)) ||
            covariance.SelectMany(row => row).Any(value => !double.IsFinite(value)))
        {
            throw new InvalidOperationException(
                $"O grupo '{_groupName}' produziu parâmetros numéricos inválidos.");
        }

        return new MultivariateGroupModel
        {
            Version = 1,
            ClienteId = clienteId,
            GroupName = _groupName,
            TrainedAtUtc = DateTime.UtcNow,
            Algorithm = "OnlineMeanCovariance",
            SampleCount = SampleCount,
            MinimumAvailableTags =
                Math.Max(
                    2,
                    minimumAvailableTags),
            MaxLagMinutes =
                Math.Max(
                    0,
                    maxLagMinutes),
            Tags = _tags.ToList(),
            Means = _mean.ToList(),
            StandardDeviations =
                standardDeviations.ToList(),
            CovarianceMatrix = covariance,
            CorrelationMatrix = correlation
        };
    }

    private static List<List<double>>
        CriarMatriz(int dimension)
    {
        var matrix =
            new List<List<double>>(
                dimension);

        for (var row = 0;
             row < dimension;
             row++)
        {
            matrix.Add(
                Enumerable
                    .Repeat(
                        0.0,
                        dimension)
                    .ToList());
        }

        return matrix;
    }
}