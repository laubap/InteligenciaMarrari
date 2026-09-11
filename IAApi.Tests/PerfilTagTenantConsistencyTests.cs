using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests;

public sealed class PerfilTagTenantConsistencyTests
{
    [Fact]
    public async Task VisaoCliente_NaoUsaProcessoDeOutroClienteComMesmaTag()
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

        context.PerfisIa.Add(
            new TagPerfilIa
            {
                ClienteId = "CLIENTE-A",
                TagName = "TAG1"
            });

        var processoClienteB =
            new ProcessoIa
            {
                ClienteId = "CLIENTE-B",
                Nome = "Processo B"
            };

        context.ProcessosIa.Add(processoClienteB);

        context.ProcessoTagsIa.Add(
            new ProcessoTagIa
            {
                ClienteId = "CLIENTE-B",
                TagName = "TAG1",
                ProcessoIa = processoClienteB
            });

        await context.SaveChangesAsync();

        var controller =
            new PerfilTagController(
                null!,
                context);

        var result =
            controller.ListarTagsVisaoCliente();

        var ok =
            Assert.IsType<OkObjectResult>(result);

        var json =
            JsonSerializer.Serialize(ok.Value);

        using var document =
            JsonDocument.Parse(json);

        var tags =
            document.RootElement;

        var tag =
            Assert.Single(tags.EnumerateArray());

        Assert.Equal(
            "CLIENTE-A",
            tag.GetProperty("ClienteId").GetString());

        Assert.Equal(
            "TAG1",
            tag.GetProperty("TagName").GetString());

        Assert.Equal(
            JsonValueKind.Null,
            tag.GetProperty("processo").ValueKind);
    }
}