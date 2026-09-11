using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class MultivariateModelQualityAuditorTests
{
    private readonly MultivariateModelQualityAuditor
        _auditor = new();

    [Fact]
    public void ModeloSaudavelComResultadoTreinamento_DeveAuditarCobertura()
    {
        var model =
            Model(
                correlation:
                    0.85,
                sampleCount:
                    200);

        var training =
            Training(
                finalized:
                    200,
                complete:
                    200,
                incomplete:
                    0,
                invalid:
                    0);

        var result =
            _auditor.Auditar(
                model,
                training);

        Assert.True(
            result.StructurallyValid);

        Assert.True(
            result.TrainingCoverageAvailable);

        Assert.Empty(
            result.StructuralIssues);

        Assert.Equal(
            200,
            result.TrainingCompleteVectors);

        Assert.NotNull(
            result.TrainingCompletionRate);

        Assert.Equal(
            1.0,
            result.TrainingCompletionRate!.Value,
            6);

        Assert.NotNull(
            result.MaximumAbsoluteLinearCorrelation);

        Assert.Equal(
            0.85,
            result.MaximumAbsoluteLinearCorrelation!.Value,
            6);
    }

    [Fact]
    public void CorrelacaoLinearFraca_DeveSerWarningENaoErroEstrutural()
    {
        var result =
            _auditor.Auditar(
                Model(
                    correlation:
                        0.0017));

        Assert.True(
            result.StructurallyValid);

        Assert.Contains(
            "RelacaoLinearHistoricaFraca",
            result.Warnings);

        Assert.DoesNotContain(
            "RelacaoLinearHistoricaFraca",
            result.StructuralIssues);
    }

    [Fact]
    public void CorrelacaoQuaseColinear_DeveAlertarCondicionamento()
    {
        var result =
            _auditor.Auditar(
                Model(
                    correlation:
                        0.999999));

        Assert.True(
            result.StructurallyValid);

        Assert.Contains(
            "PossivelColinearidadeForte",
            result.Warnings);

        Assert.Contains(
            "MatrizCorrelacaoMalCondicionada",
            result.Warnings);

        Assert.NotNull(
            result.EstimatedCorrelationConditionNumber);

        Assert.True(
            result.EstimatedCorrelationConditionNumber >
            1_000_000);
    }

    [Fact]
    public void SampleCountDivergenteDoTreinamento_DeveSerProblemaEstrutural()
    {
        var result =
            _auditor.Auditar(
                Model(
                    sampleCount:
                        200),
                Training(
                    finalized:
                        200,
                    complete:
                        199,
                    incomplete:
                        1,
                    invalid:
                        0));

        Assert.False(
            result.StructurallyValid);

        Assert.Contains(
            "SampleCountDivergeDoResultadoTreinamento",
            result.StructuralIssues);
    }

    [Fact]
    public void SemResultadoTreinamento_DeveExplicitarGapDeMetadados()
    {
        var result =
            _auditor.Auditar(
                Model());

        Assert.False(
            result.TrainingCoverageAvailable);

        Assert.Contains(
            "MetadadosDeCoberturaDoTreinamentoIndisponiveis",
            result.Warnings);

        Assert.NotEmpty(
            result.Notes);
    }

    [Fact]
    public void TagSemVariancia_DeveSerSinalizada()
    {
        var model =
            Model();

        model.StandardDeviations =
            [0, 2];

        model.CovarianceMatrix =
        [
            [0, 0],
            [0, 4]
        ];

        model.CorrelationMatrix =
        [
            [1, 0],
            [0, 1]
        ];

        var result =
            _auditor.Auditar(
                model);

        Assert.True(
            result.StructurallyValid);

        Assert.Contains(
            "TAG-A",
            result.NearZeroVarianceTags);

        Assert.Contains(
            "TagComVarianciaQuaseZero",
            result.Warnings);
    }

    private static MultivariateGroupModel Model(
        double correlation = 0.85,
        long sampleCount = 200)
    {
        return new MultivariateGroupModel
        {
            Version =
                1,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            TrainedAtUtc =
                new DateTime(
                    2026, 9, 4, 12, 0, 0,
                    DateTimeKind.Utc),

            Algorithm =
                "OnlineMeanCovariance",

            SampleCount =
                sampleCount,

            MinimumAvailableTags =
                2,

            MaxLagMinutes =
                2,

            Tags =
                ["TAG-A", "TAG-B"],

            Means =
                [10, 20],

            StandardDeviations =
                [1, 1],

            CovarianceMatrix =
            [
                [1, correlation],
                [correlation, 1]
            ],

            CorrelationMatrix =
            [
                [1, correlation],
                [correlation, 1]
            ]
        };
    }

    private static OperationalMultivariateTrainingResult
        Training(
            long finalized,
            long complete,
            long incomplete,
            long invalid)
    {
        return new OperationalMultivariateTrainingResult
        {
            ClienteId =
                "PK7",

            GrupoId =
                1,

            GrupoNome =
                "GRUPO-AB",

            Inicio =
                new DateTime(
                    2026, 9, 1, 0, 0, 0),

            Fim =
                new DateTime(
                    2026, 9, 2, 0, 0, 0),

            Tags =
                ["TAG-A", "TAG-B"],

            LeiturasValidasPorTag =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["TAG-A"] =
                        200,

                    ["TAG-B"] =
                        200
                },

            TotalLeiturasValidas =
                400,

            VetoresFinalizados =
                finalized,

            VetoresCompletosUsados =
                complete,

            VetoresIncompletosIgnorados =
                incomplete,

            VetoresInvalidosIgnorados =
                invalid,

            MinimumAvailableTags =
                2,

            MaxLagMinutes =
                2,

            ModeloGerado =
                true,

            TreinadoEmUtc =
                new DateTime(
                    2026, 9, 4, 12, 0, 0,
                    DateTimeKind.Utc)
        };
    }
}