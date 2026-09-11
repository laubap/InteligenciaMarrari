using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests;

public sealed class DashboardGeralConsistencyTests
{
    [Fact]
    public async Task Dashboard_ContaSomenteAnomaliasOperacionais()
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
                Score = 4.0,
                EhAnomalia = true,
                Mensagem = "Anomalia detectada",
                DataDeteccao =
                    new DateTime(2026, 9, 2, 12, 1, 0)
            });

        await context.SaveChangesAsync();

        var controller =
            new DashboardGeralController(context);

        var actionResult =
            controller.ObterDashboard();

        var ok =
            Assert.IsType<OkObjectResult>(actionResult);

        var json =
            JsonSerializer.Serialize(ok.Value);

        using var document =
            JsonDocument.Parse(json);

        var root =
            document.RootElement;

        Assert.Equal(
            1,
            root.GetProperty("totalAnomalias").GetInt32());

        Assert.Equal(
            95,
            root.GetProperty("saudeGeral").GetInt32());

        var ultimas =
            root.GetProperty("ultimasAnomalias");

        Assert.Equal(
            1,
            ultimas.GetArrayLength());

        Assert.True(
            ultimas[0]
                .GetProperty("EhAnomalia")
                .GetBoolean());
    }
}