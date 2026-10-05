using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.Discovery;

public sealed class BehaviorDiscoveryCandidateFactoryTests
{
    [Fact]
    public void Create_GrupoComDuasOcorrencias_DeveMarcarComoRecorrente()
    {
        var factory =
            new BehaviorDiscoveryCandidateFactory();

        var grouping =
            CreateGrouping(
                new[]
                {
                    CreateOccurrence(
                        episode: 0,
                        median: 40.0,
                        robustScale: 0.6),

                    CreateOccurrence(
                        episode: 2,
                        median: 40.4,
                        robustScale: 0.8)
                });

        var candidate =
            Assert.Single(
                factory.Create(
                    grouping));

        Assert.True(
            candidate.IsRecurrent);

        Assert.Equal(
            2,
            candidate.OccurrenceCount);

        Assert.Equal(
            40.2,
            candidate.RepresentativeProfile.Mediana,
            precision: 5);

        Assert.Equal(
            0.7,
            candidate.RepresentativeProfile.RobustScale,
            precision: 5);
    }

    [Fact]
    public void Create_GrupoComUmaOcorrencia_DeveContinuarCandidatoMasNaoRecorrente()
    {
        var factory =
            new BehaviorDiscoveryCandidateFactory();

        var grouping =
            CreateGrouping(
                new[]
                {
                    CreateOccurrence(
                        episode: 0,
                        median: 40,
                        robustScale: 0.7)
                });

        var candidate =
            Assert.Single(
                factory.Create(
                    grouping));

        Assert.False(
            candidate.IsRecurrent);

        Assert.Equal(
            1,
            candidate.OccurrenceCount);

        Assert.Single(
            candidate.Occurrences);
    }

    [Fact]
    public void Create_AssinaturaRepresentativa_DeveUsarMedianaEntreOcorrencias()
    {
        var factory =
            new BehaviorDiscoveryCandidateFactory();

        var grouping =
            CreateGrouping(
                new[]
                {
                    CreateOccurrence(
                        episode: 0,
                        median: 40,
                        robustScale: 0.5,
                        typicalStep: 0.10),

                    CreateOccurrence(
                        episode: 1,
                        median: 41,
                        robustScale: 0.7,
                        typicalStep: 0.20),

                    CreateOccurrence(
                        episode: 2,
                        median: 100,
                        robustScale: 4.0,
                        typicalStep: 8.00)
                });

        var candidate =
            Assert.Single(
                factory.Create(
                    grouping));

        Assert.Equal(
            41,
            candidate.RepresentativeProfile.Mediana,
            precision: 5);

        Assert.Equal(
            0.7,
            candidate.RepresentativeProfile.RobustScale,
            precision: 5);

        Assert.Equal(
            0.20,
            candidate.RepresentativeProfile.TypicalAbsoluteStep,
            precision: 5);
    }

    [Fact]
    public void Create_DevePreservarPrimeiraEUltimaOcorrencia()
    {
        var factory =
            new BehaviorDiscoveryCandidateFactory();

        var first =
            CreateOccurrence(
                episode: 0,
                median: 40,
                robustScale: 0.7,
                startHour: 2);

        var second =
            CreateOccurrence(
                episode: 1,
                median: 40.2,
                robustScale: 0.8,
                startHour: 10);

        var grouping =
            CreateGrouping(
                new[]
                {
                    second,
                    first
                });

        var candidate =
            Assert.Single(
                factory.Create(
                    grouping));

        Assert.Equal(
            first.Inicio,
            candidate.FirstOccurrenceStart);

        Assert.Equal(
            second.Fim,
            candidate.LastOccurrenceEnd);

        Assert.Equal(
            new[] { 0, 1 },
            candidate.Occurrences
                .Select(x =>
                    x.EpisodeIndex));
    }

    [Fact]
    public void Constructor_MinimoRecorrenciaMenorQueDois_DeveFalhar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new BehaviorDiscoveryCandidateFactory(
                    new BehaviorCandidateFormationOptions
                    {
                        MinimumOccurrencesForRecurrence =
                            1
                    }));
    }

    private static BehaviorCandidateEpisodeGroupingResult
        CreateGrouping(
            IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> occurrences)
    {
        return new BehaviorCandidateEpisodeGroupingResult
        {
            Groups =
                new[]
                {
                    new BehaviorCandidateEpisodeSimilarityGroup
                    {
                        GroupIndex =
                            0,

                        Members =
                            occurrences
                    }
                }
        };
    }

    private static BehaviorCandidateEpisodeCharacteristics
        CreateOccurrence(
            int episode,
            double median,
            double robustScale,
            double typicalStep = 0.2,
            int startHour = 0)
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

        return new BehaviorCandidateEpisodeCharacteristics
        {
            SourceSegmentIndex =
                0,

            EpisodeIndex =
                episode,

            Inicio =
                start,

            Fim =
                start.AddMinutes(
                    30),

            IsCharacterizable =
                true,

            Media =
                median,

            Mediana =
                median,

            DesvioPadrao =
                robustScale,

            IntervaloInterquartil =
                robustScale,

            DeadbandRecomendado =
                robustScale,

            ProfileFeatures =
                new BehaviorProfileFeatures
                {
                    RobustScale =
                        robustScale,

                    TypicalAbsoluteStep =
                        typicalStep,

                    ChangeRatio =
                        0.7,

                    ReversalRatio =
                        0.4
                }
        };
    }
}
