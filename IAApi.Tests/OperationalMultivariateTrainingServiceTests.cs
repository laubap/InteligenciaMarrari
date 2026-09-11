using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

public sealed class OperationalMultivariateTrainingServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "IAApiTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TreinarAsync_DeveGerarModeloUsandoHistoricoDoGrupoPersistido()
    {
        Directory.CreateDirectory(_tempRoot);

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var grupo = CriarGrupo("PK7", "GRUPO_TESTE", "PK7.TAG1", "PK7.TAG2");
        context.GruposAnaliseIa.Add(grupo);
        await context.SaveChangesAsync();

        var inicio = new DateTime(2026, 8, 27, 10, 0, 0);
        var historico = new FakeHistoricoService();

        for (var i = 0; i < 20; i++)
        {
            var data = inicio.AddMinutes(i);
            historico.Adicionar("PK7", "PK7.TAG1", data, 10 + i);
            historico.Adicionar("PK7", "PK7.TAG2", data, 20 + (2 * i));
        }

        var persistence = new MultivariateModelPersistenceService(
            new FakeWebHostEnvironment(_tempRoot));

        var provider = new DatabaseGrupoAnaliseProvider(context);
        var grupoService = new GrupoAnaliseService(provider);

        var service = new OperationalMultivariateTrainingService(
            context,
            historico,
            grupoService,
            persistence);

        var result = await service.TreinarAsync(
            grupo.Id,
            "PK7",
            inicio,
            inicio.AddMinutes(19));

        Assert.True(result.ModeloGerado);
        Assert.Equal(20, result.VetoresCompletosUsados);
        Assert.Equal(2, result.Tags.Count);
        Assert.True(File.Exists(result.CaminhoModelo));

        var model = await persistence.CarregarAsync("PK7", "GRUPO_TESTE");

        Assert.NotNull(model);
        Assert.Equal(20, model!.SampleCount);
        Assert.Equal(new[] { "PK7.TAG1", "PK7.TAG2" }, model.Tags);
        Assert.Equal(2, model.MinimumAvailableTags);
    }

    [Fact]
    public async Task TreinarAsync_NaoDeveUsarGrupoDeOutroCliente()
    {
        Directory.CreateDirectory(_tempRoot);

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var grupo = CriarGrupo("CLIENTE-A", "GRUPO", "A.TAG1", "A.TAG2");
        context.GruposAnaliseIa.Add(grupo);
        await context.SaveChangesAsync();

        var persistence = new MultivariateModelPersistenceService(
            new FakeWebHostEnvironment(_tempRoot));

        var service = new OperationalMultivariateTrainingService(
            context,
            new FakeHistoricoService(),
            new GrupoAnaliseService(new DatabaseGrupoAnaliseProvider(context)),
            persistence);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.TreinarAsync(
                grupo.Id,
                "CLIENTE-B",
                new DateTime(2026, 8, 1),
                new DateTime(2026, 8, 2)));
    }

    [Fact]
    public async Task TreinarAsync_DeveFalharQuandoNaoHaHistoricoParaUmaDasTags()
    {
        Directory.CreateDirectory(_tempRoot);

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var grupo = CriarGrupo("PK7", "SEM_DADOS", "PK7.TAG1", "PK7.TAG2");
        context.GruposAnaliseIa.Add(grupo);
        await context.SaveChangesAsync();

        var inicio = new DateTime(2026, 8, 27, 10, 0, 0);
        var historico = new FakeHistoricoService();
        historico.Adicionar("PK7", "PK7.TAG1", inicio, 10);
        historico.Adicionar("PK7", "PK7.TAG1", inicio.AddMinutes(1), 11);

        var persistence = new MultivariateModelPersistenceService(
            new FakeWebHostEnvironment(_tempRoot));

        var service = new OperationalMultivariateTrainingService(
            context,
            historico,
            new GrupoAnaliseService(new DatabaseGrupoAnaliseProvider(context)),
            persistence);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TreinarAsync(
                grupo.Id,
                "PK7",
                inicio,
                inicio.AddMinutes(10)));

        Assert.Contains("PK7.TAG2", ex.Message);
        Assert.False(persistence.ExisteModelo("PK7", "SEM_DADOS"));
    }

    [Fact]
    public async Task TreinarAsync_DeveIgnorarVetoresParciaisENaoTreinarComMenosDeDoisCompletos()
    {
        Directory.CreateDirectory(_tempRoot);

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var grupo = CriarGrupo("PK7", "PARCIAL", "PK7.TAG1", "PK7.TAG2");
        context.GruposAnaliseIa.Add(grupo);
        await context.SaveChangesAsync();

        var inicio = new DateTime(2026, 8, 27, 10, 0, 0);
        var historico = new FakeHistoricoService();

        // Apenas um minuto possui as duas tags; os demais ficam parciais.
        historico.Adicionar("PK7", "PK7.TAG1", inicio, 10);
        historico.Adicionar("PK7", "PK7.TAG2", inicio, 20);
        historico.Adicionar("PK7", "PK7.TAG1", inicio.AddMinutes(1), 11);
        historico.Adicionar("PK7", "PK7.TAG2", inicio.AddMinutes(5), 25);

        var persistence = new MultivariateModelPersistenceService(
            new FakeWebHostEnvironment(_tempRoot));

        var service = new OperationalMultivariateTrainingService(
            context,
            historico,
            new GrupoAnaliseService(new DatabaseGrupoAnaliseProvider(context)),
            persistence);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TreinarAsync(
                grupo.Id,
                "PK7",
                inicio,
                inicio.AddMinutes(10)));

        Assert.False(persistence.ExisteModelo("PK7", "PARCIAL"));
    }

    private static GrupoAnaliseIa CriarGrupo(
        string clienteId,
        string nome,
        params string[] tags)
    {
        return new GrupoAnaliseIa
        {
            ClienteId = clienteId,
            Nome = nome,
            Ativo = true,
            MinimumAvailableTags = 2,
            MaxLagMinutes = 0,
            Algoritmo = "Mahalanobis",
            Tags = tags.Select(tag => new GrupoAnaliseTagIa
            {
                TagName = tag,
                Role = "Input"
            }).ToList()
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Test cleanup best-effort.
        }
    }

    private sealed class FakeHistoricoService : IHistoricoService
    {
        private readonly Dictionary<string, List<LeituraBruta>> _dados =
            new(StringComparer.OrdinalIgnoreCase);

        public void Adicionar(
            string clienteId,
            string tagName,
            DateTime dataHora,
            float valor)
        {
            var key = Key(clienteId, tagName);

            if (!_dados.TryGetValue(key, out var lista))
            {
                lista = [];
                _dados[key] = lista;
            }

            lista.Add(new LeituraBruta
            {
                DataHora = dataHora,
                Valor = valor
            });
        }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return BuscarHistoricoPorPeriodo(
                clienteId,
                tagName,
                DateTime.MinValue,
                DateTime.MaxValue);
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            return _dados.TryGetValue(Key(clienteId, tagName), out var lista)
                ? lista
                    .Where(x => x.DataHora >= inicio && x.DataHora <= fim)
                    .OrderBy(x => x.DataHora)
                    .Select(x => new LeituraBruta
                    {
                        DataHora = x.DataHora,
                        Valor = x.Valor
                    })
                    .ToList()
                : [];
        }

        private static string Key(string clienteId, string tagName) =>
            $"{clienteId.Trim()}::{tagName.Trim()}";
    }

    private sealed class FakeWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootPath = contentRootPath;
            WebRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string ApplicationName { get; set; } = "IAApi.Tests";
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
