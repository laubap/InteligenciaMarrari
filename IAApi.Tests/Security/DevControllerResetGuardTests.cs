using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace IAApi.Tests.Security;

/// <summary>
/// P33: POST /api/dev/reset so responde em Development e para a propria maquina
/// (mesma regra do Intelligence Lab). Fora disso, 404 e o banco fica intacto.
/// SQLite em memoria; nenhum servico externo.
/// </summary>
public sealed class DevControllerResetGuardTests
{
    [Fact]
    public async Task Producao_Retorna404_ESemApagarNada()
    {
        await using var banco = await BancoComUmEquipamentoAsync();
        var controller = CriarController(banco.Context, Environments.Production, IPAddress.Loopback);

        Assert.IsType<NotFoundResult>(controller.ResetarBancoTeste());
        Assert.Equal(1, await banco.Context.EquipamentosIa.CountAsync());
    }

    [Fact]
    public async Task Staging_Retorna404()
    {
        await using var banco = await BancoComUmEquipamentoAsync();
        var controller = CriarController(banco.Context, Environments.Staging, IPAddress.Loopback);

        Assert.IsType<NotFoundResult>(controller.ResetarBancoTeste());
        Assert.Equal(1, await banco.Context.EquipamentosIa.CountAsync());
    }

    [Theory]
    [InlineData("192.0.2.40")]
    [InlineData("10.0.1.99")]
    [InlineData("::ffff:192.0.2.40")]
    public async Task PedidoDeOutraMaquina_Retorna404_ESemApagarNada(string endereco)
    {
        await using var banco = await BancoComUmEquipamentoAsync();
        var controller = CriarController(banco.Context, Environments.Development, IPAddress.Parse(endereco));

        Assert.IsType<NotFoundResult>(controller.ResetarBancoTeste());
        Assert.Equal(1, await banco.Context.EquipamentosIa.CountAsync());
    }

    [Fact]
    public async Task SemEnderecoDoCliente_Retorna404()
    {
        await using var banco = await BancoComUmEquipamentoAsync();
        var controller = CriarController(banco.Context, Environments.Development, remoteAddress: null);

        Assert.IsType<NotFoundResult>(controller.ResetarBancoTeste());
        Assert.Equal(1, await banco.Context.EquipamentosIa.CountAsync());
    }

    [Fact]
    public async Task DevelopmentELoopbackIpv4_ApagaComoAntes()
    {
        await using var banco = await BancoComUmEquipamentoAsync();
        var controller = CriarController(banco.Context, Environments.Development, IPAddress.Loopback);

        Assert.IsType<OkObjectResult>(controller.ResetarBancoTeste());
        Assert.Equal(0, await banco.Context.EquipamentosIa.CountAsync());
    }

    [Fact]
    public async Task DevelopmentELoopbackIpv6_ApagaComoAntes()
    {
        await using var banco = await BancoComUmEquipamentoAsync();
        var controller = CriarController(banco.Context, Environments.Development, IPAddress.IPv6Loopback);

        Assert.IsType<OkObjectResult>(controller.ResetarBancoTeste());
        Assert.Equal(0, await banco.Context.EquipamentosIa.CountAsync());
    }

    private static DevController CriarController(
        AppDbContext context,
        string ambiente,
        IPAddress? remoteAddress)
    {
        var controller = new DevController(context, new StubEnvironment(ambiente));
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = remoteAddress;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static async Task<Banco> BancoComUmEquipamentoAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.EquipamentosIa.Add(new EquipamentoIa { ClienteId = "C1", Nome = "Bomba de teste" });
        await context.SaveChangesAsync();
        return new Banco(connection, context);
    }

    private sealed class Banco(SqliteConnection connection, AppDbContext context) : IAsyncDisposable
    {
        public AppDbContext Context { get; } = context;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "IAApi.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
