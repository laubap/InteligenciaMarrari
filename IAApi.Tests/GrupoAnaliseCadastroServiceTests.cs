using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

public sealed class GrupoAnaliseCadastroServiceTests
{
    [Fact]
    public async Task CriarAsync_DevePersistirGrupoETagsNormalizadas()
    {
        await using var fixture = await Fixture.CreateAsync();

        var grupo = await fixture.Service.CriarAsync(new GrupoAnaliseSalvarRequest
        {
            ClienteId = " CLIENTE-A ",
            Nome = " DRYER_AIRFLOW ",
            MinimumAvailableTags = 2,
            MaxLagMinutes = 3,
            Tags =
            [
                new() { TagName = " DRYER.AIR_FLOW_SENSOR " },
                new() { TagName = "DRYER.AIR_FLOW_REFERENCE", Role = "Reference" }
            ]
        });

        Assert.True(grupo.Id > 0);
        Assert.Equal("CLIENTE-A", grupo.ClienteId);
        Assert.Equal("DRYER_AIRFLOW", grupo.Nome);
        Assert.Equal(2, grupo.Tags.Count);
        Assert.Equal("Mahalanobis", grupo.Algoritmo);

        var persistido = await fixture.Context.GruposAnaliseIa
            .Include(x => x.Tags)
            .SingleAsync();

        Assert.Equal(2, persistido.Tags.Count);
        Assert.Equal(2, persistido.MinimumAvailableTags);
        Assert.Equal(3, persistido.MaxLagMinutes);
    }

    [Fact]
    public async Task CriarAsync_DeveRejeitarMinimumMaiorQueQuantidadeDeTags()
    {
        await using var fixture = await Fixture.CreateAsync();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fixture.Service.CriarAsync(new GrupoAnaliseSalvarRequest
            {
                ClienteId = "CLIENTE-A",
                Nome = "INVALIDO",
                MinimumAvailableTags = 3,
                Tags =
                [
                    new() { TagName = "TAG-A" },
                    new() { TagName = "TAG-B" }
                ]
            }));

