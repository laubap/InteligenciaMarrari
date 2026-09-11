using IAApi.Services.AnomalyDetection.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace IAApi.Tests;

public sealed class RuntimeRolloutHistoryTests
{
    [Theory]
    [InlineData(null, 20)]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(25, 25)]
    [InlineData(500, 100)]
    public void NormalizeLimit_AplicaLimitesEsperados(int? input, int expected)
    {
        Assert.Equal(expected, RuntimeRolloutHistoryService.NormalizeLimit(input));
    }

    [Fact]
    public async Task GetLatestAsync_RetornaMaisRecentesPrimeiro()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.Context.RuntimeRolloutSnapshots.AddRange(
            Snapshot(new DateTimeOffset(2026, 8, 24, 20, 10, 0, TimeSpan.Zero), 10),
            Snapshot(new DateTimeOffset(2026, 8, 24, 20, 20, 0, TimeSpan.Zero), 30),
            Snapshot(new DateTimeOffset(2026, 8, 24, 20, 15, 0, TimeSpan.Zero), 20));

        await fixture.Context.SaveChangesAsync();

        var service = new RuntimeRolloutHistoryService(fixture.Context);
        var result = await service.GetLatestAsync(limit: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(30, result[0].TotalDecisions);
        Assert.Equal(20, result[1].TotalDecisions);
    }

    [Fact]
    public async Task GetHistory_EndpointDesabilitado_Retorna404()
    {
        await using var fixture = await CreateFixtureAsync();

        var controller = new RuntimeRolloutHistoryController(
            new RuntimeRolloutHistoryService(fixture.Context),
            Options.Create(new RuntimeDecisionMetricsEndpointOptions { Enabled = false }));

        var result = await controller.GetHistory();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetHistory_EndpointHabilitado_Retorna200()
    {
        await using var fixture = await CreateFixtureAsync();

        fixture.Context.RuntimeRolloutSnapshots.Add(
            Snapshot(DateTimeOffset.UtcNow, 42));

        await fixture.Context.SaveChangesAsync();

        var controller = new RuntimeRolloutHistoryController(
            new RuntimeRolloutHistoryService(fixture.Context),
            Options.Create(new RuntimeDecisionMetricsEndpointOptions { Enabled = true }));

        var result = await controller.GetHistory();

        var ok = Assert.IsType<OkObjectResult>(result.Result);

        var history = Assert.IsAssignableFrom<
            IReadOnlyList<RuntimeRolloutHistoryItemResponse>>(ok.Value);

        Assert.Single(history);
        Assert.Equal(42, history[0].TotalDecisions);
    }

    private static RuntimeRolloutSnapshot Snapshot(
        DateTimeOffset generatedAtUtc,
        long total)
    {
        return new RuntimeRolloutSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            RuntimeDecisionEnabled = true,
            CanaryOnly = true,
            AllowedClientIdsJson = "[\"PK7\"]",
            Status = "Warning",
            ReadyToExpand = false,
            TotalDecisions = total,
            ObservationMinutes = 10,
            LegacyFallbackRate = 0,
            DataQualityUncertainRate = 0,
            CandidateMismatchRate = 0,
            StableUnresolvedContinuationRate = 0,
            ReasonsJson = "[\"InsufficientObservationWindow\"]"
        };
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new Fixture(connection, context);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(SqliteConnection connection, AppDbContext context)
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