using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeRolloutEvolutionTests
{
    [Fact]
    public async Task GetEvolutionAsync_SemSnapshots_RetornaResumoVazio()
    {
        await using var fixture =
            await CreateFixtureAsync();

        var service =
            new RuntimeRolloutEvolutionService(
                fixture.Context);

        var result =
            await service.GetEvolutionAsync();

        Assert.Equal(0, result.SnapshotCount);
        Assert.Equal(0, result.ProcessRestartCount);
        Assert.Equal(0, result.ObservationSessionCount);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task GetEvolutionAsync_CalculaDeltasNaMesmaSessao()
    {
        await using var fixture =
            await CreateFixtureAsync();

        fixture.Context.RuntimeRolloutSnapshots.AddRange(
            Snapshot(1, At(20, 0), 10, 5, FirstAt(19, 55)),
            Snapshot(2, At(20, 5), 25, 10, FirstAt(19, 55)));

        await fixture.Context.SaveChangesAsync();

        var result =
            await new RuntimeRolloutEvolutionService(
                fixture.Context)
                .GetEvolutionAsync();

        Assert.Equal(15, result.ObservedDecisionGrowth);
        Assert.Equal(5, result.ObservedObservationMinutesGrowth);
        Assert.Equal(0, result.ProcessRestartCount);
        Assert.Equal(1, result.ObservationSessionCount);

        Assert.Single(result.Changes);
        Assert.False(result.Changes[0].RestartBoundary);
        Assert.Equal(15, result.Changes[0].DecisionDelta);
        Assert.Equal(5, result.Changes[0].ObservationMinutesDelta);
    }

    [Fact]
    public async Task GetEvolutionAsync_RestartNaoGeraDeltaNegativo()
    {
        await using var fixture =
            await CreateFixtureAsync();

        fixture.Context.RuntimeRolloutSnapshots.AddRange(
            Snapshot(1, At(20, 0), 24, 9.5, FirstAt(19, 50)),
            Snapshot(2, At(21, 0), 0, 0, null));

        await fixture.Context.SaveChangesAsync();

        var result =
            await new RuntimeRolloutEvolutionService(
                fixture.Context)
                .GetEvolutionAsync();

        Assert.Equal(1, result.ProcessRestartCount);
        Assert.Equal(2, result.ObservationSessionCount);

        Assert.Single(result.Changes);
        Assert.True(result.Changes[0].RestartBoundary);
        Assert.Null(result.Changes[0].DecisionDelta);
        Assert.Null(result.Changes[0].ObservationMinutesDelta);

        Assert.Equal(0, result.ObservedDecisionGrowth);
        Assert.Equal(0, result.ObservedObservationMinutesGrowth);
    }

    [Fact]
    public async Task GetEvolutionAsync_SomaCrescimentoSomenteDentroDasSessoes()
    {
        await using var fixture =
            await CreateFixtureAsync();

        fixture.Context.RuntimeRolloutSnapshots.AddRange(
            Snapshot(1, At(20, 0), 0, 0, null),
            Snapshot(2, At(20, 5), 13, 4.4, FirstAt(20, 1)),
            Snapshot(3, At(20, 10), 24, 9.5, FirstAt(20, 1)),
            Snapshot(4, At(21, 0), 0, 0, null),
            Snapshot(5, At(21, 5), 7, 1.7, FirstAt(21, 3)));

        await fixture.Context.SaveChangesAsync();

        var result =
            await new RuntimeRolloutEvolutionService(
                fixture.Context)
                .GetEvolutionAsync();

        Assert.Equal(31, result.ObservedDecisionGrowth);
        Assert.Equal(1, result.ProcessRestartCount);
        Assert.Equal(2, result.ObservationSessionCount);
    }

    [Fact]
    public async Task GetEvolutionAsync_IdentificaPrimeiroReadyToExpand()
    {
        await using var fixture =
            await CreateFixtureAsync();

        fixture.Context.RuntimeRolloutSnapshots.AddRange(
            Snapshot(1, At(20, 0), 20, 30, FirstAt(19, 30), "Warning", false),
            Snapshot(2, At(20, 5), 30, 35, FirstAt(19, 30), "Healthy", true),
            Snapshot(3, At(20, 10), 40, 40, FirstAt(19, 30), "Healthy", true));

        await fixture.Context.SaveChangesAsync();

        var result =
            await new RuntimeRolloutEvolutionService(
                fixture.Context)
                .GetEvolutionAsync();

        Assert.Equal(
            At(20, 5),
            result.FirstReadyToExpandAtUtc);

        Assert.True(
            result.Changes[0].BecameReadyToExpand);
    }

    [Fact]
    public async Task GetEvolution_EndpointDesabilitado_Retorna404()
    {
        await using var fixture =
            await CreateFixtureAsync();

        var controller =
            new RuntimeRolloutEvolutionController(
                new RuntimeRolloutEvolutionService(
                    fixture.Context),
                Options.Create(
                    new RuntimeDecisionMetricsEndpointOptions
                    {
                        Enabled = false
                    }));

        var result =
            await controller.GetEvolution();

        Assert.IsType<NotFoundResult>(
            result.Result);
    }

    [Fact]
    public async Task GetEvolution_EndpointHabilitado_Retorna200()
    {
        await using var fixture =
            await CreateFixtureAsync();

        var controller =
            new RuntimeRolloutEvolutionController(
                new RuntimeRolloutEvolutionService(
                    fixture.Context),
                Options.Create(
                    new RuntimeDecisionMetricsEndpointOptions
                    {
                        Enabled = true
                    }));

        var result =
            await controller.GetEvolution();

        Assert.IsType<OkObjectResult>(
            result.Result);
    }

    private static RuntimeRolloutSnapshot Snapshot(
        long id,
        DateTimeOffset at,
        long total,
        double observation,
        DateTimeOffset? firstDecisionAtUtc,
        string status = "Warning",
        bool ready = false)
    {
        return new RuntimeRolloutSnapshot
        {
            Id = id,
            GeneratedAtUtc = at,
            RuntimeDecisionEnabled = true,
            CanaryOnly = true,
            AllowedClientIdsJson = "[\"PK7\"]",
            Status = status,
            ReadyToExpand = ready,
            TotalDecisions = total,
            ObservationMinutes = observation,
            LegacyFallbackRate = 0,
            DataQualityUncertainRate = 0,
            CandidateMismatchRate = 0,
            StableUnresolvedContinuationRate = 0,
            ReasonsJson = "[]",
            FirstDecisionAtUtc = firstDecisionAtUtc,
            LastDecisionAtUtc =
                firstDecisionAtUtc?.AddMinutes(observation)
        };
    }

    private static DateTimeOffset At(
        int hour,
        int minute)
    {
        return new DateTimeOffset(
            2026, 8, 25,
            hour, minute, 0,
            TimeSpan.Zero);
    }

    private static DateTimeOffset FirstAt(
        int hour,
        int minute)
    {
        return At(hour, minute);
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        return new Fixture(
            connection,
            context);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(
            SqliteConnection connection,
            AppDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }

        public AppDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}