        Assert.Contains("não pode ser maior", exception.Message);
        Assert.Empty(fixture.Context.GruposAnaliseIa);
    }

    [Fact]
    public async Task CriarAsync_DeveRejeitarTagDuplicadaIgnorandoMaiusculasMinusculas()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.CriarAsync(new GrupoAnaliseSalvarRequest
            {
                ClienteId = "CLIENTE-A",
                Nome = "DUPLICADO",
                Tags =
                [
                    new() { TagName = "TAG-A" },
                    new() { TagName = "tag-a" }
                ]
            }));

        Assert.Empty(fixture.Context.GruposAnaliseIa);
    }

    [Fact]
    public async Task AtualizarAsync_DeveSubstituirTagsEParametrosDoGrupo()
    {
        await using var fixture = await Fixture.CreateAsync();

        var criado = await fixture.Service.CriarAsync(new GrupoAnaliseSalvarRequest
        {
            ClienteId = "CLIENTE-A",
            Nome = "GRUPO",
            Tags =
            [
                new() { TagName = "TAG-A" },
                new() { TagName = "TAG-B" }
            ]
        });

        var atualizado = await fixture.Service.AtualizarAsync(
            criado.Id,
            new GrupoAnaliseSalvarRequest
            {
                ClienteId = "CLIENTE-A",
                Nome = "GRUPO",
                MinimumAvailableTags = 2,
                MaxLagMinutes = 5,
                Tags =
                [
                    new() { TagName = "TAG-A" },
                    new() { TagName = "TAG-C" }
                ]
            });

        Assert.NotNull(atualizado);
        Assert.Equal(5, atualizado!.MaxLagMinutes);
        Assert.Equal(
            new[] { "TAG-A", "TAG-C" },
            atualizado.Tags.Select(x => x.TagName).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task AtualizarAsync_DeveInvalidarModeloTreinadoDaConfiguracaoAnterior()
    {
        await using var fixture = await Fixture.CreateAsync();

        var criado = await fixture.Service.CriarAsync(new GrupoAnaliseSalvarRequest
        {
            ClienteId = "CLIENTE-A",
            Nome = "GRUPO-COM-MODELO",
            Tags =
            [
                new() { TagName = "TAG-A" },
                new() { TagName = "TAG-B" }
            ]
        });

        var caminhoModelo = fixture.ModelPersistence.ObterCaminhoModelo(
            "CLIENTE-A",
            "GRUPO-COM-MODELO");

        Directory.CreateDirectory(Path.GetDirectoryName(caminhoModelo)!);
        await File.WriteAllTextAsync(caminhoModelo, "{\"modeloAntigo\":true}");
        Assert.True(File.Exists(caminhoModelo));

        await fixture.Service.AtualizarAsync(
            criado.Id,
            new GrupoAnaliseSalvarRequest
            {
                ClienteId = "CLIENTE-A",
                Nome = "GRUPO-COM-MODELO",
                MaxLagMinutes = 4,
                Tags =
                [
                    new() { TagName = "TAG-A" },
                    new() { TagName = "TAG-C" }
                ]
            });

        Assert.False(File.Exists(caminhoModelo));
    }


    [Fact]
    public async Task DesativarGrupo_DeveDescartarBufferEImpedirNovosVetores()
    {
        await using var fixture =
            await Fixture.CreateAsync();

        var criado =
            await fixture.Service.CriarAsync(
                new GrupoAnaliseSalvarRequest
                {
                    ClienteId =
                        "CLIENTE-A",

                    Nome =
                        "GRUPO-DESATIVAVEL",

                    Ativo =
                        true,

                    MinimumAvailableTags =
                        2,

                    MaxLagMinutes =
                        2,

                    Tags =
                    [
                        new() { TagName = "TAG-A" },
                        new() { TagName = "TAG-B" }
                    ]
                });

        /*
         * Simula o grupo operando normalmente e deixando
         * uma rodada parcial em memória.
         */
        var bufferAtivo =
            new MultivariateGroupBufferService(
                new GrupoAnaliseService(
                    new DatabaseGrupoAnaliseProvider(
                        fixture.Context)),
                fixture.RuntimeStateStore);

        var leituraInicial =
            await bufferAtivo.AdicionarLeituraAsync(
                "CLIENTE-A",
                "TAG-A",
                minuteIndex: 1000,
                valor: 10);

        Assert.Empty(
            leituraInicial);

        Assert.Equal(
            1,
            fixture.RuntimeStateStore
                .ObterQuantidadeBuffers());

        /*
         * Simula um modelo treinado existente para esse grupo.
         * Ao alterar/desativar a configuração, ele não pode continuar
         * com autoridade operacional.
         */
        var caminhoModelo =
            fixture.ModelPersistence
                .ObterCaminhoModelo(
                    "CLIENTE-A",
                    "GRUPO-DESATIVAVEL");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                caminhoModelo)!);

        await File.WriteAllTextAsync(
            caminhoModelo,
            "{\"modeloAntigo\":true}");

        Assert.True(
            File.Exists(
                caminhoModelo));

        /*
         * Desativa o grupo pelo mesmo serviço usado pelo CRUD operacional.
         */
        var atualizado =
            await fixture.Service.AtualizarAsync(
                criado.Id,
                new GrupoAnaliseSalvarRequest
                {
                    ClienteId =
                        "CLIENTE-A",

                    Nome =
                        "GRUPO-DESATIVAVEL",

                    Ativo =
                        false,

                    MinimumAvailableTags =
                        2,

                    MaxLagMinutes =
                        2,

                    Tags =
                    [
                        new() { TagName = "TAG-A" },
                        new() { TagName = "TAG-B" }
                    ]
                });

        Assert.NotNull(
            atualizado);

        Assert.False(
            atualizado!.Ativo);

        /*
         * A edição precisa invalidar imediatamente qualquer vetor
         * ainda em formação para o cliente.
         */
        Assert.Equal(
            0,
            fixture.RuntimeStateStore
                .ObterQuantidadeBuffers());

        /*
         * O modelo treinado da configuração anterior também deve ser
         * descartado de forma fail-safe.
         */
        Assert.False(
            File.Exists(
                caminhoModelo));

        /*
         * O provider operacional não pode mais expor o grupo.
         */
        var providerDepoisDaDesativacao =
            new DatabaseGrupoAnaliseProvider(
                fixture.Context);

        var gruposAtivos =
            await providerDepoisDaDesativacao
                .CarregarGruposAsync(
                    "CLIENTE-A");

        Assert.Empty(
            gruposAtivos);

        /*
         * Simula um novo scope/request após a desativação.
         * Mesmo recebendo as duas tags, nenhum novo buffer/vetor
         * pode ser criado para o grupo inativo.
         */
        var bufferDepoisDaDesativacao =
            new MultivariateGroupBufferService(
                new GrupoAnaliseService(
                    providerDepoisDaDesativacao),
                fixture.RuntimeStateStore);

        var depoisA =
            await bufferDepoisDaDesativacao
                .AdicionarLeituraAsync(
                    "CLIENTE-A",
                    "TAG-A",
                    minuteIndex: 1001,
                    valor: 10);

        var depoisB =
            await bufferDepoisDaDesativacao
                .AdicionarLeituraAsync(
                    "CLIENTE-A",
                    "TAG-B",
                    minuteIndex: 1001,
                    valor: 20);

        Assert.Empty(
            depoisA);

        Assert.Empty(
            depoisB);

        Assert.Equal(
            0,
            fixture.RuntimeStateStore
                .ObterQuantidadeBuffers());
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "IAApi.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string? WebRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Fixture(
            SqliteConnection connection,
            AppDbContext context,
            MultivariateRuntimeStateStore runtimeStateStore,
            MultivariateModelPersistenceService modelPersistence,
            GrupoAnaliseCadastroService service)
        {
            _connection = connection;
            Context = context;
            RuntimeStateStore = runtimeStateStore;
            ModelPersistence = modelPersistence;
            Service = service;
        }

        public AppDbContext Context { get; }
        public MultivariateRuntimeStateStore RuntimeStateStore { get; }
        public MultivariateModelPersistenceService ModelPersistence { get; }
        public GrupoAnaliseCadastroService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var stateStore = new MultivariateRuntimeStateStore();

            var modelDirectory = Path.Combine(
                Path.GetTempPath(),
                "IAApiTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(modelDirectory);

            var environment = new TestWebHostEnvironment
            {
                ContentRootPath = modelDirectory
            };

            var persistence = new MultivariateModelPersistenceService(environment);
            var service = new GrupoAnaliseCadastroService(
                context,
                stateStore,
                persistence);

            return new Fixture(
                connection,
                context,
                stateStore,
                persistence,
                service);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
