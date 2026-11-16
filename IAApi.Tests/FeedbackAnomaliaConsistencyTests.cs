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

    // P38 (B323-06): o motivo do feedback tem de ser do cliente da anomalia e estar ativo.

    [Fact]
    public async Task P38_MotivoDeOutroCliente_Retorna400_SemFeedbackESemMudarStatus()
    {
        await using var banco = await BancoComAnomaliaAsync();
        var motivo = await AdicionarMotivoAsync(banco.Context, "C2", ativo: true);

        var result = await new FeedbackAnomaliaController(banco.Context).SalvarFeedback(
            banco.Anomalia.Id,
            new FeedbackAnomaliaRequest { MotivoAnomaliaIAId = motivo.Id, AvaliacaoDeteccao = AvaliacaoIa.Correto });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await banco.Context.FeedbackAnomaliasIa.CountAsync());
        Assert.Equal(StatusAnomalia.Pendente, (await banco.Context.AnomaliasDetectadas.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task P38_MotivoInativoDoMesmoCliente_Retorna400_SemFeedbackESemMudarStatus()
    {
        await using var banco = await BancoComAnomaliaAsync();
        var motivo = await AdicionarMotivoAsync(banco.Context, "C1", ativo: false);

        var result = await new FeedbackAnomaliaController(banco.Context).SalvarFeedback(
            banco.Anomalia.Id,
            new FeedbackAnomaliaRequest { MotivoAnomaliaIAId = motivo.Id, AvaliacaoDeteccao = AvaliacaoIa.Incorreto });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await banco.Context.FeedbackAnomaliasIa.CountAsync());
        Assert.Equal(StatusAnomalia.Pendente, (await banco.Context.AnomaliasDetectadas.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task P38_MotivoInexistente_Retorna400ComAMesmaMensagem()
    {
        await using var banco = await BancoComAnomaliaAsync();

        var result = await new FeedbackAnomaliaController(banco.Context).SalvarFeedback(
            banco.Anomalia.Id,
            new FeedbackAnomaliaRequest { MotivoAnomaliaIAId = 999, AvaliacaoDeteccao = AvaliacaoIa.Correto });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "O tipo de falha informado não existe.",
            System.Text.Json.JsonSerializer.SerializeToElement(badRequest.Value).GetProperty("erro").GetString());
    }

    [Fact]
    public async Task P38_MotivoAtivoDoMesmoCliente_GravaComoAntes()
    {
        await using var banco = await BancoComAnomaliaAsync();
        var motivo = await AdicionarMotivoAsync(banco.Context, "C1", ativo: true);

        var result = await new FeedbackAnomaliaController(banco.Context).SalvarFeedback(
            banco.Anomalia.Id,
            new FeedbackAnomaliaRequest { MotivoAnomaliaIAId = motivo.Id, AvaliacaoDeteccao = AvaliacaoIa.Correto });

        Assert.IsType<OkObjectResult>(result);
        var feedback = await banco.Context.FeedbackAnomaliasIa.SingleAsync();
        Assert.Equal("C1", feedback.ClienteId);
        Assert.Equal(motivo.Id, feedback.MotivoAnomaliaIAId);
        Assert.Equal(StatusAnomalia.Confirmada, (await banco.Context.AnomaliasDetectadas.AsNoTracking().SingleAsync()).Status);
    }

    private static async Task<MotivoAnomaliaIA> AdicionarMotivoAsync(AppDbContext context, string cliente, bool ativo)
    {
        var motivo = new MotivoAnomaliaIA { ClienteId = cliente, Nome = $"Motivo {cliente}", Ativo = ativo };
        context.MotivosAnomaliaIA.Add(motivo);
        await context.SaveChangesAsync();
        return motivo;
    }

    private static async Task<BancoComAnomalia> BancoComAnomaliaAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();

        var anomalia = new AnomaliaDetectada
        {
            ClienteId = "C1",
            TagName = "TAG24",
            TipoTag = "pressao",
            ValorRecebido = 40,
            Score = 0.9,
            EhAnomalia = true,
            Mensagem = "Anomalia",
            Status = StatusAnomalia.Pendente,
            DataDeteccao = new DateTime(2026, 9, 2, 12, 0, 0)
        };
        context.AnomaliasDetectadas.Add(anomalia);
        await context.SaveChangesAsync();

        return new BancoComAnomalia(connection, context, anomalia);
    }

    private sealed class BancoComAnomalia : IAsyncDisposable
    {
        public BancoComAnomalia(SqliteConnection connection, AppDbContext context, AnomaliaDetectada anomalia)
        {
            Connection = connection;
            Context = context;
            Anomalia = anomalia;
        }

        public SqliteConnection Connection { get; }
        public AppDbContext Context { get; }
        public AnomaliaDetectada Anomalia { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}