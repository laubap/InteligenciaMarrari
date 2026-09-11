

/// <summary>
/// B3.24.2 / MV07.1
///
/// Avalia se um grupo/modelo multivariado está estruturalmente pronto
/// para receber autoridade operacional.
///
/// Esta camada usa um snapshot próprio para não acoplar a política
/// de rollout ao objeto concreto de persistência/treinamento.
/// </summary>
public sealed class MultivariateOperationalReadinessEvaluator
{
    public MultivariateOperationalReadinessResult Evaluate(
        MultivariateOperationalReadinessContext context,
        MultivariateOperationalReadinessOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var blockers = new List<string>();
        var warnings = new List<string>();

        var clienteId =
            context.ClienteId?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(clienteId))
        {
            blockers.Add("ClienteIdAusente");
        }

        if (!context.GroupIsActive)
        {
            blockers.Add("GrupoInativo");
        }

        var group = context.Group;

        if (group is null)
        {
            blockers.Add("GrupoAusente");

            return BuildResult(
                clienteId,
                groupName: "",
                model: null,
                approved: false,
                blockers,
                warnings,
                maxAbsCorrelation: null);
        }

        var groupName =
            group.Nome?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(groupName))
        {
            blockers.Add("NomeGrupoAusente");
        }

        var groupTags =
            NormalizeTags(group.Tags);

        if (groupTags.Count < 2)
        {
            blockers.Add("GrupoComMenosDeDuasTags");
        }

        var approved =
            IsExplicitlyApproved(
                clienteId,
                groupName,
                options.ApprovedGroups);

        if (options.RequireExplicitApproval &&
            !approved)
        {
            blockers.Add(
                "GrupoNaoAprovadoParaAutoridadeOperacional");
        }

        var model = context.Model;

        if (model is null)
        {
            blockers.Add("ModeloAusente");

            return BuildResult(
                clienteId,
                groupName,
                model,
                approved,
                blockers,
                warnings,
                maxAbsCorrelation: null);
        }

        if (!string.Equals(
                model.ClienteId?.Trim(),
                clienteId,
                StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(
                "ModeloClienteIncompativel");
        }

        if (!string.Equals(
                model.GroupName?.Trim(),
                groupName,
                StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(
                "ModeloGrupoIncompativel");
        }

        var modelTags =
            NormalizeTags(model.Tags);

        if (!SetsEqual(
                groupTags,
                modelTags))
        {
            blockers.Add(
                "TagsModeloDiferemDaConfiguracaoAtual");
        }

        if (model.MinimumAvailableTags !=
            group.MinimumAvailableTags)
        {
            blockers.Add(
                "MinimumAvailableTagsIncompativel");
        }

        if (model.MaxLagMinutes !=
            group.MaxLagMinutes)
        {
            blockers.Add(
                "MaxLagMinutesIncompativel");
        }

        var minimumTrainingSamples =
            Math.Max(
                2,
                options.MinimumTrainingSamples);

        if (model.SampleCount <
            minimumTrainingSamples)
        {
            blockers.Add(
                "AmostrasTreinamentoInsuficientes");
        }

        if (model.TrainedAtUtc == default)
        {
            blockers.Add(
                "DataTreinamentoAusente");
        }
        else
        {
            var nowUtc =
                context.NowUtc == default
                    ? DateTime.UtcNow
                    : context.NowUtc.ToUniversalTime();

            var trainedAtUtc =
                model.TrainedAtUtc.Kind ==
                DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(
                        model.TrainedAtUtc,
                        DateTimeKind.Utc)
                    : model.TrainedAtUtc.ToUniversalTime();

            var age =
                nowUtc - trainedAtUtc;

            if (age < TimeSpan.Zero)
            {
                blockers.Add(
                    "DataTreinamentoNoFuturo");
            }

            if (options.MaxModelAgeDays > 0 &&
                age >
                TimeSpan.FromDays(
                    options.MaxModelAgeDays))
            {
                blockers.Add(
                    "ModeloObsoleto");
            }
        }

        ValidateNumerics(
            model,
            blockers);

        var maxAbsCorrelation =
            TryGetMaximumAbsoluteOffDiagonalCorrelation(
                model);

        if (maxAbsCorrelation.HasValue &&
            maxAbsCorrelation.Value <
            options.WeakLinearRelationshipWarningBelow)
        {
            warnings.Add(
                "RelacaoLinearHistoricaFraca");
        }

        /*
         * Correlação fraca é apenas warning.
         *
         * A distância multivariada pode representar estrutura conjunta
         * mesmo sem correlação Pearson forte entre um par específico.
         * A autorização operacional continua explícita.
         */
        return BuildResult(
            clienteId,
            groupName,
            model,
            approved,
            blockers,
            warnings,
            maxAbsCorrelation);
    }

    private static void ValidateNumerics(
        MultivariateOperationalModelSnapshot model,
        List<string> blockers)
    {
        var dimension =
            model.Tags?.Count ?? 0;

        if (dimension < 2)
        {
            blockers.Add(
                "ModeloComMenosDeDuasTags");

            return;
        }

        if (model.Means is null ||
            model.StandardDeviations is null ||
            model.CovarianceMatrix is null ||
            model.Means.Count != dimension ||
            model.StandardDeviations.Count != dimension ||
            model.CovarianceMatrix.Count != dimension ||
            model.CovarianceMatrix.Any(
                row =>
                    row is null ||
                    row.Count != dimension))
        {
            blockers.Add(
                "DimensoesModeloInvalidas");

            return;
        }

        if (model.Means.Any(
                value => !double.IsFinite(value)) ||
            model.StandardDeviations.Any(
                value =>
                    !double.IsFinite(value) ||
                    value < 0) ||
            model.CovarianceMatrix
                .SelectMany(row => row)
                .Any(value =>
                    !double.IsFinite(value)))
        {
            blockers.Add(
                "ParametrosNumericosInvalidos");
        }
    }

    private static double?
        TryGetMaximumAbsoluteOffDiagonalCorrelation(
            MultivariateOperationalModelSnapshot model)
    {
        var matrix =
            model.CorrelationMatrix;

        var dimension =
            model.Tags?.Count ?? 0;

        if (matrix is null ||
            dimension < 2 ||
            matrix.Count != dimension ||
            matrix.Any(
                row =>
                    row is null ||
                    row.Count != dimension))
        {
            return null;
        }

        double? maximum = null;

        for (var row = 0;
             row < dimension;
             row++)
        {
            for (var column = row + 1;
                 column < dimension;
                 column++)
            {
                var value =
                    matrix[row][column];

                if (!double.IsFinite(value))
                {
                    continue;
                }

                var absolute =
                    Math.Abs(value);

                maximum =
                    maximum.HasValue
                        ? Math.Max(
                            maximum.Value,
                            absolute)
                        : absolute;
            }
        }

        return maximum;
    }

    private static List<string> NormalizeTags(
        IEnumerable<string>? tags)
    {
        return (tags ?? [])
            .Where(tag =>
                !string.IsNullOrWhiteSpace(tag))
            .Select(tag =>
                tag.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                tag => tag,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool SetsEqual(
        IReadOnlyCollection<string> left,
        IReadOnlyCollection<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        return new HashSet<string>(
            left,
            StringComparer.OrdinalIgnoreCase)
            .SetEquals(right);
    }

    private static bool IsExplicitlyApproved(
        string clienteId,
        string groupName,
        IReadOnlyCollection<string>? approvedGroups)
    {
        if (string.IsNullOrWhiteSpace(clienteId) ||
            string.IsNullOrWhiteSpace(groupName) ||
            approvedGroups is null ||
            approvedGroups.Count == 0)
        {
            return false;
        }

        var expected =
            $"{clienteId}::{groupName}";

        return approvedGroups.Any(
            item =>
                !string.IsNullOrWhiteSpace(item) &&
                string.Equals(
                    item.Trim(),
                    expected,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static MultivariateOperationalReadinessResult
        BuildResult(
            string clienteId,
            string groupName,
            MultivariateOperationalModelSnapshot? model,
            bool approved,
            IReadOnlyCollection<string> blockers,
            IReadOnlyCollection<string> warnings,
            double? maxAbsCorrelation)
    {
        var uniqueBlockers =
            blockers
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var uniqueWarnings =
            warnings
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new MultivariateOperationalReadinessResult
        {
            ClienteId =
                clienteId,

            GroupName =
                groupName,

            ReadyForOperationalAuthority =
                uniqueBlockers.Length == 0,

            ExplicitlyApproved =
                approved,

            ModelPresent =
                model is not null,

            ModelSampleCount =
                model?.SampleCount ?? 0,

            ModelTrainedAtUtc =
                model?.TrainedAtUtc,

            MaximumAbsoluteLinearCorrelation =
                maxAbsCorrelation,

            Blockers =
                uniqueBlockers,

            Warnings =
                uniqueWarnings
        };
    }
}

public sealed class MultivariateOperationalReadinessOptions
{
    public bool RequireExplicitApproval { get; init; } =
        true;

    public int MinimumTrainingSamples { get; init; } =
        100;

    public int MaxModelAgeDays { get; init; } =
        30;

    /// <summary>
    /// Formato CLIENTE::GRUPO.
    /// </summary>
    public string[] ApprovedGroups { get; init; } =
        [];

    /// <summary>
    /// Apenas warning; não bloqueia sozinho.
    /// </summary>
    public double WeakLinearRelationshipWarningBelow { get; init; } =
        0.30;
}

public sealed class MultivariateOperationalReadinessContext
{
    public string ClienteId { get; init; } =
        "";

    public bool GroupIsActive { get; init; } =
        true;

    public GrupoAnaliseDto? Group { get; init; }

    public MultivariateOperationalModelSnapshot? Model { get; init; }

    public DateTime NowUtc { get; init; } =
        DateTime.UtcNow;
}

/// <summary>
/// Snapshot mínimo necessário para a política de readiness.
/// Na integração futura ele será preenchido a partir do modelo
/// persistido, sem fazer o evaluator depender da classe concreta.
/// </summary>
public sealed class MultivariateOperationalModelSnapshot
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public DateTime TrainedAtUtc { get; init; }

    public long SampleCount { get; init; }

    public int MinimumAvailableTags { get; init; }

    public int MaxLagMinutes { get; init; }

    public List<string> Tags { get; init; } =
        [];

    public List<double> Means { get; init; } =
        [];

    public List<double> StandardDeviations { get; init; } =
        [];

    public List<List<double>> CovarianceMatrix { get; init; } =
        [];

    public List<List<double>> CorrelationMatrix { get; init; } =
        [];
}

public sealed class MultivariateOperationalReadinessResult
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public bool ReadyForOperationalAuthority { get; init; }

    public bool ExplicitlyApproved { get; init; }

    public bool ModelPresent { get; init; }

    public long ModelSampleCount { get; init; }

    public DateTime? ModelTrainedAtUtc { get; init; }

    public double? MaximumAbsoluteLinearCorrelation { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } =
        Array.Empty<string>();
}
