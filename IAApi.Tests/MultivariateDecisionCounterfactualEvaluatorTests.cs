using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class MultivariateDecisionCounterfactualEvaluatorTests
{
    private readonly MultivariateDecisionCounterfactualEvaluator
        _evaluator = new();

    [Fact]
    public void FeatureOff_PreservaDecisaoAtualExatamente()
    {
        var current =
            Decision(
                emitAnomaly: false,
                emitNewEvidence: false,
                source: "LegacyNormal");

        var result =
            _evaluator.Evaluate(
                current,
                Options(
                    enabled: false),
                "PK7",
                "PK7.TAG1",
                Time(19, 41),
                [Anomaly(Time(19, 40))]);

        Assert.False(result.WouldEmitAnomaly);
        Assert.False(result.WouldEmitNewEvidence);
        Assert.False(result.WouldChangeDecision);
        Assert.Equal(
            "MultivariateFusionDisabled",
            result.Source);
        Assert.Null(result.Evidence);

        // O objeto operacional original jamais é alterado.
        Assert.False(current.EmitAnomaly);
        Assert.False(current.EmitNewEvidence);
        Assert.Equal("LegacyNormal", current.Source);
    }

    [Fact]
    public void Canary_ClienteForaDaAllowlist_NaoPropoeMudanca()
    {
        var result =
            _evaluator.Evaluate(
                Decision(false, false),
                Options(
                    enabled: true,
                    canaryOnly: true,
                    allowed: ["PK7"]),
                "OUTRO",
                "PK7.TAG1",
                Time(19, 41),
                [Anomaly(Time(19, 40), clienteId: "OUTRO")]);

        Assert.False(result.WouldEmitAnomaly);
        Assert.False(result.WouldChangeDecision);
        Assert.Equal(
            "MultivariateFusionCanaryExcluded",
            result.Source);
    }

    [Fact]
    public void AnomaliaFrescaDaTagAtual_RuntimeNormal_PropoePromocaoSomenteCounterfactual()
    {
        var current =
            Decision(
                emitAnomaly: false,
                emitNewEvidence: false,
                source: "LegacyNormal");

        var result =
            _evaluator.Evaluate(
                current,
                Options(enabled: true),
                "PK7",
                "PK7.TAG1",
                Time(19, 41),
                [Anomaly(Time(19, 40))]);

        Assert.True(result.WouldEmitAnomaly);
        Assert.True(result.WouldEmitNewEvidence);
        Assert.True(result.WouldChangeDecision);
        Assert.Equal(
            "MultivariateNewAnomalyCounterfactual",
            result.Source);

        Assert.NotNull(result.Evidence);
        Assert.Equal(
            "PK7_TESTE_MULTIVARIADO",
            result.Evidence!.GroupName);
        Assert.Equal(1, result.Evidence.AgeMinutes);
        Assert.Equal(78.57, result.Evidence.Score, 2);

        // Ainda não existe autoridade operacional.
        Assert.False(current.EmitAnomaly);
        Assert.False(current.EmitNewEvidence);
    }

    [Fact]
    public void EvidenciaAntiga_NaoPodePromoverDecisao()
    {
        var result =
            _evaluator.Evaluate(
                Decision(false, false),
                Options(
                    enabled: true,
                    maxAgeMinutes: 2),
                "PK7",
                "PK7.TAG1",
                Time(19, 45),
                [Anomaly(Time(19, 40))]);

        Assert.False(result.WouldEmitAnomaly);
        Assert.False(result.WouldChangeDecision);
        Assert.Equal(
            "NoFreshMultivariateAnomaly",
            result.Source);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public void TagAtualNaoParticipaDoVetor_NaoPodePromoverDecisao()
    {
        var result =
            _evaluator.Evaluate(
                Decision(false, false),
                Options(enabled: true),
                "PK7",
                "PK7.TAG9",
                Time(19, 41),
                [Anomaly(Time(19, 40))]);

        Assert.False(result.WouldEmitAnomaly);
        Assert.False(result.WouldChangeDecision);
        Assert.Equal(
            "NoFreshMultivariateAnomaly",
            result.Source);
    }

    [Fact]
    public void RuntimeJaAnomalo_MultivariadoSomenteCorroboraSemNovaEvidencia()
    {
        var result =
            _evaluator.Evaluate(
                Decision(
                    emitAnomaly: true,
                    emitNewEvidence: false,
                    source: "StableContinuation"),
                Options(enabled: true),
                "PK7",
                "PK7.TAG1",
                Time(19, 41),
                [Anomaly(Time(19, 40))]);

        Assert.True(result.WouldEmitAnomaly);
        Assert.False(result.WouldEmitNewEvidence);
        Assert.False(result.WouldChangeDecision);
        Assert.Equal(
            "MultivariateCorroborationCounterfactual",
            result.Source);
        Assert.NotNull(result.Evidence);
    }

    private static RuntimeAnomalyDecision Decision(
        bool emitAnomaly,
        bool emitNewEvidence,
        string source = "RuntimeTest")
    {
        return new RuntimeAnomalyDecision(
            LegacyFinalAnomaly:
                emitAnomaly,

            EmitAnomaly:
                emitAnomaly,

            EmitNewEvidence:
                emitNewEvidence,

            UsedLegacyFallback:
                false,

            Source:
                source,

            Reasons:
                ["Test"]);
    }

    private static MultivariateDecisionFusionOptions Options(
        bool enabled,
        bool canaryOnly = true,
        string[]? allowed = null,
        int maxAgeMinutes = 2)
    {
        return new MultivariateDecisionFusionOptions
        {
            Enabled =
                enabled,

            CanaryOnly =
                canaryOnly,

            AllowedClientIds =
                allowed ?? ["PK7"],

            MaxEvidenceAgeMinutes =
                maxAgeMinutes
        };
    }

    private static MultivariateDetectionResult Anomaly(
        DateTime vectorTime,
        string clienteId = "PK7")
    {
        return new MultivariateDetectionResult
        {
            Aplicavel =
                true,

            EhAnomalia =
                true,

            ClienteId =
                clienteId,

            GroupName =
                "PK7_TESTE_MULTIVARIADO",

            MinuteIndex =
                MultivariateShadowRuntimeCoordinator
                    .CriarMinuteIndex(vectorTime),

            ScoreNormalizado =
                78.57,

            LimiteScore =
                75,

            MahalanobisSquared =
                6.161,

            TagsUtilizadas =
                ["PK7.TAG1", "PK7.TAG2"]
        };
    }

    private static DateTime Time(
        int hour,
        int minute)
    {
        return new DateTime(
            2026,
            9,
            3,
            hour,
            minute,
            0,
            DateTimeKind.Utc);
    }
}
