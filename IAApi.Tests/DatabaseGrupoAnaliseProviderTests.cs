using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class DatabaseGrupoAnaliseProviderTests
{
    [Fact]
    public async Task CarregarGruposAsync_DeveRetornarSomenteGruposAtivosDoCliente()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var grupoClienteA = new GrupoAnaliseIa
        {
            ClienteId = "CLIENTE-A",
            Nome = "DRYER_AIRFLOW",
            Descricao = "Fluxo de ar do secador",
            Ativo = true,
            MinimumAvailableTags = 2,
            MaxLagMinutes = 3,
            PesoEnsemble = 0.25,
            Sensibilidade = 0.75,
            Algoritmo = "Mahalanobis",
            Tags = new List<GrupoAnaliseTagIa>
            {
                new() { TagName = "DRYER.AIR_FLOW_SENSOR" },
                new() { TagName = "DRYER.AIR_FLOW_REFERENCE" }
            }
        };

        var grupoInativoClienteA = new GrupoAnaliseIa
        {
            ClienteId = "CLIENTE-A",
            Nome = "INATIVO",
            Ativo = false,
            Tags = new List<GrupoAnaliseTagIa>
            {
                new() { TagName = "TAG.X" },
                new() { TagName = "TAG.Y" }
            }
        };

        var grupoClienteB = new GrupoAnaliseIa
        {
            ClienteId = "CLIENTE-B",
            Nome = "OUTRO_CLIENTE",
            Ativo = true,
            Tags = new List<GrupoAnaliseTagIa>
            {
                new() { TagName = "TAG.A" },
                new() { TagName = "TAG.B" }
            }
        };

        context.GruposAnaliseIa.AddRange(
            grupoClienteA,
            grupoInativoClienteA,
            grupoClienteB);

        await context.SaveChangesAsync();

        var provider = new DatabaseGrupoAnaliseProvider(context);

        var grupos = await provider.CarregarGruposAsync(" CLIENTE-A ");

        var grupo = Assert.Single(grupos);

        Assert.Equal("DRYER_AIRFLOW", grupo.Nome);
        Assert.Equal(2, grupo.MinimumAvailableTags);
        Assert.Equal(3, grupo.MaxLagMinutes);
        Assert.Equal(2, grupo.Tags.Count);
        Assert.Contains("DRYER.AIR_FLOW_SENSOR", grupo.Tags);
        Assert.Contains("DRYER.AIR_FLOW_REFERENCE", grupo.Tags);
    }
}
