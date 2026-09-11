using System.Text.Json;
using Xunit;

namespace IAApi.Tests;

public sealed class AnomaliaResponseMultivariateContractTests
{
    [Fact]
    public void FeatureOff_MultivariadoNulo_DeveSerOmitidoDoJson()
    {
        var response =
            new AnomaliaResponse
            {
                ClienteId = "PK7",
                TagName = "PK7.TAG1",
                TipoTag = "temperatura",
                EhAnomalia = false,
                Score = 0.1317f,
                Mensagem = "normal"
            };

        var json =
            JsonSerializer.Serialize(
                response);

        Assert.DoesNotContain(
            "\"Multivariado\"",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PromocaoMultivariada_DeveExporScoresSeparados()
    {
        var response =
            new AnomaliaResponse
            {
                ClienteId = "PK7",
                TagName = "PK7.TAG1",
                TipoTag = "temperatura",
                EhAnomalia = true,
                Score = 0.1317f,

                Multivariado =
                    new MultivariateAnomalyDto
                    {
                        PromocaoOperacional = true,
                        Origem = "MultivariateNewAnomaly",
                        Grupo = "PK7_TESTE_MULTIVARIADO",
                        MinuteIndex = 1065401970,
                        IdadeMinutos = 1,
                        ScoreNormalizado = 78.57,
                        LimiteScore = 75,
                        MahalanobisSquared = 6.1610,
                        ScoreUnivariado = 0.1317,
                        Tags =
                            ["PK7.TAG1", "PK7.TAG2"]
                    }
            };

        var json =
            JsonSerializer.Serialize(
                response);

        Assert.Contains(
            "\"Score\":0.1317",
            json,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"ScoreNormalizado\":78.57",
            json,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"ScoreUnivariado\":0.1317",
            json,
            StringComparison.Ordinal);

        Assert.Contains(
            "PK7_TESTE_MULTIVARIADO",
            json,
            StringComparison.Ordinal);
    }
}
