using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorMemoryLifecyclePolicyTests
{
    [Fact]
    public void Evaluate_UmaOcorrencia_DevePermanecerCandidate()
    {
        var policy =
            new BehaviorMemoryLifecyclePolicy();

        var candidate =
            CreateCandidate(
                occurrenceCount: 1,
                isRecurrent: false,
                evidenceSpanMinutes: 30);

        var result =
            policy.Evaluate(
                candidate);

        Assert.Equal(
            BehaviorMemoryLifecycle.Candidate,
            result.Lifecycle);

        Assert.False(
            result.EligibleForStableIdentity);

        Assert.False(
            result.RecurrenceSatisfied);
    }

    [Fact]
    public void Evaluate_DuasOcorrenciasRecorrentes_DevePermanecerCandidatePorEvidenciaInsuficiente()
    {
        var policy =
            new BehaviorMemoryLifecyclePolicy();

        var candidate =
            CreateCandidate(
                occurrenceCount: 2,
                isRecurrent: true,
                evidenceSpanMinutes: 180);

        var result =
            policy.Evaluate(
                candidate);

        Assert.Equal(
            BehaviorMemoryLifecycle.Candidate,
            result.Lifecycle);

        Assert.True(
            result.RecurrenceSatisfied);

        Assert.False(
            result.OccurrenceThresholdSatisfied);

        Assert.False(
            result.EligibleForStableIdentity);
    }

    [Fact]
    public void Evaluate_TresOcorrenciasComHorizonteSuficiente_DevePromoverParaKnown()
    {
        var policy =
            new BehaviorMemoryLifecyclePolicy();

        var candidate =
            CreateCandidate(
                occurrenceCount: 3,
                isRecurrent: true,
                evidenceSpanMinutes: 180);

        var result =
            policy.Evaluate(
                candidate);

        Assert.Equal(
            BehaviorMemoryLifecycle.Known,
            result.Lifecycle);

        Assert.True(
            result.EligibleForStableIdentity);

        Assert.True(
            result.RecurrenceSatisfied);

        Assert.True(
            result.OccurrenceThresholdSatisfied);

        Assert.True(
            result.EvidenceSpanSatisfied);
    }

    [Fact]
    public void Evaluate_TresOcorrenciasConcentradasEmPoucoTempo_DevePermanecerCandidate()
    {
        var policy =
            new BehaviorMemoryLifecyclePolicy();

        var candidate =
            CreateCandidate(
                occurrenceCount: 3,
                isRecurrent: true,
                evidenceSpanMinutes: 45);

        var result =
            policy.Evaluate(
                candidate);

        Assert.Equal(
            BehaviorMemoryLifecycle.Candidate,
            result.Lifecycle);

        Assert.True(
            result.OccurrenceThresholdSatisfied);

        Assert.False(
            result.EvidenceSpanSatisfied);

        Assert.False(
            result.EligibleForStableIdentity);
    }

    [Fact]
    public void Constructor_OpcoesInvalidas_DeveFalhar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new BehaviorMemoryLifecyclePolicy(
                    new BehaviorMemoryLifecycleOptions
                    {
                        MinimumOccurrencesForKnown =
                            1
                    }));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new BehaviorMemoryLifecyclePolicy(
                    new BehaviorMemoryLifecycleOptions
                    {
                        MinimumEvidenceSpanMinutesForKnown =
                            -1
                    }));
    }

    private static BehaviorDiscoveryCandidate
        CreateCandidate(
            int occurrenceCount,
            bool isRecurrent,
            double evidenceSpanMinutes)
    {
        var start =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        return new BehaviorDiscoveryCandidate
        {
            GroupIndex =
                0,

            OccurrenceCount =
                occurrenceCount,

            IsRecurrent =
                isRecurrent,

            FirstOccurrenceStart =
                start,

            LastOccurrenceEnd =
                start.AddMinutes(
                    evidenceSpanMinutes),

            RepresentativeProfile =
                new BehaviorCandidateRepresentativeProfile(),

            Reason =
                "Teste de lifecycle."
        };
    }
}
