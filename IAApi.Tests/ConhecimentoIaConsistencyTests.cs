using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests;

public sealed class ConhecimentoIaConsistencyTests
{
    [Fact]
    public async Task ConhecimentoIa_UsaSomenteAnomaliasNasMetricasDeAnomalia()
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
                ClassificacaoRisco = "normal",
                DataDeteccao =
                    new DateTime(2026, 9, 1, 12, 0, 0)
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
                ClassificacaoRisco = "medio",
                DataDeteccao =
                    new DateTime(2026, 9, 2, 12, 0, 0)
            });

        await context.SaveChangesAsync();

        var controller =
            new ConhecimentoIaController(context);

        var result =
            await controller.Obter();

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var json =
            JsonSerializer.Serialize(ok.Value);

        using var document =
            JsonDocument.Parse(json);

        var root =
            document.RootElement;

        Assert.Equal(
            1,
            root.GetProperty("totalAnomalias").GetInt32());

        var tags =
            root.GetProperty("tagsComMaisAnomalias");

        Assert.Equal(
            1,
            tags.GetArrayLength());

        Assert.Equal(
            "TAG-ANOMALIA",
            tags[0].GetProperty("tagName").GetString());

        var porDia =
            root.GetProperty("anomaliasPorDia");

        Assert.Equal(
            1,
            porDia.GetArrayLength());

        Assert.Equal(
            1,
            porDia[0].GetProperty("total").GetInt32());
    }
}