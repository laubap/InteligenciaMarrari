
namespace IAApi.Services.AnomalyDetection.Multivariate;
/// <summary>
/// B3.24.2 / MV08.4A
///
/// Avalia se um grupo multivariado possui evidência técnica suficiente
/// para ser CANDIDATO a aprovação operacional explícita.
///
/// IMPORTANTE:
/// - NÃO habilita promoção;
/// - NÃO altera ApprovedGroups;
/// - NÃO altera readiness;
/// - NÃO altera PredictionService;
/// - NÃO interpreta taxa de anomalia como falso-positivo;
/// - NÃO substitui validação industrial/humana.
///
/// O resultado diferencia:
/// 1. critérios técnicos;
/// 2. validação industrial explícita;
/// 3. elegibilidade para uma futura aprovação manual.
/// </summary>
public sealed class MultivariateOperationalQualificationEvaluator
{
    public MultivariateOperationalQualificationResult Evaluate(
        MultivariateOperationalQualificationContext context,
        MultivariateOperationalQualificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var effectiveOptions =
            options ??
            new MultivariateOperationalQualificationOptions();

        ValidateOptions(
            effectiveOptions);

        var technicalBlockers =
            new List<string>();

        var governanceBlockers =
            new List<string>();

        var warnings =
            new List<string>();

        var model =
            context.Model;

        if (model is null)
        {
            technicalBlockers.Add(
                "ModeloAusente");

            return BuildResult(
                context,
                effectiveOptions,
                technicalBlockers,
                governanceBlockers,
                warnings,
                maxAbsCorrelation: null,
                referenceApplicableRate: null,
                holdoutApplicableRate: null,
                anomalyRateAbsoluteDrift: null,
                p99ScoreAbsoluteDrift: null,
                trainingSamplesPerDimension: null);
        }

        var dimension =
            model.Tags?
                .Where(tag =>
                    !string.IsNullOrWhiteSpace(tag))
                .Select(tag =>
                    tag.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count() ??
            0;

        if (dimension < 2)
        {
            technicalBlockers.Add(
                "ModeloComMenosDeDuasTags");
        }

        double? trainingSamplesPerDimension =
            dimension > 0
                ? model.SampleCount /
                  (double)dimension
                : null;

        if (trainingSamplesPerDimension.HasValue &&
            trainingSamplesPerDimension.Value <
                effectiveOptions
                    .MinimumTrainingSamplesPerDimension)
        {
            technicalBlockers.Add(
                "AmostrasTreinamentoPorDimensaoInsuficientes");
        }

        if (model.TrainedAtUtc == default)
        {
            technicalBlockers.Add(
                "DataTreinamentoAusente");
        }

        var maxAbsCorrelation =
            TryGetMaximumAbsoluteOffDiagonalCorrelation(
                model);

        if (maxAbsCorrelation.HasValue &&
            maxAbsCorrelation.Value <
                effectiveOptions
                    .WeakLinearRelationshipWarningBelow)
        {
            warnings.Add(
                "RelacaoLinearHistoricaFraca");
        }

        var reference =
            context.ReferenceBacktest;

        var holdout =
            context.HoldoutBacktest;

        double? referenceApplicableRate =
            null;

        double? holdoutApplicableRate =
            null;

        double? anomalyRateAbsoluteDrift =
            null;

        double? p99ScoreAbsoluteDrift =
            null;

        if (reference is null)
        {
            technicalBlockers.Add(
                "BacktestReferenciaAusente");
        }
        else
        {
            ValidateSummary(
                reference,
                "Referencia",
                effectiveOptions.MinimumReferenceApplicableResults,
                effectiveOptions.MinimumApplicableRate,
                technicalBlockers,
                out referenceApplicableRate);
        }

        if (holdout is null)
        {
            technicalBlockers.Add(
                "BacktestHoldoutAusente");
        }
        else
        {
            ValidateSummary(
                holdout,
                "Holdout",
                effectiveOptions.MinimumHoldoutApplicableResults,
                effectiveOptions.MinimumApplicableRate,
                technicalBlockers,
                out holdoutApplicableRate);

            if (holdout.AnomalyRate >
                effectiveOptions.MaximumHoldoutAnomalyRate)
            {
                technicalBlockers.Add(
                    "TaxaAtivacaoHoldoutAcimaDoLimite");
            }

            if (holdout.LongestConsecutiveAnomalyRunMinutes >
                effectiveOptions
                    .MaximumConsecutiveAnomalyRunMinutes)
            {
                technicalBlockers.Add(
                    "SequenciaAnomalaHoldoutAcimaDoLimite");
            }
        }

        if (reference is not null &&
            holdout is not null)
        {
            anomalyRateAbsoluteDrift =
                Math.Abs(
                    holdout.AnomalyRate -
                    reference.AnomalyRate);

            if (anomalyRateAbsoluteDrift >
                effectiveOptions
                    .MaximumAbsoluteAnomalyRateDrift)
            {
                technicalBlockers.Add(
                    "DerivaTaxaAtivacaoAcimaDoLimite");
            }

            p99ScoreAbsoluteDrift =
                Math.Abs(
                    holdout.ScoreDistribution.P99 -
                    reference.ScoreDistribution.P99);

            if (!double.IsFinite(
                    p99ScoreAbsoluteDrift.Value) ||
                p99ScoreAbsoluteDrift.Value >
                    effectiveOptions
                        .MaximumAbsoluteP99ScoreDrift)
            {
                technicalBlockers.Add(
                    "DerivaP99ScoreAcimaDoLimite");
            }
        }

        ValidateHoldoutTemporalIndependence(
            context,
            model,
            technicalBlockers);

        if (effectiveOptions.RequireIndustrialValidation)
        {
            if (!context.IndustrialValidationConfirmed)
            {
                governanceBlockers.Add(
                    "ValidacaoIndustrialNaoConfirmada");
            }
            else if (string.IsNullOrWhiteSpace(
                         context.IndustrialValidationReference))
            {
                governanceBlockers.Add(
                    "ReferenciaValidacaoIndustrialAusente");
            }
        }

        /*
         * Correlação fraca NÃO é blocker automático.
         *
         * Um grupo pode carregar evidência conjunta sem relação linear
         * forte. Porém, isso precisa ficar visível para que ninguém
         * interprete Mahalanobis alta como "quebra de relação" quando
         * o modelo não sustenta essa afirmação.
         */

        return BuildResult(
            context,
            effectiveOptions,
            technicalBlockers,
            governanceBlockers,
            warnings,
            maxAbsCorrelation,
            referenceApplicableRate,
            holdoutApplicableRate,
            anomalyRateAbsoluteDrift,
            p99ScoreAbsoluteDrift,
            trainingSamplesPerDimension);
    }

    private static void ValidateSummary(
        MultivariateHistoricalBacktestSummary summary,
        string suffix,
        int minimumApplicableResults,
        double minimumApplicableRate,
        List<string> technicalBlockers,
        out double? applicableRate)
    {
        if (summary.TotalResults <= 0)
        {
            applicableRate =
                0;

            technicalBlockers.Add(
                $"Backtest{suffix}SemResultados");

            return;
        }

        applicableRate =
            summary.ApplicableResults /
            (double)summary.TotalResults;

        if (summary.ApplicableResults <
            minimumApplicableResults)
        {
            technicalBlockers.Add(
                $"Backtest{suffix}ComPoucosResultadosAplicaveis");
        }

        if (applicableRate <
            minimumApplicableRate)
        {
            technicalBlockers.Add(
                $"CoberturaAplicavel{suffix}AbaixoDoLimite");
        }

        if (summary.NotApplicableResults < 0 ||
            summary.ApplicableResults < 0 ||
            summary.AnomalyCount < 0 ||
            summary.NormalCount < 0)
        {
            technicalBlockers.Add(
                $"Backtest{suffix}ComContadoresInvalidos");
        }

        if (!double.IsFinite(
                summary.AnomalyRate) ||
            summary.AnomalyRate < 0 ||
            summary.AnomalyRate > 1)
        {
            technicalBlockers.Add(
                $"TaxaAtivacao{suffix}Invalida");
        }

        if (!double.IsFinite(
                summary.ScoreDistribution.P99))
        {
            technicalBlockers.Add(
                $"P99Score{suffix}Invalido");
        }
    }

    private static void ValidateHoldoutTemporalIndependence(
        MultivariateOperationalQualificationContext context,
        MultivariateGroupModel model,
        List<string> technicalBlockers)
    {
        if (context.HoldoutStartUtc == default)
        {
            technicalBlockers.Add(
                "InicioHoldoutAusente");

            return;
        }

        if (context.HoldoutEndUtc != default &&
            context.HoldoutEndUtc <=
                context.HoldoutStartUtc)
        {
            technicalBlockers.Add(
                "JanelaHoldoutInvalida");
        }

        if (model.TrainedAtUtc == default)
        {
            return;
        }

        var trainedAtUtc =
            NormalizeUtc(
                model.TrainedAtUtc);

        var holdoutStartUtc =
            NormalizeUtc(
                context.HoldoutStartUtc);

        if (holdoutStartUtc <=
            trainedAtUtc)
        {
            technicalBlockers.Add(
                "HoldoutNaoPosteriorAoTreinamento");
        }
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }

    private static double?
        TryGetMaximumAbsoluteOffDiagonalCorrelation(
            MultivariateGroupModel model)
    {
        var dimension =
            model.Tags?.Count ??
            0;

        var matrix =
            model.CorrelationMatrix;

        if (dimension < 2 ||
            matrix is null ||
            matrix.Count != dimension ||
            matrix.Any(row =>
                row is null ||
                row.Count != dimension))
        {
            return null;
        }

        double? maximum =
            null;

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

                if (!double.IsFinite(
                        value))
                {
                    continue;
                }

                var absolute =
                    Math.Abs(
                        value);

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

    private static MultivariateOperationalQualificationResult
        BuildResult(
            MultivariateOperationalQualificationContext context,
            MultivariateOperationalQualificationOptions options,
            IReadOnlyCollection<string> technicalBlockers,
            IReadOnlyCollection<string> governanceBlockers,
            IReadOnlyCollection<string> warnings,
            double? maxAbsCorrelation,
            double? referenceApplicableRate,
            double? holdoutApplicableRate,
            double? anomalyRateAbsoluteDrift,
            double? p99ScoreAbsoluteDrift,
            double? trainingSamplesPerDimension)
    {
        var uniqueTechnicalBlockers =
            technicalBlockers
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var uniqueGovernanceBlockers =
            governanceBlockers
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var uniqueWarnings =
            warnings
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var technicalCriteriaPassed =
            uniqueTechnicalBlockers.Length ==
            0;

        var eligibleForExplicitApproval =
            technicalCriteriaPassed &&
            uniqueGovernanceBlockers.Length ==
            0;

        var status =
            !technicalCriteriaPassed
                ? MultivariateOperationalQualificationStatuses
                    .NotTechnicallyQualified
                : eligibleForExplicitApproval
                    ? MultivariateOperationalQualificationStatuses
                        .QualifiedForExplicitApproval
                    : MultivariateOperationalQualificationStatuses
                        .TechnicallyQualifiedPendingIndustrialValidation;

        return new MultivariateOperationalQualificationResult
        {
            ClienteId =
                context.Model?.ClienteId?.Trim() ??
                "",

            GroupName =
                context.Model?.GroupName?.Trim() ??
                "",

            Status =
                status,

            TechnicalCriteriaPassed =
                technicalCriteriaPassed,

            IndustrialValidationConfirmed =
                context.IndustrialValidationConfirmed,

            EligibleForExplicitApproval =
                eligibleForExplicitApproval,

            TrainingSamplesPerDimension =
                trainingSamplesPerDimension,

            ReferenceApplicableRate =
                referenceApplicableRate,

            HoldoutApplicableRate =
                holdoutApplicableRate,

            ReferenceAnomalyRate =
                context.ReferenceBacktest?.AnomalyRate,

            HoldoutAnomalyRate =
                context.HoldoutBacktest?.AnomalyRate,

            AbsoluteAnomalyRateDrift =
                anomalyRateAbsoluteDrift,

            ReferenceP99Score =
                context.ReferenceBacktest?
                    .ScoreDistribution.P99,

            HoldoutP99Score =
                context.HoldoutBacktest?
                    .ScoreDistribution.P99,

            AbsoluteP99ScoreDrift =
                p99ScoreAbsoluteDrift,

            MaximumAbsoluteLinearCorrelation =
                maxAbsCorrelation,

            TechnicalBlockers =
                uniqueTechnicalBlockers,

            GovernanceBlockers =
                uniqueGovernanceBlockers,

            Warnings =
                uniqueWarnings,

            Criteria =
                new MultivariateOperationalQualificationCriteriaSnapshot
                {
                    MinimumTrainingSamplesPerDimension =
                        options.MinimumTrainingSamplesPerDimension,

                    MinimumReferenceApplicableResults =
                        options.MinimumReferenceApplicableResults,

                    MinimumHoldoutApplicableResults =
                        options.MinimumHoldoutApplicableResults,

                    MinimumApplicableRate =
                        options.MinimumApplicableRate,

                    MaximumHoldoutAnomalyRate =
                        options.MaximumHoldoutAnomalyRate,

                    MaximumAbsoluteAnomalyRateDrift =
                        options.MaximumAbsoluteAnomalyRateDrift,

                    MaximumAbsoluteP99ScoreDrift =
                        options.MaximumAbsoluteP99ScoreDrift,

                    MaximumConsecutiveAnomalyRunMinutes =
                        options.MaximumConsecutiveAnomalyRunMinutes,

                    WeakLinearRelationshipWarningBelow =
                        options.WeakLinearRelationshipWarningBelow,

                    RequireIndustrialValidation =
                        options.RequireIndustrialValidation
                }
        };
    }

    private static void ValidateOptions(
        MultivariateOperationalQualificationOptions options)
    {
        if (options.MinimumTrainingSamplesPerDimension <
            2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.MinimumTrainingSamplesPerDimension));
        }

        if (options.MinimumReferenceApplicableResults <
                1 ||
            options.MinimumHoldoutApplicableResults <
                1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.MinimumReferenceApplicableResults));
        }

        if (!IsProbability(
                options.MinimumApplicableRate) ||
            options.MinimumApplicableRate <=
                0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.MinimumApplicableRate));
        }

        if (!IsProbability(
                options.MaximumHoldoutAnomalyRate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.MaximumHoldoutAnomalyRate));
        }

        if (!IsProbability(
                options.MaximumAbsoluteAnomalyRateDrift))
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.MaximumAbsoluteAnomalyRateDrift));
        }

        if (!double.IsFinite(
                options.MaximumAbsoluteP99ScoreDrift) ||
            options.MaximumAbsoluteP99ScoreDrift <
                0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.MaximumAbsoluteP99ScoreDrift));
        }

        if (options.MaximumConsecutiveAnomalyRunMinutes <
            1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.MaximumConsecutiveAnomalyRunMinutes));
        }

        if (!IsProbability(
                options.WeakLinearRelationshipWarningBelow))
        {
            throw new ArgumentOutOfRangeException(
                nameof(
                    options.WeakLinearRelationshipWarningBelow));
        }
    }

    private static bool IsProbability(
        double value)
    {
        return
            double.IsFinite(value) &&
            value >= 0 &&
            value <= 1;
    }
}

