namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// Compara um vetor multivariado atual com o modelo aprendido
/// no banco limpo usando distância de Mahalanobis regularizada.
/// </summary>
public sealed class MultivariateDetector
{
    private const double RegularizationFactor = 1e-6;
    private const double DefaultAnomalyScoreThreshold = 75.0;

    private readonly MultivariateModelPersistenceService
        _persistenceService;

    public MultivariateDetector(
        MultivariateModelPersistenceService persistenceService)
    {
        _persistenceService = persistenceService
            ?? throw new ArgumentNullException(
                nameof(persistenceService));
    }

    public async Task<MultivariateDetectionResult> DetectarAsync(
        BufferedGroupReading reading,
        double anomalyScoreThreshold =
            DefaultAnomalyScoreThreshold,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (!double.IsFinite(anomalyScoreThreshold) ||
            anomalyScoreThreshold <= 0 ||
            anomalyScoreThreshold >= 100)
        {
            return MultivariateDetectionResult.NaoAplicavel(
                reading,
                "O threshold multivariado deve estar no intervalo (0, 100).");
        }

        var model =
            await _persistenceService.CarregarAsync(
                reading.ClienteId,
                reading.GroupName,
                cancellationToken);

        if (model is null)
        {
            return MultivariateDetectionResult.NaoAplicavel(
                reading,
                $"Nenhum modelo multivariado treinado foi " +
                $"encontrado para o grupo '{reading.GroupName}'.");
        }

        var availableIndexes =
            ObterIndicesDisponiveis(
                reading,
                model);

        var minimumRequired =
            Math.Max(
                2,
                model.MinimumAvailableTags);

        if (availableIndexes.Count < minimumRequired)
        {
            return MultivariateDetectionResult.NaoAplicavel(
                reading,
                $"O grupo possui {availableIndexes.Count} tag(s) " +
                $"disponível(is), mas o modelo exige pelo menos " +
                $"{minimumRequired}.");
        }

        var values =
            new double[availableIndexes.Count];

        var means =
            new double[availableIndexes.Count];

        var covariance =
            new double[
                availableIndexes.Count,
                availableIndexes.Count];

        var usedTags =
            new List<string>(
                availableIndexes.Count);

        for (var i = 0;
             i < availableIndexes.Count;
             i++)
        {
            var modelIndex =
                availableIndexes[i];

            var tagName =
                model.Tags[modelIndex];

            values[i] =
                reading.Valores[tagName];

            means[i] =
                model.Means[modelIndex];

            usedTags.Add(tagName);

            for (var j = 0;
                 j < availableIndexes.Count;
                 j++)
            {
                covariance[i, j] =
                    model.CovarianceMatrix[
                        modelIndex][
                        availableIndexes[j]];
            }
        }

        if (!TryRegularizeAndInvert(
                covariance,
                out var inverseCovariance,
                out var ridgeAplicado,
                out var tentativasRegularizacao))
        {
            return MultivariateDetectionResult.NaoAplicavel(
                reading,
                "Não foi possível estabilizar/inverter a matriz de " +
                "covariância do grupo após regularização adaptativa.");
        }

        var delta =
            new double[values.Length];

        for (var i = 0;
             i < values.Length;
             i++)
        {
            delta[i] =
                values[i] - means[i];
        }

        var mahalanobisSquared =
            QuadraticForm(
                delta,
                inverseCovariance);

        if (!double.IsFinite(mahalanobisSquared))
        {
            return MultivariateDetectionResult.NaoAplicavel(
                reading,
                "A distância de Mahalanobis resultou em valor numérico inválido.");
        }

        mahalanobisSquared =
            Math.Max(
                0,
                mahalanobisSquared);

        var normalizedScore =
            NormalizarScore(
                mahalanobisSquared,
                values.Length);

        var threshold =
            anomalyScoreThreshold;

        var isAnomaly =
            normalizedScore >= threshold;

        var contributions =
            CalcularContribuicoes(
                values,
                means,
                usedTags,
                model,
                availableIndexes);

        var explanation =
            CriarExplicacao(
                reading,
                normalizedScore,
                threshold,
                mahalanobisSquared,
                usedTags,
                contributions,
                isAnomaly);

        return new MultivariateDetectionResult
        {
            Aplicavel = true,
            EhAnomalia = isAnomaly,
            ClienteId = reading.ClienteId,
            GroupName = reading.GroupName,
            MinuteIndex = reading.MinuteIndex,
            ScoreNormalizado = normalizedScore,
            LimiteScore = threshold,
            MahalanobisSquared = mahalanobisSquared,
            TagsUtilizadas = usedTags,
            Contribuicoes = contributions,
            Explicacao = explanation
        };
    }

