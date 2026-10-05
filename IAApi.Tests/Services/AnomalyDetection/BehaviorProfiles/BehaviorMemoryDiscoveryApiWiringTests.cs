using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles;

public sealed class BehaviorMemoryDiscoveryApiWiringTests
{
    [Fact]
    public void AddBehaviorMemoryDiscovery_DeveResolverWorkflowCompleto()
    {
        using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        connection.Open();

        var services =
            new ServiceCollection();

        services.AddDbContext<AppDbContext>(
            options =>
                options.UseSqlite(
                    connection));

        services.AddSingleton<IHistoricoService>(
            new EmptyHistoricoService());

        services.AddBehaviorMemoryDiscovery();

        using var provider =
            services.BuildServiceProvider();

        using var scope =
            provider.CreateScope();

        var workflow =
            scope.ServiceProvider
                .GetRequiredService<BehaviorDiscoveryWorkflowService>();

        Assert.NotNull(
            workflow);
    }

    [Fact]
    public async Task Controller_PeriodoInvalido_DeveRetornarBadRequest()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow);

        var response =
            await controller.DiscoverAsync(
                "CLIENTE-A",
                "TAG1",
                new BehaviorMemoryDiscoveryRequest
                {
                    Inicio =
                        new DateTime(
                            2026, 9, 2, 0, 0, 0,
                            DateTimeKind.Unspecified),

                    Fim =
                        new DateTime(
                            2026, 9, 1, 0, 0, 0,
                            DateTimeKind.Unspecified)
                },
                CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            response.Result);
    }

    [Fact]
    public async Task Controller_HistoricoVazio_DeveRetornarResumo200()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow);

        var response =
            await controller.DiscoverAsync(
                "CLIENTE-A",
                "TAG1",
                new BehaviorMemoryDiscoveryRequest
                {
                    Inicio =
                        new DateTime(
                            2026, 9, 1, 0, 0, 0,
                            DateTimeKind.Unspecified),

                    Fim =
                        new DateTime(
                            2026, 9, 2, 0, 0, 0,
                            DateTimeKind.Unspecified)
                },
                CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                response.Result);

        var body =
            Assert.IsType<BehaviorMemoryDiscoveryResponse>(
                ok.Value);

        Assert.Equal(
            "CLIENTE-A",
            body.ClienteId);

        Assert.Equal(
            "TAG1",
            body.TagName);

        Assert.Equal(
            0,
            body.SegmentCount);

        Assert.Empty(
            body.Candidates);

        Assert.Empty(
            body.Consolidation.Items);
    }

    private sealed class ApiFixture :
        IAsyncDisposable
    {
        private ApiFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            IServiceScope scope,
            BehaviorDiscoveryWorkflowService workflow)
        {
            Connection =
                connection;

            Provider =
                provider;

            Scope =
                scope;

            Workflow =
                workflow;
        }

        public SqliteConnection Connection { get; }

        public ServiceProvider Provider { get; }

        public IServiceScope Scope { get; }

        public BehaviorDiscoveryWorkflowService Workflow { get; }

        public static async Task<ApiFixture> CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:");

            await connection.OpenAsync();

            var services =
                new ServiceCollection();

            services.AddDbContext<AppDbContext>(
                options =>
                    options.UseSqlite(
                        connection));

            services.AddSingleton<IHistoricoService>(
                new EmptyHistoricoService());

            services.AddBehaviorMemoryDiscovery();

            var provider =
                services.BuildServiceProvider();

            var scope =
                provider.CreateScope();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            await context.Database
                .EnsureCreatedAsync();

            var workflow =
                scope.ServiceProvider
                    .GetRequiredService<BehaviorDiscoveryWorkflowService>();

            return new ApiFixture(
                connection,
                provider,
                scope,
                workflow);
        }

        public async ValueTask DisposeAsync()
        {
            Scope.Dispose();
            await Provider.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class EmptyHistoricoService :
        IHistoricoService
    {
        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return new List<LeituraBruta>();
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return new List<LeituraBruta>();
        }
    }
}

