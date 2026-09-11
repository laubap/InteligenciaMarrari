using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class MultivariateEvidenceTypeClassifierTests
{
    private readonly MultivariateEvidenceTypeClassifier
        _classifier = new();

    [Fact]
    public void CorrelacaoFracaEDesviosEquilibrados_DeveSerJointDeviation()
    {
        var result =
            _classifier.Classificar(
                Detection(
                    ("TAG-A", 8.3, 10.0, 1.0),
                    ("TAG-B", 21.6, 20.0, 1.0)),
                Model(
                    correlation:
                        0.0017));

        Assert.Equal(
            MultivariateEvidenceTypes
                .JointDeviation,
            result.EvidenceType);

        Assert.NotNull(
            result.MaximumAbsoluteCorrelation);

        Assert.Equal(
            0.0017,
            result.MaximumAbsoluteCorrelation!.Value,
            6);
    }

    [Fact]
    public void CorrelacaoFracaComSinaisOpostos_NaoDeveMarcarContradicaoRelacional()
    {
        var result =
            _classifier.Classificar(
                Detection(
                    ("TAG-A", 8.3, 10.0, 1.0),
                    ("TAG-B", 21.6, 20.0, 1.0)),
                Model(
                    correlation:
                        0.0017));

        Assert.Equal(
            MultivariateEvidenceTypes
                .JointDeviation,
            result.EvidenceType);

        Assert.False(
            result.RelationshipDirectionContradicted);
    }

    [Fact]
    public void CorrelacaoPositivaForteComSinaisOpostos_DeveSerRelationalDeviation()
    {
        var result =
            _classifier.Classificar(
                Detection(
                    ("TAG-A", 12.0, 10.0, 1.0),
                    ("TAG-B", 18.0, 20.0, 1.0)),
                Model(
                    correlation:
                        0.85));

        Assert.Equal(
            MultivariateEvidenceTypes
                .RelationalDeviation,
            result.EvidenceType);

        Assert.True(
            result.RelationshipDirectionContradicted);

        Assert.Equal(
            0.85,
            result.RelationshipCorrelation!.Value,
            6);
    }

    [Fact]
    public void CorrelacaoNegativaForteComMesmoSinal_DeveSerRelationalDeviation()
    {
        var result =
            _classifier.Classificar(
                Detection(
                    ("TAG-A", 12.0, 10.0, 1.0),
                    ("TAG-B", 22.0, 20.0, 1.0)),
                Model(
                    correlation:
                        -0.80));

        Assert.Equal(
            MultivariateEvidenceTypes
                .RelationalDeviation,
            result.EvidenceType);

        Assert.True(
            result.RelationshipDirectionContradicted);
    }

    [Fact]
    public void UmaTagDominandoEnergia_DeveSerSingleTagDominated()
    {
        var result =
            _classifier.Classificar(
                Detection(
                    ("TAG-A", 14.0, 10.0, 1.0),
                    ("TAG-B", 20.5, 20.0, 1.0)),
                Model(
                    correlation:
                        0.10));

        Assert.Equal(
            MultivariateEvidenceTypes
                .SingleTagDominated,
            result.EvidenceType);

        Assert.Equal(
            "TAG-A",
            result.DominantTag);

        Assert.True(
            result.DominantSquaredZShare >
            0.98);
    }

    [Fact]
    public void CorrelacaoPositivaForteComMesmoSentido_NaoDeveChamarDeQuebraRelacional()
    {
        var result =
            _classifier.Classificar(
                Detection(
                    ("TAG-A", 12.0, 10.0, 1.0),
                    ("TAG-B", 22.0, 20.0, 1.0)),
                Model(
                    correlation:
                        0.90));

        Assert.Equal(
            MultivariateEvidenceTypes
                .JointDeviation,
            result.EvidenceType);

        Assert.False(
            result.RelationshipDirectionContradicted);
    }

    [Fact]
    public void TagNaoExistenteNoModelo_DeveSerIndeterminate()
    {
        var result =
            _classifier.Classificar(
                Detection(
                    ("TAG-A", 12.0, 10.0, 1.0),
                    ("TAG-X", 22.0, 20.0, 1.0)),
                Model(
                    correlation:
                        0.90));

        Assert.Equal(
            MultivariateEvidenceTypes
                .Indeterminate,
            result.EvidenceType);

        Assert.Equal(
            "ModelOrTagMappingInvalid",
            result.Reason);
    }

    private static MultivariateDetectionResult Detection(
        params (
            string Tag,
            double Value,
            double Mean,
            double StandardDeviation)[]
            contributions)
    {
        return new MultivariateDetectionResult
        {
            Aplicavel =
                true,

            EhAnomalia =
                true,

            ClienteId =
                "PK7",

            GroupName =
                "GRUPO-AB",

            MinuteIndex =
                100,

            ScoreNormalizado =
                80,

            LimiteScore =
                75,

            MahalanobisSquared =
                6,

            TagsUtilizadas =
                contributions
                    .Select(item =>
                        item.Tag)
                    .ToList(),

            Contribuicoes =
                contributions
                    .Select(item =>
                    {
                        var signedZ =
                            (
                                item.Value -
                                item.Mean
                            ) /
                            item.StandardDeviation;

                        return new MultivariateTagContribution
                        {
                            TagName =
                                item.Tag,

                            ValorAtual =
                                item.Value,

                            MediaTreinamento =
                                item.Mean,

                            DesvioPadraoTreinamento =
                                item.StandardDeviation,

                            ZScoreAbsoluto =
                                Math.Abs(
                                    signedZ)
                        };
                    })
                    .ToList()
        };
    }

    private static MultivariateGroupModel Model(
        double correlation)
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
                    2026,
                    9,
                    3,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc),

            Algorithm =
                "OnlineMeanCovariance",

            SampleCount =
                1000,

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
}
