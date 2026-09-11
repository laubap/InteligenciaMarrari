using IAApi.Services.AnomalyDetection.Multivariate;
using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests;

public sealed class MultivariateDecisionCounterfactualObserverTests
{
    [Fact]
    public void EvidenciaElegivel_DeveAvaliarSemAlterarRuntimeDecision()
    {
        var logger =
            new CapturingLogger<
                MultivariateDecisionCounterfactualObserver>();

        var observer =
            CreateObserver(
                enabled: true,
                logger);

        var current =
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: false,
                EmitAnomaly: false,
                EmitNewEvidence: false,
                UsedLegacyFallback: false,
                Source: "LegacyNormal",
                Reasons: ["Test"]);

        var vectorTime =
            new DateTime(
                2026, 9, 3, 19, 40, 0);

        var currentTime =
            vectorTime.AddMinutes(1);

        var result =
            observer.Observe(
                current,
                "PK7",
                "PK7.TAG1",
                currentTime,
                [
                    new MultivariateDetectionResult
                    {
                        Aplicavel = true,
                        EhAnomalia = true,
                        ClienteId = "PK7",
                        GroupName =
                            "PK7_TESTE_MULTIVARIADO",
                        MinuteIndex =
                            MultivariateShadowRuntimeCoordinator
                                .CriarMinuteIndex(vectorTime),
                        ScoreNormalizado = 78.57,
                        LimiteScore = 75,
                        MahalanobisSquared = 6.161,
                        TagsUtilizadas =
                            ["PK7.TAG1", "PK7.TAG2"]
                    }
                ]);

        Assert.True(result.WouldEmitAnomaly);
        Assert.True(result.WouldChangeDecision);

        // Garantia principal do MV06.2:
        // a decisão operacional original continua intacta.
        Assert.False(current.EmitAnomaly);
        Assert.False(current.EmitNewEvidence);
        Assert.Equal("LegacyNormal", current.Source);

        Assert.Single(logger.Messages);
        Assert.Contains(
            "MULTIVARIATE FUSION COUNTERFACTUAL",
            logger.Messages[0]);
    }

    [Fact]
    public void SemEvidenciaElegivel_NaoDevePoluirLog()
    {
        var logger =
            new CapturingLogger<
                MultivariateDecisionCounterfactualObserver>();

        var observer =
            CreateObserver(
                enabled: true,
                logger);

        var current =
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: false,
                EmitAnomaly: false,
                EmitNewEvidence: false,
                UsedLegacyFallback: false,
                Source: "LegacyNormal",
                Reasons: ["Test"]);

        var result =
            observer.Observe(
                current,
                "PK7",
                "PK7.TAG1",
                new DateTime(
                    2026, 9, 3, 19, 41, 0),
                []);

        Assert.False(result.WouldChangeDecision);
        Assert.Empty(logger.Messages);
    }

    [Fact]
    public void FeatureOff_MesmoComAnomaliaMultivariada_NaoDeveLogarNemProporMudanca()
    {
        var logger =
            new CapturingLogger<
                MultivariateDecisionCounterfactualObserver>();

        var observer =
            CreateObserver(
                enabled: false,
                logger);

        var current =
            new RuntimeAnomalyDecision(
                LegacyFinalAnomaly: false,
                EmitAnomaly: false,
                EmitNewEvidence: false,
                UsedLegacyFallback: false,
                Source: "LegacyNormal",
                Reasons: ["Test"]);

        var vectorTime =
            new DateTime(
                2026, 9, 3, 19, 40, 0);

        var result =
            observer.Observe(
                current,
                "PK7",
                "PK7.TAG1",
                vectorTime.AddMinutes(1),
                [
                    new MultivariateDetectionResult
                    {
                        Aplicavel = true,
                        EhAnomalia = true,
                        ClienteId = "PK7",
                        GroupName = "GRUPO",
                        MinuteIndex =
                            MultivariateShadowRuntimeCoordinator
                                .CriarMinuteIndex(vectorTime),
                        ScoreNormalizado = 90,
                        LimiteScore = 75,
                        MahalanobisSquared = 10,
                        TagsUtilizadas =
                            ["PK7.TAG1", "PK7.TAG2"]
                    }
                ]);

        Assert.False(result.WouldChangeDecision);
        Assert.False(result.WouldEmitAnomaly);
        Assert.Empty(logger.Messages);
    }

    private static
        MultivariateDecisionCounterfactualObserver
        CreateObserver(
            bool enabled,
            CapturingLogger<
                MultivariateDecisionCounterfactualObserver> logger)
    {
        var options =
            Options.Create(
                new MultivariateDecisionFusionOptions
                {
                    Enabled = enabled,
                    CanaryOnly = true,
                    AllowedClientIds = ["PK7"],
                    MaxEvidenceAgeMinutes = 2
                });

        return
            new MultivariateDecisionCounterfactualObserver(
                new MultivariateDecisionCounterfactualEvaluator(),
                options,
                logger);
    }

    private sealed class CapturingLogger<T>
        : ILogger<T>
    {
        public List<string> Messages { get; } =
            [];

        public IDisposable? BeginScope<TState>(
            TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(
            LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(
                formatter(
                    state,
                    exception));
        }
    }
}
