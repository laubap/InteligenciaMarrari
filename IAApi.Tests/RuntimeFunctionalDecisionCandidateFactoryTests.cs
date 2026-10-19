using IAApi.Services.AnomalyDetection.Runtime;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeFunctionalDecisionCandidateFactoryTests
{
    private readonly RuntimeFunctionalDecisionCandidateFactory
        _factory = new();

    [Fact]
    public void LegacyNormal_NaoInventaEvento()
    {
        var result =
_factory.Create(
    false,
    context: null);

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.LegacyNormal,
            result.Source);
    }

    [Fact]
    public void LegacyNormal_ComEvidenciaAtivaForte_RecuperaFalsoNegativo()
    {
        var result =
            _factory.Create(
                    false,
                Context(
                    zActiveVote: true,
                    zActiveScore: 100,
                    madActiveVote: true,
                    madActiveScore: 100,
                    rateActiveVote: true,
                    rateActiveScore: 100,
                    legacyScoreNormalized: 54.67));

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.NewAnomaly,
            result.Source);
        Assert.Contains(
            "LegacyFalseNegativeRecovered",
            result.Reasons);
        Assert.Contains(
            "StrongZActiveEvidence",
            result.Reasons);
        Assert.Contains(
            "StrongMadActiveEvidence",
            result.Reasons);
        Assert.Contains(
            "StrongRateOfChangeEvidence",
            result.Reasons);
    }

    [Fact]
    public void LegacyNormal_SemRateForte_NaoRecuperaFalsoNegativo()
    {
        var result =
            _factory.Create(
                    false,
                Context(
                    zActiveVote: true,
                    zActiveScore: 100,
                    madActiveVote: true,
                    madActiveScore: 100,
                    rateActiveVote: false,
                    rateActiveScore: 0,
                    legacyScoreNormalized: 54.67));

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.LegacyNormal,
            result.Source);
    }

    [Fact]
    public void LegacyNormal_ComDetectorAbaixoDoLimiteForte_NaoRecuperaFalsoNegativo()
    {
        var result =
            _factory.Create(
                    false,
                Context(
                    zActiveVote: true,
                    zActiveScore: 100,
                    madActiveVote: true,
                    madActiveScore: 89.99,
                    rateActiveVote: true,
                    rateActiveScore: 100,
                    legacyScoreNormalized: 54.67));

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.LegacyNormal,
            result.Source);
    }

    [Fact]
    public void LegacyNormal_ComScoreLegacyAbaixoDoMinimo_NaoRecuperaFalsoNegativo()
    {
        var result =
            _factory.Create(
                false,
                Context(
                    zActiveVote: true,
                    zActiveScore: 100,
                    madActiveVote: true,
                    madActiveScore: 100,
                    rateActiveVote: true,
                    rateActiveScore: 100,
                    legacyScoreNormalized: 49.99));

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.LegacyNormal,
            result.Source);
    }

    [Fact]
    public void LegacyNormal_ComCriticalGap_NaoRecuperaFalsoNegativo()
    {
        var result =
            _factory.Create(
                    false,
                Context(
                    criticalGap: true,
                    zActiveVote: true,
                    zActiveScore: 100,
                    madActiveVote: true,
                    madActiveScore: 100,
                    rateActiveVote: true,
                    rateActiveScore: 100,
                    legacyScoreNormalized: 54.67));

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.LegacyNormal,
            result.Source);
    }

    [Fact]
    public void LegacyAnomalo_SemContexto_PreservaLegacy()
    {
        var result =
            _factory.Create(
                true,
                context: null);

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.NewAnomaly,
            result.Source);
        Assert.Contains(
            "RuntimeFunctionalContextUnavailable",
            result.Reasons);
    }

    [Fact]
    public void CriticalGap_TemPrioridadeSobreCandidateMismatch()
    {
        var result =
            _factory.Create(
                true,
                Context(
                    criticalGap: true,
                    madCandidateVote: true));

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources
                .DataQualityUncertain,
            result.Source);
    }

    [Fact]
    public void CandidateMismatch_NaoEmiteNovaAnomalia_MasPreservaEvidencia()
    {
        var result =
            _factory.Create(
                true,
                Context(
                    madCandidateVote: true));

        Assert.False(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources
                .CandidateMismatch,
            result.Source);
    }

    [Fact]
    public void ContinuationSegura_SuprimeAnomaliaENovaEvidencia()
    {
        var result =
            _factory.Create(
                true,
                Context());

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources
                .StableUnresolvedContinuation,
            result.Source);
    }

    [Fact]
    public void EwmaIsolado_NaoDestroiContinuation()
    {
        var result =
            _factory.Create(
                true,
                Context(
                    ewmaCandidateVote: true));

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Contains(
            "EwmaOnlyTemporalMismatch",
            result.Reasons);
    }

    [Fact]
    public void CandidateNaoPronto_PreservaComoNewAnomaly()
    {
        var result =
            _factory.Create(
                true,
                Context(
                    candidateBaselineReady: false));

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.NewAnomaly,
            result.Source);
        Assert.Contains(
            "CandidateNotReady",
            result.Reasons);
    }

    [Fact]
    public void LegacyNormal_IncreasingNoiseStarted_EmiteNovaAnomalia()
    {
        var result =
            _factory.Create(
                false,
                Context(
                    increasingNoise: true,
                    increasingNoiseStarted: true));

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.NewAnomaly,
            result.Source);
        Assert.Contains(
            "IncreasingNoiseDetected",
            result.Reasons);
        Assert.Contains(
            "IndependentVarianceExpansionEvidence",
            result.Reasons);
    }

    [Fact]
    public void LegacyNormal_IncreasingNoiseContinuation_NaoReemite()
    {
        var result =
            _factory.Create(
                false,
                Context(
                    increasingNoise: true,
                    increasingNoiseContinuation: true));

        Assert.True(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            "IncreasingNoiseContinuation",
            result.Source);
        Assert.Contains(
            "IncreasingNoiseStillActive",
            result.Reasons);
    }

    [Fact]
    public void LegacyAnomalo_DuranteIncreasingNoise_NaoEhEngolido()
    {
        var result =
            _factory.Create(
                true,
                Context(
                    increasingNoise: true,
                    increasingNoiseContinuation: true,
                    regimeState: "StableKnownRegime"));

        Assert.True(result.EmitAnomaly);
        Assert.True(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.NewAnomaly,
            result.Source);
    }

    [Fact]
    public void LegacyNormal_SemIncreasingNoise_PreservaComportamentoAnterior()
    {
        var result =
            _factory.Create(
                false,
                Context(
                    increasingNoise: false));

        Assert.False(result.EmitAnomaly);
        Assert.False(result.EmitNewEvidence);
        Assert.Equal(
            RuntimeFunctionalDecisionSources.LegacyNormal,
            result.Source);
        Assert.Contains(
            "LegacyFinalAnomalyFalse",
            result.Reasons);
    }

    private static RuntimeFunctionalDecisionContext Context(
        bool candidateBaselineReady = true,
        bool madCandidateVote = false,
        bool zCandidateVote = false,
        bool ewmaCandidateVote = false,
        bool criticalGap = false,
        bool zActiveVote = false,
        double zActiveScore = 0,
        bool madActiveVote = false,
        double madActiveScore = 0,
        bool rateActiveVote = false,
        double rateActiveScore = 0,
        double legacyScoreNormalized = 0,
        bool increasingNoise = false,
        bool increasingNoiseStarted = false,
        bool increasingNoiseContinuation = false,
        string regimeState = "StableUnresolvedDeviation")
    {
        return new RuntimeFunctionalDecisionContext(
            IncidentId: "INC-1",
            CandidateBaselineReady:
                candidateBaselineReady,
            RegimeState:
                regimeState,
            AgainstActive:
                "Abnormal",
            MadCandidateVote:
                madCandidateVote,
            ZCandidateVote:
                zCandidateVote,
            EwmaCandidateVote:
                ewmaCandidateVote,
            CriticalGap:
                criticalGap,
            OperationalBaselineChanged:
                false,
            Promotion:
                false)
        {
            ZActiveVote = zActiveVote,
            ZActiveScore = zActiveScore,
            MadActiveVote = madActiveVote,
            MadActiveScore = madActiveScore,
            RateActiveVote = rateActiveVote,
            RateActiveScore = rateActiveScore,
            LegacyScoreNormalized = legacyScoreNormalized,
            IncreasingNoise = increasingNoise,
            IncreasingNoiseStarted = increasingNoiseStarted,
            IncreasingNoiseContinuation = increasingNoiseContinuation
        };
    }
}






