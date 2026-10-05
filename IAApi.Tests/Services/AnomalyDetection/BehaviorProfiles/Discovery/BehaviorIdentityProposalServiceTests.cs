using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorIdentityProposalServiceTests
{
    [Fact]
    public void Create_CandidateNaoDeveReceberIdentidade()
    {
        var service =
            new BehaviorIdentityProposalService();

        var evaluation =
            CreateEvaluation(
                groupIndex: 0,
                lifecycle:
                    BehaviorMemoryLifecycle.Candidate,
                eligible: false,
                startHour: 0);

        var proposal =
            Assert.Single(
                service.Create(
                    new[]
                    {
                        evaluation
                    }));

        Assert.False(
            proposal.HasStableIdentityProposal);

        Assert.Null(
            proposal.ProposedIdentity);
    }

    [Fact]
    public void Create_KnownElegivel_DeveReceberBeh001()
    {
        var service =
            new BehaviorIdentityProposalService();

        var evaluation =
            CreateEvaluation(
                groupIndex: 0,
                lifecycle:
                    BehaviorMemoryLifecycle.Known,
                eligible: true,
                startHour: 0);

        var proposal =
            Assert.Single(
                service.Create(
                    new[]
                    {
                        evaluation
                    }));

        Assert.True(
            proposal.HasStableIdentityProposal);

        Assert.Equal(
            "BEH-001",
            proposal.ProposedIdentity);
    }

    [Fact]
    public void Create_MultiplosKnown_DeveOrdenarPelaPrimeiraOcorrencia()
    {
        var service =
            new BehaviorIdentityProposalService();

        var later =
            CreateEvaluation(
                groupIndex: 9,
                lifecycle:
                    BehaviorMemoryLifecycle.Known,
                eligible: true,
                startHour: 10);

        var earlier =
            CreateEvaluation(
                groupIndex: 3,
                lifecycle:
                    BehaviorMemoryLifecycle.Known,
                eligible: true,
                startHour: 2);

        var proposals =
            service.Create(
                new[]
                {
                    later,
                    earlier
                });

        Assert.Equal(
            2,
            proposals.Count);

        Assert.Equal(
            3,
            proposals[0].GroupIndex);

        Assert.Equal(
            "BEH-001",
            proposals[0].ProposedIdentity);

        Assert.Equal(
            9,
            proposals[1].GroupIndex);

        Assert.Equal(
            "BEH-002",
            proposals[1].ProposedIdentity);
    }

    [Fact]
    public void Create_IdentidadesReservadas_DeveContinuarDepoisDoMaiorCodigo()
    {
        var service =
            new BehaviorIdentityProposalService();

        var evaluation =
            CreateEvaluation(
                groupIndex: 0,
                lifecycle:
                    BehaviorMemoryLifecycle.Known,
                eligible: true,
                startHour: 0);

        var proposal =
            Assert.Single(
                service.Create(
                    new[]
                    {
                        evaluation
                    },
                    new[]
                    {
                        "BEH-001",
                        "BEH-003",
                        "OUTRO-999"
                    }));

        Assert.Equal(
            "BEH-004",
            proposal.ProposedIdentity);
    }

    [Fact]
    public void Constructor_OpcoesInvalidas_DeveFalhar()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new BehaviorIdentityProposalService(
                    new BehaviorIdentityProposalOptions
                    {
                        Prefix =
                            ""
                    }));

        Assert.Throws<ArgumentException>(
            () =>
                new BehaviorIdentityProposalService(
                    new BehaviorIdentityProposalOptions
                    {
                        Prefix =
                            "BEH-X"
                    }));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new BehaviorIdentityProposalService(
                    new BehaviorIdentityProposalOptions
                    {
                        NumericPadding =
                            0
                    }));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new BehaviorIdentityProposalService(
                    new BehaviorIdentityProposalOptions
                    {
                        StartingSequence =
                            0
                    }));
    }

    private static BehaviorMemoryLifecycleEvaluation
        CreateEvaluation(
            int groupIndex,
            BehaviorMemoryLifecycle lifecycle,
            bool eligible,
            int startHour)
    {
        var start =
            new DateTime(
                2026,
                9,
                1,
                startHour,
                0,
                0,
                DateTimeKind.Utc);

        var candidate =
            new BehaviorDiscoveryCandidate
            {
                GroupIndex =
                    groupIndex,

                OccurrenceCount =
                    eligible
                        ? 3
                        : 1,

                IsRecurrent =
                    eligible,

                FirstOccurrenceStart =
                    start,

                LastOccurrenceEnd =
                    start.AddHours(
                        2),

                RepresentativeProfile =
                    new BehaviorCandidateRepresentativeProfile()
            };

        return new BehaviorMemoryLifecycleEvaluation
        {
            GroupIndex =
                groupIndex,

            Lifecycle =
                lifecycle,

            EligibleForStableIdentity =
                eligible,

            OccurrenceCount =
                candidate.OccurrenceCount,

            EvidenceSpanMinutes =
                120,

            RecurrenceSatisfied =
                candidate.IsRecurrent,

            OccurrenceThresholdSatisfied =
                eligible,

            EvidenceSpanSatisfied =
                eligible,

            Candidate =
                candidate
        };
    }
}