public sealed class MultivariateOperationalQualificationOptions
{
    /// <summary>
    /// Evita aprovar modelo com poucas observações em relação à dimensão.
    /// Default: 100 amostras por tag/dimensão.
    /// </summary>
    public int MinimumTrainingSamplesPerDimension { get; init; } =
        100;

    /// <summary>
    /// Janela de referência deve possuir volume suficiente.
    /// </summary>
    public int MinimumReferenceApplicableResults { get; init; } =
        1000;

    /// <summary>
    /// Holdout pós-treinamento deve possuir volume mínimo independente.
    /// </summary>
    public int MinimumHoldoutApplicableResults { get; init; } =
        500;

    /// <summary>
    /// Percentual mínimo de resultados efetivamente avaliáveis.
    /// </summary>
    public double MinimumApplicableRate { get; init; } =
        0.95;

    /// <summary>
    /// Limite conservador de taxa de ativação no holdout.
    ///
    /// NÃO significa taxa de falso-positivo porque não há ground truth.
    /// </summary>
    public double MaximumHoldoutAnomalyRate { get; init; } =
        0.01;

    /// <summary>
    /// Máxima diferença absoluta aceitável entre a taxa de ativação
    /// da referência e do holdout.
    /// </summary>
    public double MaximumAbsoluteAnomalyRateDrift { get; init; } =
        0.01;

