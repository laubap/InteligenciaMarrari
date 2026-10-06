using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
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
                fixture.Workflow,
                fixture.ClassificationService);

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
                fixture.Workflow,
                fixture.ClassificationService);

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

    [Fact]
    public async Task Controller_ClassificarKnownComoNormal_DeveRetornar200EPersistir()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        await fixture.AddBehaviorAsync(
            "CLIENTE-A",
            "TAG1",
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            TagComportamentoIa.ClassificacaoUndefined);

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow,
                fixture.ClassificationService);

        var response =
            await controller.ClassifyAsync(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                new BehaviorMemoryClassificationRequest
                {
                    Classification =
                        TagComportamentoIa.ClassificacaoNormal
                },
                CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                response.Result);

        var body =
            Assert.IsType<BehaviorMemoryClassificationResult>(
                ok.Value);

        Assert.True(
            body.Changed);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            body.PreviousClassification);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoNormal,
            body.Classification);

        var persisted =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync(x =>
                    x.ClienteId == "CLIENTE-A" &&
                    x.TagName == "TAG1" &&
                    x.Codigo == "BEH-001");

        Assert.Equal(
            TagComportamentoIa.ClassificacaoNormal,
            persisted.Classificacao);
    }

    [Fact]
    public async Task Controller_ClassificarKnownComoKnownAbnormal_DeveRetornar200()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        await fixture.AddBehaviorAsync(
            "CLIENTE-A",
            "TAG1",
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            TagComportamentoIa.ClassificacaoUndefined);

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow,
                fixture.ClassificationService);

        var response =
            await controller.ClassifyAsync(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                new BehaviorMemoryClassificationRequest
                {
                    Classification =
                        TagComportamentoIa.ClassificacaoKnownAbnormal
                },
                CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                response.Result);

        var body =
            Assert.IsType<BehaviorMemoryClassificationResult>(
                ok.Value);

        Assert.True(
            body.Changed);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoKnownAbnormal,
            body.Classification);
    }

    [Fact]
    public async Task Controller_RepetirMesmaClassificacao_DeveSerIdempotente()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        await fixture.AddBehaviorAsync(
            "CLIENTE-A",
            "TAG1",
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            TagComportamentoIa.ClassificacaoNormal);

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow,
                fixture.ClassificationService);

        var response =
            await controller.ClassifyAsync(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                new BehaviorMemoryClassificationRequest
                {
                    Classification =
                        TagComportamentoIa.ClassificacaoNormal
                },
                CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                response.Result);

        var body =
            Assert.IsType<BehaviorMemoryClassificationResult>(
                ok.Value);

        Assert.False(
            body.Changed);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoNormal,
            body.PreviousClassification);

        Assert.Equal(
            TagComportamentoIa.ClassificacaoNormal,
            body.Classification);
    }

    [Fact]
    public async Task Controller_ClassificarCandidate_DeveRetornarBadRequest()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        await fixture.AddBehaviorAsync(
            "CLIENTE-A",
            "TAG1",
            "CAND-001",
            TagComportamentoIa.StatusCandidate,
            TagComportamentoIa.ClassificacaoUndefined);

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow,
                fixture.ClassificationService);

        var response =
            await controller.ClassifyAsync(
                "CLIENTE-A",
                "TAG1",
                "CAND-001",
                new BehaviorMemoryClassificationRequest
                {
                    Classification =
                        TagComportamentoIa.ClassificacaoNormal
                },
                CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            response.Result);

        var persisted =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync(x =>
                    x.ClienteId == "CLIENTE-A" &&
                    x.TagName == "TAG1" &&
                    x.Codigo == "CAND-001");

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            persisted.Classificacao);
    }

    [Fact]
    public async Task Controller_BehaviorInexistente_DeveRetornarNotFound()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow,
                fixture.ClassificationService);

        var response =
            await controller.ClassifyAsync(
                "CLIENTE-A",
                "TAG1",
                "BEH-999",
                new BehaviorMemoryClassificationRequest
                {
                    Classification =
                        TagComportamentoIa.ClassificacaoNormal
                },
                CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(
            response.Result);
    }

    [Fact]
    public async Task Controller_ClassificacaoInvalida_DeveRetornarBadRequest()
    {
        await using var fixture =
            await ApiFixture.CreateAsync();

        await fixture.AddBehaviorAsync(
            "CLIENTE-A",
            "TAG1",
            "BEH-001",
            TagComportamentoIa.StatusKnown,
            TagComportamentoIa.ClassificacaoUndefined);

        var controller =
            new BehaviorMemoryDiscoveryController(
                fixture.Workflow,
                fixture.ClassificationService);

        var response =
            await controller.ClassifyAsync(
                "CLIENTE-A",
                "TAG1",
                "BEH-001",
                new BehaviorMemoryClassificationRequest
                {
                    Classification =
                        "QualquerCoisa"
                },
                CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            response.Result);

        var persisted =
            await fixture.Context.TagsComportamentosIa
                .SingleAsync(x =>
                    x.ClienteId == "CLIENTE-A" &&
                    x.TagName == "TAG1" &&
                    x.Codigo == "BEH-001");

        Assert.Equal(
            TagComportamentoIa.ClassificacaoUndefined,
            persisted.Classificacao);
    }

    private sealed class ApiFixture :
        IAsyncDisposable
    {
        private ApiFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            IServiceScope scope,
            AppDbContext context,
            BehaviorDiscoveryWorkflowService workflow,
            BehaviorMemoryClassificationService classificationService)
        {
            Connection =
                connection;

            Provider =
                provider;

            Scope =
                scope;

            Context =
                context;

            Workflow =
                workflow;

            ClassificationService =
                classificationService;
        }

        public SqliteConnection Connection { get; }

        public ServiceProvider Provider { get; }

        public IServiceScope Scope { get; }

        public AppDbContext Context { get; }

        public BehaviorDiscoveryWorkflowService Workflow { get; }

        public BehaviorMemoryClassificationService ClassificationService { get; }

        public async Task AddBehaviorAsync(
            string clienteId,
            string tagName,
            string codigo,
            string status,
            string classificacao)
        {
            Context.TagsComportamentosIa.Add(
                new TagComportamentoIa
                {
                    ClienteId =
                        clienteId,

                    TagName =
                        tagName,

                    Codigo =
                        codigo,

                    Status =
                        status,

                    Origem =
                        TagComportamentoIa.OrigemAutomatic,

                    Classificacao =
                        classificacao,

                    PrimeiraObservacao =
                        new DateTime(
                            2026, 9, 1, 0, 0, 0,
                            DateTimeKind.Utc),

                    UltimaObservacao =
                        new DateTime(
                            2026, 9, 1, 1, 0, 0,
                            DateTimeKind.Utc),

                    QuantidadeOcorrencias =
                        1,

                    TotalAmostras =
                        60,

                    Confianca =
                        90,

                    DataCriacao =
                        DateTime.UtcNow
                });

            await Context.SaveChangesAsync();
        }

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

            var classificationService =
                scope.ServiceProvider
                    .GetRequiredService<BehaviorMemoryClassificationService>();

            return new ApiFixture(
                connection,
                provider,
                scope,
                context,
                workflow,
                classificationService);
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