    private static List<int> ObterIndicesDisponiveis(
        BufferedGroupReading reading,
        MultivariateGroupModel model)
    {
        var indexes = new List<int>();

        for (var index = 0;
             index < model.Tags.Count;
             index++)
        {
            var tagName =
                model.Tags[index];

            if (reading.Valores.TryGetValue(
                    tagName,
                    out var value) &&
                double.IsFinite(value))
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    private static bool TryRegularizeAndInvert(
        double[,] original,
        out double[,] inverse,
        out double ridgeApplied,
        out int attempts)
    {
        var dimension = original.GetLength(0);
        inverse = new double[dimension, dimension];
        ridgeApplied = 0;
        attempts = 0;

        var averageVariance = 0.0;
        for (var i = 0; i < dimension; i++)
            averageVariance += Math.Max(0, original[i, i]);

        averageVariance =
            dimension > 0 ? averageVariance / dimension : 0;

        var scale = Math.Max(averageVariance, 1e-9);

        // Tenta de 1e-9 até 1e-2 da variância média.
        for (var exponent = -9; exponent <= -2; exponent++)
        {
            attempts++;

            var ridge =
                Math.Max(1e-12, scale * Math.Pow(10, exponent));

            var candidate =
                (double[,])original.Clone();

            for (var i = 0; i < dimension; i++)
                candidate[i, i] += ridge;

            if (TryInvertMatrix(candidate, out inverse) &&
                MatrixIsFinite(inverse))
            {
                ridgeApplied = ridge;
                return true;
            }
        }

        return false;
    }

    private static bool MatrixIsFinite(double[,] matrix)
    {
        for (var row = 0; row < matrix.GetLength(0); row++)
        for (var column = 0; column < matrix.GetLength(1); column++)
        {
            if (!double.IsFinite(matrix[row, column]))
                return false;
        }

        return true;
    }

    private static double QuadraticForm(
        double[] vector,
        double[,] matrix)
    {
        var dimension =
            vector.Length;

        var result = 0.0;

        for (var row = 0;
             row < dimension;
             row++)
        {
            var rowValue = 0.0;

            for (var column = 0;
                 column < dimension;
                 column++)
            {
                rowValue +=
                    matrix[row, column] *
                    vector[column];
            }

            result +=
                vector[row] *
                rowValue;
        }

        return result;
    }

    private static double NormalizarScore(
        double mahalanobisSquared,
        int dimension)
    {
        /*
         * Mapeamento suave de 0..infinito para 0..100.
         * A divisão pela dimensão evita favorecer grupos maiores.
         */
        var adjusted =
            mahalanobisSquared /
            Math.Max(
                1,
                dimension);

        var score =
            100.0 *
            (1.0 - Math.Exp(
                -adjusted / 2.0));

        return Math.Clamp(
            score,
            0,
            100);
    }

    private static List<MultivariateTagContribution>
        CalcularContribuicoes(
            IReadOnlyList<double> values,
            IReadOnlyList<double> means,
            IReadOnlyList<string> usedTags,
            MultivariateGroupModel model,
            IReadOnlyList<int> availableIndexes)
    {
        var contributions =
            new List<MultivariateTagContribution>(
                usedTags.Count);

        for (var index = 0;
             index < usedTags.Count;
             index++)
        {
            var modelIndex =
                availableIndexes[index];

            var standardDeviation =
                model.StandardDeviations[modelIndex];

            var zScore =
                standardDeviation > 1e-12
                    ? (values[index] - means[index]) /
                      standardDeviation
                    : 0;

            contributions.Add(
                new MultivariateTagContribution
                {
                    TagName = usedTags[index],
                    ValorAtual = values[index],
                    MediaTreinamento = means[index],
                    DesvioPadraoTreinamento =
                        standardDeviation,
                    ZScoreAbsoluto =
                        Math.Abs(zScore)
                });
        }

        return contributions
            .OrderByDescending(
                item => item.ZScoreAbsoluto)
            .ToList();
    }

    private static string CriarExplicacao(
        BufferedGroupReading reading,
        double score,
        double threshold,
        double mahalanobisSquared,
        IReadOnlyList<string> usedTags,
        IReadOnlyList<MultivariateTagContribution>
            contributions,
        bool isAnomaly)
    {
        var topContributors =
            contributions
                .Take(3)
                .Select(item =>
                    $"{item.TagName} " +
                    $"(|z|={item.ZScoreAbsoluto:F2})");

        return
            $"Análise multivariada do grupo " +
            $"'{reading.GroupName}' com " +
            $"{usedTags.Count} tag(s). " +
            $"Distância de Mahalanobis²: " +
            $"{mahalanobisSquared:F2}. " +
            $"Score: {score:F2}/100; " +
            $"limite: {threshold:F2}. " +
            $"Resultado: " +
            $"{(isAnomaly ? "anomalia" : "normal")}. " +
            $"Maiores desvios individuais: " +
            $"{string.Join(", ", topContributors)}.";
    }

    private static bool TryInvertMatrix(
        double[,] source,
        out double[,] inverse)
    {
        var dimension =
            source.GetLength(0);

        inverse =
            new double[
                dimension,
                dimension];

        if (dimension == 0 ||
            source.GetLength(1) != dimension)
        {
            return false;
        }

        var augmented =
            new double[
                dimension,
                dimension * 2];

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = 0;
                 column < dimension;
                 column++)
            {
                augmented[row, column] =
                    source[row, column];
            }

            augmented[
                row,
                dimension + row] = 1.0;
        }

        for (var pivot = 0;
             pivot < dimension;
             pivot++)
        {
            var bestRow = pivot;
            var bestValue =
                Math.Abs(
                    augmented[pivot, pivot]);

            for (var row = pivot + 1;
                 row < dimension;
                 row++)
            {
                var candidate =
                    Math.Abs(
                        augmented[row, pivot]);

                if (candidate > bestValue)
                {
                    bestValue = candidate;
                    bestRow = row;
                }
            }

            if (bestValue < 1e-15)
            {
                return false;
            }

            if (bestRow != pivot)
            {
                TrocarLinhas(
                    augmented,
                    pivot,
                    bestRow);
            }

            var pivotValue =
                augmented[pivot, pivot];

            for (var column = 0;
                 column < dimension * 2;
                 column++)
            {
                augmented[pivot, column] /=
                    pivotValue;
            }

            for (var row = 0;
                 row < dimension;
                 row++)
            {
                if (row == pivot)
                {
                    continue;
                }

                var factor =
                    augmented[row, pivot];

                for (var column = 0;
                     column < dimension * 2;
                     column++)
                {
                    augmented[row, column] -=
                        factor *
                        augmented[pivot, column];
                }
            }
        }

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = 0;
                 column < dimension;
                 column++)
            {
                inverse[row, column] =
                    augmented[
                        row,
                        dimension + column];
            }
        }

        return true;
    }

    private static void TrocarLinhas(
        double[,] matrix,
        int firstRow,
        int secondRow)
    {
        var columns =
            matrix.GetLength(1);

        for (var column = 0;
             column < columns;
             column++)
        {
            (
                matrix[firstRow, column],
                matrix[secondRow, column]
            ) =
            (
                matrix[secondRow, column],
                matrix[firstRow, column]
            );
        }
    }
}