    /// <summary>
    /// Máxima diferença absoluta em pontos de score entre P99
    /// da referência e do holdout.
    /// </summary>
    public double MaximumAbsoluteP99ScoreDrift { get; init; } =
        10.0;

    /// <summary>
    /// Evita aprovar comportamento que dispara longas sequências
    /// contínuas sem investigação.
    /// </summary>
    public int MaximumConsecutiveAnomalyRunMinutes { get; init; } =
        5;

    /// <summary>
    /// Correlação abaixo disso gera warning, não blocker.
    /// </summary>
    public double WeakLinearRelationshipWarningBelow { get; init; } =
        0.30;

    /// <summary>
    /// Mesmo que os critérios estatísticos passem, exige evidência
    /// industrial/humana antes de declarar o grupo elegível.
    /// </summary>
    public bool RequireIndustrialValidation { get; init; } =
        true;
}

public sealed class MultivariateOperationalQualificationContext
{
    public MultivariateGroupModel? Model { get; init; }

    public MultivariateHistoricalBacktestSummary?
        ReferenceBacktest { get; init; }

    public MultivariateHistoricalBacktestSummary?
        HoldoutBacktest { get; init; }

    public DateTime HoldoutStartUtc { get; init; }

    public DateTime HoldoutEndUtc { get; init; }

