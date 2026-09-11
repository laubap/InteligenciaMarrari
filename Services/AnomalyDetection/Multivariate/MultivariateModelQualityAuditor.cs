namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.1
///
/// Auditoria estrutural e estatística de um modelo multivariado.
///
/// IMPORTANTE:
/// - não aprova grupo para produção;
/// - não altera readiness;
/// - não altera RuntimeDecision;
/// - não altera threshold do detector;
/// - não persiste nada.
///
/// O objetivo desta etapa é produzir evidências objetivas que serão
/// usadas posteriormente na política de qualificação industrial.
/// </summary>
public sealed class MultivariateModelQualityAuditor
{
    private const double NumericTolerance = 1e-10;
    private const double SymmetryTolerance = 1e-8;

    public MultivariateModelQualityAudit Auditar(
        MultivariateGroupModel model,
        OperationalMultivariateTrainingResult? training = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var issues = new List<string>();
        var warnings = new List<string>();
        var notes = new List<string>();

        var tagCount =
            model.Tags?.Count ?? 0;

        ValidarEstrutura(
            model,
            tagCount,
            issues);

        var samplesPerDimension =
            tagCount > 0
                ? model.SampleCount / (double)tagCount
                : 0;

        if (tagCount > 0 &&
            samplesPerDimension < 20)
        {
            warnings.Add(
                "PoucasAmostrasPorDimensao");
        }

        var nearZeroVarianceTags =
            ObterTagsComVarianciaQuaseZero(
                model,
                tagCount);

        if (nearZeroVarianceTags.Count > 0)
        {
            warnings.Add(
                "TagComVarianciaQuaseZero");
        }

        var correlationMetrics =
            CalcularMetricasCorrelacao(
                model,
                tagCount,
                issues,
                warnings);

        double? trainingCompletionRate =
            null;

        double? trainingInvalidRate =
            null;

        int? minReadingsPerTag =
            null;

        int? maxReadingsPerTag =
            null;

        if (training is null)
        {
            warnings.Add(
                "MetadadosDeCoberturaDoTreinamentoIndisponiveis");

            notes.Add(
                "O modelo persistido atual não contém a janela de treinamento, " +
                "vetores finalizados/incompletos/inválidos nem leituras válidas por tag.");
        }
        else
        {
            AuditarTreinamento(
                model,
                training,
                issues,
                notes,
                out trainingCompletionRate,
                out trainingInvalidRate,
                out minReadingsPerTag,
                out maxReadingsPerTag);
        }

        return new MultivariateModelQualityAudit
        {
            ClienteId =
                model.ClienteId,

            GroupName =
                model.GroupName,

            ModelVersion =
                model.Version,

            Algorithm =
                model.Algorithm,

            TrainedAtUtc =
                model.TrainedAtUtc,

            TagCount =
                tagCount,

            SampleCount =
                model.SampleCount,

            SamplesPerDimension =
                samplesPerDimension,

            MaximumAbsoluteLinearCorrelation =
                correlationMetrics.MaximumAbsoluteCorrelation,

            MeanAbsoluteLinearCorrelation =
                correlationMetrics.MeanAbsoluteCorrelation,

            MostCorrelatedPair =
                correlationMetrics.MostCorrelatedPair,

            MinimumCorrelationEigenvalue =
                correlationMetrics.MinimumEigenvalue,

            MaximumCorrelationEigenvalue =
                correlationMetrics.MaximumEigenvalue,

            EstimatedCorrelationConditionNumber =
                correlationMetrics.EstimatedConditionNumber,

            NearZeroVarianceTags =
                nearZeroVarianceTags,

            TrainingCoverageAvailable =
                training is not null,

            TrainingFinalizedVectors =
                training?.VetoresFinalizados,

            TrainingCompleteVectors =
                training?.VetoresCompletosUsados,

            TrainingIncompleteVectors =
                training?.VetoresIncompletosIgnorados,

            TrainingInvalidVectors =
                training?.VetoresInvalidosIgnorados,

            TrainingCompletionRate =
                trainingCompletionRate,

            TrainingInvalidRate =
                trainingInvalidRate,

            MinimumValidReadingsPerTag =
                minReadingsPerTag,

            MaximumValidReadingsPerTag =
                maxReadingsPerTag,

            StructurallyValid =
                issues.Count == 0,

            StructuralIssues =
                issues
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),

            Warnings =
                warnings
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),

