using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests;

public sealed class AnomaliaHistoricoConsistencyTests
{
    [Fact]
    public async Task HistoricoDeAnomalias_NaoIncluiLeituraNormal()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var context =
            new AppDbContext(options);

        await context.Database.EnsureCreatedAsync();

        context.AnomaliasDetectadas.AddRange(
            new AnomaliaDetectada
            {
                ClienteId = "C1",
                TagName = "TAG-NORMAL",
                TipoTag = "temperatura",
                ValorRecebido = 40,
                Score = 0.2,
                EhAnomalia = false,
                Mensagem = "Comportamento normal",
                DataDeteccao =
                    new DateTime(2026, 9, 2, 12, 0, 0)
            },
            new AnomaliaDetectada
            {
                ClienteId = "C1",
                TagName = "TAG-ANOMALIA",
                TipoTag = "temperatura",
                ValorRecebido = 100,
                Score = 4,
                EhAnomalia = true,
                Mensagem = "Anomalia detectada",
                DataDeteccao =
                    new DateTime(2026, 9, 2, 12, 1, 0)
            });

        await context.SaveChangesAsync();

        var controller =
            new AnomaliaHistoricoController(context);

        var result =
            controller.ListarPorCliente("C1");

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var historico =
            Assert.IsType<List<AnomaliaDetectada>>(ok.Value);

        var registro =
            Assert.Single(historico);

        Assert.True(registro.EhAnomalia);
        Assert.Equal("TAG-ANOMALIA", registro.TagName);
    }
}