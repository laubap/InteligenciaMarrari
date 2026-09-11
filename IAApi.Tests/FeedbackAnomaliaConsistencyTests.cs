using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests;

public sealed class FeedbackAnomaliaConsistencyTests
{
    [Fact]
    public async Task Feedback_NaoAceitaRegistroNormalComoAnomalia()
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

        var normal =
            new AnomaliaDetectada
            {
                ClienteId = "C1",
                TagName = "TAG-NORMAL",
                TipoTag = "temperatura",
                ValorRecebido = 40,
                Score = 0.2,
                EhAnomalia = false,
                Mensagem = "Comportamento normal",
                Status = StatusAnomalia.Pendente,
                DataDeteccao =
                    new DateTime(2026, 9, 2, 12, 0, 0)
            };

        context.AnomaliasDetectadas.Add(normal);
        await context.SaveChangesAsync();

        var controller =
            new FeedbackAnomaliaController(context);

        var request =
            new FeedbackAnomaliaRequest
            {
                MotivoAnomaliaIAId = 999,
                AvaliacaoDeteccao =
                    AvaliacaoIa.Correto
            };

        var result =
            await controller.SalvarFeedback(
                normal.Id,
                request);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}