            Notes =
                notes
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
        };
    }

    private static void ValidarEstrutura(
        MultivariateGroupModel model,
        int dimension,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(
                model.ClienteId))
        {
            issues.Add(
                "ClienteIdAusente");
        }

        if (string.IsNullOrWhiteSpace(
                model.GroupName))
        {
            issues.Add(
                "NomeGrupoAusente");
        }

        if (model.Version <= 0)
        {
            issues.Add(
                "VersaoModeloInvalida");
        }

        if (string.IsNullOrWhiteSpace(
                model.Algorithm))
        {
            issues.Add(
                "AlgoritmoAusente");
        }

        if (model.TrainedAtUtc == default)
        {
            issues.Add(
                "DataTreinamentoAusente");
        }

        if (dimension < 2)
        {
            issues.Add(
                "ModeloComMenosDeDuasTags");

            return;
        }

        if (model.SampleCount < 2)
        {
            issues.Add(
                "SampleCountInvalido");
        }

        if (model.Means is null ||
            model.Means.Count != dimension)
        {
            issues.Add(
                "DimensaoMeansInvalida");
        }

        if (model.StandardDeviations is null ||
            model.StandardDeviations.Count != dimension)
        {
            issues.Add(
                "DimensaoStandardDeviationsInvalida");
        }

        if (!MatrizTemDimensao(
                model.CovarianceMatrix,
                dimension))
        {
            issues.Add(
                "DimensaoCovarianciaInvalida");
        }

        if (!MatrizTemDimensao(
                model.CorrelationMatrix,
                dimension))
        {
            issues.Add(
                "DimensaoCorrelacaoInvalida");
        }

        if (model.Means is not null &&
            model.Means.Any(
                value =>
                    !double.IsFinite(value)))
        {
            issues.Add(
                "MeansComValorInvalido");
        }

        if (model.StandardDeviations is not null &&
            model.StandardDeviations.Any(
                value =>
                    !double.IsFinite(value) ||
                    value < 0))
        {
            issues.Add(
                "DesvioPadraoInvalido");
        }

        if (MatrizTemDimensao(
                model.CovarianceMatrix,
                dimension))
        {
            if (model.CovarianceMatrix
                .SelectMany(row => row)
                .Any(value =>
                    !double.IsFinite(value)))
            {
                issues.Add(
                    "CovarianciaComValorInvalido");
            }

            var covarianceSymmetryError =
                CalcularErroMaximoSimetria(
                    model.CovarianceMatrix);

            if (covarianceSymmetryError >
                SymmetryTolerance)
            {
                issues.Add(
                    "CovarianciaNaoSimetrica");
            }

            for (var index = 0;
                 index < dimension;
                 index++)
            {
                if (model.CovarianceMatrix[index][index] <
                    -NumericTolerance)
                {
                    issues.Add(
                        "CovarianciaComVarianciaNegativa");

                    break;
                }
            }
        }
    }

    private static CorrelationMetrics
        CalcularMetricasCorrelacao(
            MultivariateGroupModel model,
            int dimension,
            List<string> issues,
            List<string> warnings)
    {
        if (!MatrizTemDimensao(
                model.CorrelationMatrix,
                dimension))
        {
            return CorrelationMetrics.Empty;
        }

        var matrix =
            model.CorrelationMatrix;

        if (matrix
            .SelectMany(row => row)
            .Any(value =>
                !double.IsFinite(value)))
        {
            issues.Add(
                "CorrelacaoComValorInvalido");

            return CorrelationMetrics.Empty;
        }

        var symmetryError =
            CalcularErroMaximoSimetria(
                matrix);

        if (symmetryError >
            SymmetryTolerance)
        {
            issues.Add(
                "CorrelacaoNaoSimetrica");
        }

        var correlations =
            new List<double>();

        MultivariateCorrelationPair?
            mostCorrelatedPair =
                null;

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = row + 1;
                 column < dimension;
                 column++)
            {
                var correlation =
                    matrix[row][column];

                var absolute =
                    Math.Abs(correlation);

                correlations.Add(
                    absolute);

                if (mostCorrelatedPair is null ||
                    absolute >
                    Math.Abs(
                        mostCorrelatedPair.Correlation))
                {
                    mostCorrelatedPair =
                        new MultivariateCorrelationPair
                        {
                            FirstTag =
                                model.Tags[row],

                            SecondTag =
                                model.Tags[column],

                            Correlation =
                                correlation
                        };
                }
            }
        }

        var maximum =
            correlations.Count > 0
                ? correlations.Max()
                : (double?)null;

        var mean =
            correlations.Count > 0
                ? correlations.Average()
                : (double?)null;

        if (maximum.HasValue &&
            maximum.Value < 0.30)
        {
            warnings.Add(
                "RelacaoLinearHistoricaFraca");
        }

        if (maximum.HasValue &&
            maximum.Value > 0.995)
        {
            warnings.Add(
                "PossivelColinearidadeForte");
        }

        double? minimumEigenvalue =
            null;

        double? maximumEigenvalue =
            null;

        double? conditionNumber =
            null;

        if (TryEstimateEigenvaluesSymmetric(
                matrix,
                out var eigenvalues))
        {
            minimumEigenvalue =
                eigenvalues.Min();

            maximumEigenvalue =
                eigenvalues.Max();

            if (minimumEigenvalue <
                -1e-8)
            {
                issues.Add(
                    "CorrelacaoNaoPositivaSemidefinida");
            }

            var positiveEigenvalues =
                eigenvalues
                    .Where(value =>
                        value > NumericTolerance)
                    .ToArray();

            if (positiveEigenvalues.Length <
                dimension)
            {
                conditionNumber =
                    double.PositiveInfinity;
            }
            else
            {
                conditionNumber =
                    positiveEigenvalues.Max() /
                    positiveEigenvalues.Min();
            }

            if (!double.IsFinite(
                    conditionNumber.Value) ||
                conditionNumber.Value > 1_000_000)
            {
                warnings.Add(
                    "MatrizCorrelacaoMalCondicionada");
            }
        }
        else
        {
            warnings.Add(
                "NaoFoiPossivelEstimarCondicionamento");
        }

        return new CorrelationMetrics(
            maximum,
            mean,
            mostCorrelatedPair,
            minimumEigenvalue,
            maximumEigenvalue,
            conditionNumber);
    }

    private static void AuditarTreinamento(
        MultivariateGroupModel model,
        OperationalMultivariateTrainingResult training,
        List<string> issues,
        List<string> notes,
        out double? completionRate,
        out double? invalidRate,
        out int? minReadingsPerTag,
        out int? maxReadingsPerTag)
    {
        completionRate =
            null;

        invalidRate =
            null;

        minReadingsPerTag =
            null;

        maxReadingsPerTag =
            null;

        if (!string.Equals(
                model.ClienteId?.Trim(),
                training.ClienteId?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                "ClienteModeloDivergeDoTreinamento");
        }

        if (!string.Equals(
                model.GroupName?.Trim(),
                training.GrupoNome?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                "GrupoModeloDivergeDoTreinamento");
        }

        var modelTags =
            new HashSet<string>(
                model.Tags ?? [],
                StringComparer.OrdinalIgnoreCase);

        var trainingTags =
            new HashSet<string>(
                training.Tags ?? [],
                StringComparer.OrdinalIgnoreCase);

        if (!modelTags.SetEquals(
                trainingTags))
        {
            issues.Add(
                "TagsModeloDivergemDoTreinamento");
        }

        if (training.VetoresCompletosUsados !=
            model.SampleCount)
        {
            issues.Add(
                "SampleCountDivergeDoResultadoTreinamento");
        }

        if (training.VetoresFinalizados > 0)
        {
            completionRate =
                training.VetoresCompletosUsados /
                (double)training.VetoresFinalizados;

            invalidRate =
                training.VetoresInvalidosIgnorados /
                (double)training.VetoresFinalizados;
        }

        if (training.LeiturasValidasPorTag is not null &&
            training.LeiturasValidasPorTag.Count > 0)
        {
            minReadingsPerTag =
                training.LeiturasValidasPorTag
                    .Values
                    .Min();

            maxReadingsPerTag =
                training.LeiturasValidasPorTag
                    .Values
                    .Max();
        }

        notes.Add(
            $"Janela observada no resultado de treinamento: " +
            $"{training.Inicio:O} até {training.Fim:O}.");
    }

    private static List<string>
        ObterTagsComVarianciaQuaseZero(
            MultivariateGroupModel model,
            int dimension)
    {
        if (model.StandardDeviations is null ||
            model.StandardDeviations.Count != dimension)
        {
            return [];
        }

        var result =
            new List<string>();

        for (var index = 0;
             index < dimension;
             index++)
        {
            var standardDeviation =
                model.StandardDeviations[index];

            if (double.IsFinite(
                    standardDeviation) &&
                standardDeviation <=
                NumericTolerance)
            {
                result.Add(
                    model.Tags[index]);
            }
        }

        return result;
    }

    private static bool MatrizTemDimensao(
        List<List<double>>? matrix,
        int dimension)
    {
        return matrix is not null &&
               matrix.Count == dimension &&
               matrix.All(row =>
                   row is not null &&
                   row.Count == dimension);
    }

    private static double
        CalcularErroMaximoSimetria(
            List<List<double>> matrix)
    {
        var dimension =
            matrix.Count;

        var maximum =
            0.0;

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = row + 1;
                 column < dimension;
                 column++)
            {
                maximum =
                    Math.Max(
                        maximum,
                        Math.Abs(
                            matrix[row][column] -
                            matrix[column][row]));
            }
        }

        return maximum;
    }

    /// <summary>
    /// Estima os autovalores de uma matriz simétrica usando Jacobi.
    /// É suficiente para diagnóstico de condicionamento; não participa
    /// da inferência e não substitui a regularização do detector.
    /// </summary>
    private static bool
        TryEstimateEigenvaluesSymmetric(
            List<List<double>> matrix,
            out double[] eigenvalues)
    {
        var dimension =
            matrix.Count;

        eigenvalues =
            Array.Empty<double>();

        if (dimension == 0 ||
            matrix.Any(row =>
                row.Count != dimension))
        {
            return false;
        }

        var values =
            new double[
                dimension,
                dimension];

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = 0;
                 column < dimension;
                 column++)
            {
                var a =
                    matrix[row][column];

                var b =
                    matrix[column][row];

                if (!double.IsFinite(a) ||
                    !double.IsFinite(b))
                {
                    return false;
                }

                values[row, column] =
                    (a + b) / 2.0;
            }
        }

        var maximumIterations =
            Math.Max(
                50,
                dimension *
                dimension *
                50);

        for (var iteration = 0;
             iteration < maximumIterations;
             iteration++)
        {
            var p = 0;
            var q = 0;
            var largest = 0.0;

            for (var row = 0;
                 row < dimension;
                 row++)
            {
                for (var column = row + 1;
                     column < dimension;
                     column++)
                {
                    var candidate =
                        Math.Abs(
                            values[row, column]);

                    if (candidate > largest)
                    {
                        largest =
                            candidate;

                        p =
                            row;

                        q =
                            column;
                    }
                }
            }

            if (largest < 1e-12)
            {
                break;
            }

            var app =
                values[p, p];

            var aqq =
                values[q, q];

            var apq =
                values[p, q];

            var angle =
                0.5 *
                Math.Atan2(
                    2.0 * apq,
                    aqq - app);

            var cosine =
                Math.Cos(angle);

            var sine =
                Math.Sin(angle);

            for (var k = 0;
                 k < dimension;
                 k++)
            {
                if (k == p ||
                    k == q)
                {
                    continue;
                }

                var akp =
                    values[k, p];

                var akq =
                    values[k, q];

                var newKp =
                    cosine * akp -
                    sine * akq;

                var newKq =
                    sine * akp +
                    cosine * akq;

                values[k, p] =
                    newKp;

                values[p, k] =
                    newKp;

                values[k, q] =
                    newKq;

                values[q, k] =
                    newKq;
            }

            values[p, p] =
                cosine * cosine * app -
                2.0 * sine * cosine * apq +
                sine * sine * aqq;

            values[q, q] =
                sine * sine * app +
                2.0 * sine * cosine * apq +
                cosine * cosine * aqq;

            values[p, q] =
                0;

            values[q, p] =
                0;
        }

        eigenvalues =
            Enumerable
                .Range(
                    0,
                    dimension)
                .Select(index =>
                    values[index, index])
                .ToArray();

        return eigenvalues.All(
            double.IsFinite);
    }

    private sealed record CorrelationMetrics(
        double? MaximumAbsoluteCorrelation,
        double? MeanAbsoluteCorrelation,
        MultivariateCorrelationPair? MostCorrelatedPair,
        double? MinimumEigenvalue,
        double? MaximumEigenvalue,
        double? EstimatedConditionNumber)
    {
        public static CorrelationMetrics Empty { get; } =
            new(
                null,
                null,
                null,
                null,
                null,
                null);
    }
}