    /// <summary>
    /// Confirmação explícita de que especialistas/contexto operacional
    /// revisaram o comportamento do grupo.
    /// </summary>
    public bool IndustrialValidationConfirmed { get; init; }

    /// <summary>
    /// Referência auditável: chamado, documento, evidência, protocolo,
    /// relatório ou identificador equivalente.
    /// </summary>
    public string? IndustrialValidationReference { get; init; }
}

public static class MultivariateOperationalQualificationStatuses
{
    public const string NotTechnicallyQualified =
        "NotTechnicallyQualified";

    public const string TechnicallyQualifiedPendingIndustrialValidation =
        "TechnicallyQualifiedPendingIndustrialValidation";

    public const string QualifiedForExplicitApproval =
        "QualifiedForExplicitApproval";
}

public sealed class MultivariateOperationalQualificationResult
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public string Status { get; init; } =
        MultivariateOperationalQualificationStatuses
            .NotTechnicallyQualified;

    public bool TechnicalCriteriaPassed { get; init; }

    public bool IndustrialValidationConfirmed { get; init; }

    public bool EligibleForExplicitApproval { get; init; }

    public double? TrainingSamplesPerDimension { get; init; }

    public double? ReferenceApplicableRate { get; init; }

    public double? HoldoutApplicableRate { get; init; }

    public double? ReferenceAnomalyRate { get; init; }

    public double? HoldoutAnomalyRate { get; init; }

    public double? AbsoluteAnomalyRateDrift { get; init; }

    public double? ReferenceP99Score { get; init; }

    public double? HoldoutP99Score { get; init; }

    public double? AbsoluteP99ScoreDrift { get; init; }

    public double? MaximumAbsoluteLinearCorrelation { get; init; }

    public IReadOnlyList<string> TechnicalBlockers { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> GovernanceBlockers { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } =
        Array.Empty<string>();

    public MultivariateOperationalQualificationCriteriaSnapshot
        Criteria { get; init; } =
            new();
}

public sealed class MultivariateOperationalQualificationCriteriaSnapshot
{
    public int MinimumTrainingSamplesPerDimension { get; init; }

    public int MinimumReferenceApplicableResults { get; init; }

    public int MinimumHoldoutApplicableResults { get; init; }

    public double MinimumApplicableRate { get; init; }

    public double MaximumHoldoutAnomalyRate { get; init; }

    public double MaximumAbsoluteAnomalyRateDrift { get; init; }

    public double MaximumAbsoluteP99ScoreDrift { get; init; }

    public int MaximumConsecutiveAnomalyRunMinutes { get; init; }

    public double WeakLinearRelationshipWarningBelow { get; init; }

    public bool RequireIndustrialValidation { get; init; }
}