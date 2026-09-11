using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests;

public sealed class NotificacoesConsistencyTests
{
    [Fact]
    public async Task RiscoAltoSemAnomalia_NaoUsaTituloDeAnomalia()
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

        context.AnomaliasDetectadas.Add(
            new AnomaliaDetectada
            {
                ClienteId = "C1",
                TagName = "TAG-RISCO",
                TipoTag = "temperatura",
                ValorRecebido = 40,
                Score = 6,
                EhAnomalia = false,
                Mensagem = "Risco alto para o processo.",
                ScoreRiscoProcesso = 75,
                ClassificacaoRisco = "alto",
                DataDeteccao =
                    new DateTime(2026, 9, 2, 12, 0, 0)
            });

        await context.SaveChangesAsync();

        var controller =
            new NotificacoesController(context);

        var result =
            await controller.Listar();

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var json =
            JsonSerializer.Serialize(ok.Value);

        using var document =
            JsonDocument.Parse(json);

        var notificacoes =
            document.RootElement;

        Assert.Equal(
            1,
            notificacoes.GetArrayLength());

        var titulo =
            notificacoes[0]
                .GetProperty("Titulo")
                .GetString();

        Assert.Equal(
            "Risco elevado",
            titulo);
    }
}