public sealed class MultivariateDetectionResult
{
    public bool Aplicavel { get; set; }

    public bool EhAnomalia { get; set; }

    public string ClienteId { get; set; } = "";

    public string GroupName { get; set; } = "";

    public long MinuteIndex { get; set; }

    public double ScoreNormalizado { get; set; }

    public double LimiteScore { get; set; }

    public double MahalanobisSquared { get; set; }

    public List<string> TagsUtilizadas { get; set; } = [];

    public List<MultivariateTagContribution>
        Contribuicoes { get; set; } = [];

    public string Explicacao { get; set; } = "";

    public static MultivariateDetectionResult NaoAplicavel(
        BufferedGroupReading reading,
        string explanation)
    {
        return new MultivariateDetectionResult
        {
            Aplicavel = false,
            EhAnomalia = false,
            ClienteId = reading.ClienteId,
            GroupName = reading.GroupName,
            MinuteIndex = reading.MinuteIndex,
            ScoreNormalizado = 0,
            LimiteScore = 0,
            MahalanobisSquared = 0,
            TagsUtilizadas = [],
            Contribuicoes = [],
            Explicacao = explanation
        };
    }
}

public sealed class MultivariateTagContribution
{
    public string TagName { get; set; } = "";

    public double ValorAtual { get; set; }

    public double MediaTreinamento { get; set; }

    public double DesvioPadraoTreinamento { get; set; }

    public double ZScoreAbsoluto { get; set; }
}