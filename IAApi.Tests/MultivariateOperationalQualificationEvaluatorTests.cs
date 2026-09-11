using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class MultivariateOperationalQualificationEvaluatorTests
{
    private readonly MultivariateOperationalQualificationEvaluator
        _evaluator = new();

    [Fact]
    public void CenarioPk7SemValidacaoIndustrial_DeveFicarPendente()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    industrialValidationConfirmed:
                        false));

        Assert.True(
            result.TechnicalCriteriaPassed);

        Assert.False(
            result.EligibleForExplicitApproval);

        Assert.Equal(
            MultivariateOperationalQualificationStatuses
                .TechnicallyQualifiedPendingIndustrialValidation,
            result.Status);

        Assert.Contains(
            "ValidacaoIndustrialNaoConfirmada",
            result.GovernanceBlockers);

        Assert.Contains(
            "RelacaoLinearHistoricaFraca",
            result.Warnings);

        Assert.Empty(
            result.TechnicalBlockers);
    }

    [Fact]
    public void CenarioPk7ComValidacaoIndustrialAuditavel_DeveSerElegivel()
    {
        var result =
            _evaluator.Evaluate(
                Context(
                    industrialValidationConfirmed:
                        true,
                    industrialValidationReference:
                        "RELATORIO-INDUSTRIAL-PK7-001"));

        Assert.True(
            result.TechnicalCriteriaPassed);

        Assert.True(
            result.EligibleForExplicitApproval);

        Assert.Equal(
            MultivariateOperationalQualificationStatuses
                .QualifiedForExplicitApproval,
            result.Status);

        Assert.Empty(
            result.TechnicalBlockers);

        Assert.Empty(
            result.GovernanceBlockers);
    }

    [Fact]
    public void HoldoutComPoucasAmostras_DeveBloquearQualificacaoTecnica()
    {
        var context =
            Context(
                industrialValidationConfirmed:
                    true,
                industrialValidationReference:
                    "VALIDADO");

        context =
            new MultivariateOperationalQualificationContext
            {
                Model =
                    context.Model,

                ReferenceBacktest =
                    context.ReferenceBacktest,

                HoldoutBacktest =
                    Summary(
                        total:
                            100,
                        applicable:
                            100,
                        anomalyCount:
                            0,
                        anomalyRate:
                            0,
                        p99:
                            70,
                        longestRun:
                            0),

                HoldoutStartUtc =
                    context.HoldoutStartUtc,

                HoldoutEndUtc =
                    context.HoldoutEndUtc,

                IndustrialValidationConfirmed =
                    true,

                IndustrialValidationReference =
                    "VALIDADO"
            };

        var result =
            _evaluator.Evaluate(
                context);

        Assert.False(
            result.TechnicalCriteriaPassed);

        Assert.Contains(
            "BacktestHoldoutComPoucosResultadosAplicaveis",
            result.TechnicalBlockers);
    }

    [Fact]
    public void CoberturaAplicavelBaixaNoHoldout_DeveBloquear()
    {
        var context =
            Context(
                industrialValidationConfirmed:
                    true,
                industrialValidationReference:
                    "VALIDADO");

        context =
            new MultivariateOperationalQualificationContext
            {
                Model =
                    context.Model,

                ReferenceBacktest =
                    context.ReferenceBacktest,

                HoldoutBacktest =
                    Summary(
                        total:
                            1000,
                        applicable:
                            900,
                        anomalyCount:
                            2,
                        anomalyRate:
                            2.0 / 900.0,
                        p99:
                            72.5,
                        longestRun:
                            1),

                HoldoutStartUtc =
                    context.HoldoutStartUtc,

                HoldoutEndUtc =
                    context.HoldoutEndUtc,

                IndustrialValidationConfirmed =
                    true,

                IndustrialValidationReference =
                    "VALIDADO"
            };

        var result =
            _evaluator.Evaluate(
                context);

        Assert.False(
            result.TechnicalCriteriaPassed);

        Assert.Contains(
            "CoberturaAplicavelHoldoutAbaixoDoLimite",
            result.TechnicalBlockers);
    }

    [Fact]
    public void TaxaDeAtivacaoAltaNoHoldout_DeveBloquear()
    {
        var context =
            Context(
                industrialValidationConfirmed:
                    true,
                industrialValidationReference:
                    "VALIDADO");

        context =
            new MultivariateOperationalQualificationContext
            {
                Model =
                    context.Model,

                ReferenceBacktest =
                    context.ReferenceBacktest,

                HoldoutBacktest =
                    Summary(
                        total:
                            1000,
                        applicable:
                            1000,
                        anomalyCount:
                            25,
                        anomalyRate:
                            0.025,
                        p99:
                            78,
                        longestRun:
                            2),

                HoldoutStartUtc =
                    context.HoldoutStartUtc,

                HoldoutEndUtc =
                    context.HoldoutEndUtc,

                IndustrialValidationConfirmed =
                    true,

                IndustrialValidationReference =
                    "VALIDADO"
            };

        var result =
            _evaluator.Evaluate(
                context);

        Assert.False(
            result.TechnicalCriteriaPassed);

        Assert.Contains(
            "TaxaAtivacaoHoldoutAcimaDoLimite",
            result.TechnicalBlockers);
    }

    [Fact]
    public void DerivaDoP99AcimaDoLimite_DeveBloquear()
    {
        var context =
            Context(
                industrialValidationConfirmed:
                    true,
                industrialValidationReference:
                    "VALIDADO");

        context =
            new MultivariateOperationalQualificationContext
            {
                Model =
                    context.Model,

                ReferenceBacktest =
                    context.ReferenceBacktest,

                HoldoutBacktest =
                    Summary(
                        total:
                            1000,
                        applicable:
                            1000,
                        anomalyCount:
                            2,
                        anomalyRate:
                            0.002,
                        p99:
                            85,
                        longestRun:
                            1),

                HoldoutStartUtc =
                    context.HoldoutStartUtc,

                HoldoutEndUtc =
                    context.HoldoutEndUtc,

                IndustrialValidationConfirmed =
                    true,

                IndustrialValidationReference =
                    "VALIDADO"
            };

        var result =
            _evaluator.Evaluate(
                context);

        Assert.False(
            result.TechnicalCriteriaPassed);

        Assert.Contains(
            "DerivaP99ScoreAcimaDoLimite",
            result.TechnicalBlockers);
    }

    [Fact]
    public void HoldoutAnteriorAoTreinamento_DeveBloquear()
    {
        var context =
            Context(
                industrialValidationConfirmed:
                    true,
                industrialValidationReference:
                    "VALIDADO");

        context =
            new MultivariateOperationalQualificationContext
            {
                Model =
                    context.Model,

                ReferenceBacktest =
                    context.ReferenceBacktest,

                HoldoutBacktest =
                    context.HoldoutBacktest,

                HoldoutStartUtc =
                    new DateTime(
                        2026,
                        9,
                        3,
                        18,
                        0,
                        0,
                        DateTimeKind.Utc),

                HoldoutEndUtc =
                    new DateTime(
                        2026,
                        9,
                        3,
                        19,
                        0,
                        0,
                        DateTimeKind.Utc),

                IndustrialValidationConfirmed =
                    true,

                IndustrialValidationReference =
                    "VALIDADO"
            };

        var result =
            _evaluator.Evaluate(
                context);

        Assert.False(
            result.TechnicalCriteriaPassed);

        Assert.Contains(
            "HoldoutNaoPosteriorAoTreinamento",
            result.TechnicalBlockers);
    }

    private static MultivariateOperationalQualificationContext
        Context(
            bool industrialValidationConfirmed,
            string? industrialValidationReference = null)
    {
        return new MultivariateOperationalQualificationContext
        {
            Model =
                Model(),

            ReferenceBacktest =
                Summary(
                    total:
                        4213,
                    applicable:
                        4213,
                    anomalyCount:
                        3,
                    anomalyRate:
                        3.0 / 4213.0,
                    p99:
                        71.5817,
                    longestRun:
                        1),

            HoldoutBacktest =
                Summary(
                    total:
                        996,
                    applicable:
                        996,
                    anomalyCount:
                        2,
                    anomalyRate:
                        2.0 / 996.0,
                    p99:
                        72.9119,
                    longestRun:
                        1),

            HoldoutStartUtc =
                new DateTime(
                    2026,
                    9,
                    4,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),

            HoldoutEndUtc =
                new DateTime(
                    2026,
                    9,
                    4,
                    17,
                    0,
                    0,
                    DateTimeKind.Utc),

            IndustrialValidationConfirmed =
                industrialValidationConfirmed,

            IndustrialValidationReference =
                industrialValidationReference
        };
    }

    private static MultivariateGroupModel Model()
    {
        return new MultivariateGroupModel
        {
            Version =
                1,

            ClienteId =
                "PK7",

            GroupName =
                "PK7_TESTE_MULTIVARIADO",

            TrainedAtUtc =
                new DateTime(
                    2026,
                    9,
                    3,
                    19,
                    7,
                    2,
                    DateTimeKind.Utc),

            Algorithm =
                "OnlineMeanCovariance",

            SampleCount =
                1403,

            MinimumAvailableTags =
                2,

            MaxLagMinutes =
                2,

            Tags =
                ["PK7.TAG1", "PK7.TAG2"],

            Means =
                [275.8096935, 39.9643621],

            StandardDeviations =
                [131.9481920, 6.0891041],

            CovarianceMatrix =
            [
                [17410.32538, 1.375524],
                [1.375524, 37.077188]
            ],

            CorrelationMatrix =
            [
                [1.0, 0.0017120298713141297],
                [0.0017120298713141297, 1.0]
            ]
        };
    }

    private static MultivariateHistoricalBacktestSummary
        Summary(
            int total,
            int applicable,
            int anomalyCount,
            double anomalyRate,
            double p99,
            int longestRun)
    {
        return new MultivariateHistoricalBacktestSummary
        {
            TotalResults =
                total,

            ApplicableResults =
                applicable,

            NotApplicableResults =
                total - applicable,

            AnomalyCount =
                anomalyCount,

            NormalCount =
                Math.Max(
                    0,
                    applicable - anomalyCount),

            AnomalyRate =
                anomalyRate,

            DistinctAnomalyMinutes =
                anomalyCount,

            LongestConsecutiveAnomalyRunMinutes =
                longestRun,

            ScoreDistribution =
                new MultivariateBacktestDistribution
                {
                    Count =
                        applicable,

                    Minimum =
                        0,

                    Mean =
                        36,

                    P50 =
                        37,

                    P75 =
                        50,

                    P90 =
                        60,

                    P95 =
                        65,

                    P99 =
                        p99,

                    Maximum =
                        Math.Max(
                            p99,
                            75)
                },

            MahalanobisSquaredDistribution =
                new MultivariateBacktestDistribution
                {
                    Count =
                        applicable,

                    Minimum =
                        0,

                    Mean =
                        2,

                    P50 =
                        1.9,

                    P75 =
                        2.8,

                    P90 =
                        3.7,

                    P95 =
                        4.2,

                    P99 =
                        5.2,

                    Maximum =
                        5.6
                }
        };
    }
}
