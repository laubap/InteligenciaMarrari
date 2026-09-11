using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

namespace IAApi.Tests;

public sealed class MultivariateOperationalInterpretationFactoryTests
{
    private readonly MultivariateOperationalInterpretationFactory
        _factory = new();

    [Fact]
    public void EvidenciaValida_DeveGerarInterpretacaoExplicitamenteMultivariada()
    {
        var result =
            _factory.Create(
                Evidence(),
                univariateScore: 0.1317,
                severity: "Média");

        Assert.Equal(
            "Anomalia multivariada detectada.",
            result.Resumo);

        Assert.Contains(
            "PK7_TESTE_MULTIVARIADO",
            result.InterpretacaoTecnica);

        Assert.Contains(
            result.Evidencias,
            item =>
                item.Contains(
                    "Score multivariado: 78.57",
                    StringComparison.Ordinal));

        Assert.Contains(
            result.Evidencias,
            item =>
                item.Contains(
                    "PK7.TAG1, PK7.TAG2",
                    StringComparison.Ordinal));

        Assert.False(result.GeradoPorLlm);

        Assert.Equal(
            "Interpretação determinística multivariada",
            result.ModeloUtilizado);

        Assert.Equal(
            "Média",
            result.Severidade);
    }

    [Fact]
    public void Scores_DeveManterEscalasSeparadasNaExplicacao()
    {
        var result =
            _factory.Create(
                Evidence(),
                univariateScore: 0.1317,
                severity: "Média");

        Assert.Contains(
            result.Evidencias,
            item =>
                item.Contains(
                    "Score multivariado: 78.57",
                    StringComparison.Ordinal));

        Assert.Contains(
            result.Evidencias,
            item =>
                item.Contains(
                    "Score univariado da tag atual: 0.1317",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void ScoreAbaixoDoThreshold_NaoPodeGerarInterpretacaoDeAnomalia()
    {
        var evidence =
            Evidence() with
            {
                Score = 70,
                Threshold = 75
            };

        Assert.Throws<ArgumentException>(() =>
            _factory.Create(
                evidence,
                univariateScore: 0,
                severity: "Média"));
    }

    [Fact]
    public void NaoDeveInventarCausaFisica()
    {
        var result =
            _factory.Create(
                Evidence(),
                univariateScore: 0,
                severity: "Média");

        Assert.Empty(
            result.PossiveisCausas);

        Assert.NotEmpty(
            result.Recomendacoes);
    }

    private static MultivariateDecisionEvidence Evidence()
    {
        return new MultivariateDecisionEvidence
        {
            ClienteId =
                "PK7",

            GroupName =
                "PK7_TESTE_MULTIVARIADO",

            MinuteIndex =
                1065401970,

            AgeMinutes =
                1,

            Score =
                78.57,

            Threshold =
                75,

            MahalanobisSquared =
                6.1610,

            Tags =
                ["PK7.TAG1", "PK7.TAG2"]
        };
    }
}
