using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class MultivariateOperationalFusionResolverTests
{
    private readonly MultivariateOperationalFusionResolver
        _resolver = new();

    [Fact]
    public void FeatureOff_PreservaRuntimeMesmoQuandoCounterfactualPromoveria()
    {
        var current =
            Decision(false, false);

        var result =
            _resolver.Resolve(
                current,
                PromotionCounterfactual(),
                Options(enabled: false),
                "PK7",
                univariateScore: 0);

        Assert.False(result.PromotionApplied);
        Assert.Same(current, result.Decision);
        Assert.False(result.Decision.EmitAnomaly);
        Assert.Equal(
            "MultivariateOperationalPromotionDisabled",
            result.Source);
    }

    [Fact]
    public void ClienteForaDoCanario_NaoRecebePromocao()
    {
        var current =
            Decision(false, false);

        var result =
            _resolver.Resolve(
                current,
                PromotionCounterfactual(
                    clienteId: "OUTRO"),
                Options(
                    enabled: true,
                    allowed: ["PK7"]),
                "OUTRO",
                univariateScore: 0);

        Assert.False(result.PromotionApplied);
        Assert.Same(current, result.Decision);
        Assert.False(result.Decision.EmitAnomaly);
        Assert.Equal(
            "MultivariateOperationalPromotionCanaryExcluded",
            result.Source);
    }

    [Fact]
    public void PromocaoValida_GeraNovaRuntimeDecisionESegregaScores()
    {
        var current =
            Decision(
                false,
                false,
                "LegacyNormal");

        var result =
            _resolver.Resolve(
                current,
                PromotionCounterfactual(
                    multivariateScore: 78.57),
                Options(enabled: true),
                "PK7",
                univariateScore: 0);

        Assert.True(result.PromotionApplied);
        Assert.True(result.Decision.EmitAnomaly);
        Assert.True(result.Decision.EmitNewEvidence);
        Assert.Equal(
            "MultivariateNewAnomaly",
            result.Decision.Source);

        Assert.Equal(
            0,
            result.UnivariateScore);

        Assert.NotNull(
            result.MultivariateEvidence);

        Assert.Equal(
            78.57,
            result.MultivariateEvidence!.Score,
            2);

        Assert.Equal(
            "MultivariateOperationalPromotionApplied",
            result.Source);
    }

    [Fact]
    public void RuntimeJaAnomalo_NaoCriaNovaPromocao()
    {
        var current =
            Decision(
                true,
                false,
                "IncreasingNoiseContinuation");

        var counterfactual =
            PromotionCounterfactual() with
            {
                OriginalEmitAnomaly = true,
                OriginalEmitNewEvidence = false,
                WouldEmitAnomaly = true,
                WouldEmitNewEvidence = false,
                WouldChangeDecision = false,
                Source =
                    "MultivariateCorroborationCounterfactual"
            };

        var result =
            _resolver.Resolve(
                current,
                counterfactual,
                Options(enabled: true),
                "PK7",
                univariateScore: 3.5);

        Assert.False(result.PromotionApplied);
        Assert.Same(current, result.Decision);
        Assert.True(result.Decision.EmitAnomaly);
        Assert.False(result.Decision.EmitNewEvidence);
        Assert.Equal(
            "MultivariateOperationalCorroboration",
            result.Source);
    }

    [Fact]
    public void CounterfactualStale_NaoPodePromover()
    {
        var current =
            Decision(
                false,
                true);

        var counterfactual =
            PromotionCounterfactual() with
            {
                OriginalEmitAnomaly = false,
                OriginalEmitNewEvidence = false
            };

        var result =
            _resolver.Resolve(
                current,
                counterfactual,
                Options(enabled: true),
                "PK7",
                univariateScore: 0);

        Assert.False(result.PromotionApplied);
        Assert.Same(current, result.Decision);
        Assert.Equal(
            "MultivariateOperationalStaleCounterfactual",
            result.Source);
    }

    [Fact]
    public void EvidenciaAcimaDaIdadeMaxima_NaoPodePromover()
    {
        var counterfactual =
            PromotionCounterfactual() with
            {
                Evidence =
                    Evidence(ageMinutes: 3)
            };

        var current =
            Decision(false, false);

        var result =
            _resolver.Resolve(
                current,
                counterfactual,
                Options(
                    enabled: true,
                    maxAgeMinutes: 2),
                "PK7",
                univariateScore: 0);

        Assert.False(result.PromotionApplied);
        Assert.Same(current, result.Decision);
        Assert.Equal(
            "MultivariateOperationalInvalidEvidence",
            result.Source);
    }

    [Fact]
    public void ScoreAbaixoDoThreshold_NaoPodePromover()
    {
        var current =
            Decision(
                false,
                false);

        var counterfactual =
            PromotionCounterfactual(
                multivariateScore: 74.99);

        var result =
            _resolver.Resolve(
                current,
                counterfactual,
                Options(enabled: true),
                "PK7",
                univariateScore: 0.18);

        Assert.False(
            result.PromotionApplied);

        Assert.Same(
            current,
            result.Decision);

        Assert.False(
            result.Decision.EmitAnomaly);

        Assert.False(
            result.Decision.EmitNewEvidence);

        Assert.Equal(
            "MultivariateOperationalInvalidEvidence",
            result.Source);

        Assert.Equal(
            0.18,
            result.UnivariateScore,
            6);

        Assert.NotNull(
            result.MultivariateEvidence);

        Assert.Equal(
            74.99,
            result.MultivariateEvidence!.Score,
            2);

        Assert.Equal(
            75,
            result.MultivariateEvidence.Threshold,
            2);
    }

    [Fact]
    public void EvidenciaNumericamenteInvalida_NaoPodePromover()
    {
        var current =
            Decision(false, false);

        var casos =
            new[]
            {
                Evidence(
                    score: double.NaN),

                Evidence(
                    score: 78.57,
                    threshold: double.PositiveInfinity),

                Evidence(
                    score: 78.57,
                    mahalanobisSquared: -1)
            };

        foreach (var evidence in casos)
        {
            var counterfactual =
                PromotionCounterfactual() with
                {
                    Evidence =
                        evidence
                };

            var result =
                _resolver.Resolve(
                    current,
                    counterfactual,
                    Options(enabled: true),
                    "PK7",
                    univariateScore: 0.18);

            Assert.False(
                result.PromotionApplied);

            Assert.Same(
                current,
                result.Decision);

            Assert.False(
                result.Decision.EmitAnomaly);

            Assert.False(
                result.Decision.EmitNewEvidence);

            Assert.Equal(
                "MultivariateOperationalInvalidEvidence",
                result.Source);
        }
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

    private static MultivariateDecisionCounterfactual
        PromotionCounterfactual(
            string clienteId = "PK7",
            double multivariateScore = 78.57)
    {
        return new MultivariateDecisionCounterfactual
        {
            OriginalEmitAnomaly =
                false,

            OriginalEmitNewEvidence =
                false,

            WouldEmitAnomaly =
                true,

            WouldEmitNewEvidence =
                true,

            WouldChangeDecision =
                true,

            OriginalSource =
                "LegacyNormal",

            Source =
                "MultivariateNewAnomalyCounterfactual",

            Reasons =
                ["Test"],

            Evidence =
                Evidence(
                    clienteId:
                        clienteId,

                    score:
                        multivariateScore)
        };
    }

    private static MultivariateDecisionEvidence Evidence(
        string clienteId = "PK7",
        long ageMinutes = 1,
        double score = 78.57,
        double threshold = 75,
        double mahalanobisSquared = 6.161)
    {
        return new MultivariateDecisionEvidence
        {
            ClienteId =
                clienteId,

            GroupName =
                "PK7_TESTE_MULTIVARIADO",

            MinuteIndex =
                1065401050,

            AgeMinutes =
                ageMinutes,

            Score =
                score,

            Threshold =
                threshold,

            MahalanobisSquared =
                mahalanobisSquared,

            Tags =
                ["PK7.TAG1", "PK7.TAG2"]
        };
    }

    private static MultivariateOperationalPromotionOptions
        Options(
            bool enabled,
            string[]? allowed = null,
            int maxAgeMinutes = 2)
    {
        return new MultivariateOperationalPromotionOptions
        {
            Enabled =
                enabled,

            CanaryOnly =
                true,

            AllowedClientIds =
                allowed ?? ["PK7"],

            MaxEvidenceAgeMinutes =
                maxAgeMinutes
        };
    }
}