public sealed class MultivariateModelQualityAudit
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public int ModelVersion { get; init; }

    public string Algorithm { get; init; } =
        "";

    public DateTime TrainedAtUtc { get; init; }

    public int TagCount { get; init; }

    public long SampleCount { get; init; }

    public double SamplesPerDimension { get; init; }

    public double? MaximumAbsoluteLinearCorrelation { get; init; }

    public double? MeanAbsoluteLinearCorrelation { get; init; }

    public MultivariateCorrelationPair? MostCorrelatedPair { get; init; }

    public double? MinimumCorrelationEigenvalue { get; init; }

    public double? MaximumCorrelationEigenvalue { get; init; }

    public double? EstimatedCorrelationConditionNumber { get; init; }

    public IReadOnlyList<string> NearZeroVarianceTags { get; init; } =
        Array.Empty<string>();

    public bool TrainingCoverageAvailable { get; init; }

    public long? TrainingFinalizedVectors { get; init; }

    public long? TrainingCompleteVectors { get; init; }

    public long? TrainingIncompleteVectors { get; init; }

    public long? TrainingInvalidVectors { get; init; }

    public double? TrainingCompletionRate { get; init; }

    public double? TrainingInvalidRate { get; init; }

    public int? MinimumValidReadingsPerTag { get; init; }

    public int? MaximumValidReadingsPerTag { get; init; }

    public bool StructurallyValid { get; init; }

    public IReadOnlyList<string> StructuralIssues { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } =
        Array.Empty<string>();
}

public sealed class MultivariateCorrelationPair
{
    public string FirstTag { get; init; } =
        "";

    public string SecondTag { get; init; } =
        "";

    public double Correlation { get; init; }
}
