using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Tests.Security;

/// <summary>
/// P35, P36 e P37 (B323-03 a B323-05): isolamento entre clientes nas relacoes
/// de tag, no vinculo equipamento-tag e no vinculo processo-equipamento.
/// SQLite em memoria; nenhum servico externo. Os controllers sao chamados direto.
/// </summary>
public sealed class ClienteIsolamentoTests
{
    // ---------------- P35: GET /api/tags/{tagName}/relacoes ----------------

    [Fact]
    public async Task P35_ComClienteId_DevolveSoAsRelacoesDoCliente()
    {
        await using var banco = await BancoComTag24EmDoisClientesAsync();
        var controller = new TagRelacoesController(banco.Context);

        var json = Ler(controller.BuscarRelacoes("TAG24", "C1"));

        Assert.Equal("TAG24", json.GetProperty("tagName").GetString());
        Assert.Equal(new[] { "Processo C1" }, Nomes(json, "processos"));
        Assert.Equal(new[] { "Bomba C1" }, Nomes(json, "equipamentos"));
    }

    [Fact]
    public async Task P35_ComOutroClienteId_DevolveSoAsRelacoesDesseCliente()
    {
        await using var banco = await BancoComTag24EmDoisClientesAsync();
        var controller = new TagRelacoesController(banco.Context);

        var json = Ler(controller.BuscarRelacoes("TAG24", "C2"));

        Assert.Equal(new[] { "Processo C2" }, Nomes(json, "processos"));
        Assert.Equal(new[] { "Bomba C2" }, Nomes(json, "equipamentos"));
    }

    [Fact]
    public async Task P35_ClienteSemRelacoesParaATag_DevolveListasVazias()
    {
        await using var banco = await BancoComTag24EmDoisClientesAsync();
        var controller = new TagRelacoesController(banco.Context);

        var json = Ler(controller.BuscarRelacoes("TAG24", "C3"));

        Assert.Empty(Nomes(json, "processos"));
        Assert.Empty(Nomes(json, "equipamentos"));
    }

    /// <summary>
    /// Opcao (a) aprovada em 16/11: sem clienteId e com a tag em mais de um cliente,
    /// 400 com mensagem clara, sem devolver relacoes e sem citar os clientes.
    /// </summary>
    [Fact]
    public async Task P35_SemClienteId_TagEmDoisClientes_Retorna400SemRelacoes()
    {
        await using var banco = await BancoComTag24EmDoisClientesAsync();
        var controller = new TagRelacoesController(banco.Context);

        var badRequest = Assert.IsType<BadRequestObjectResult>(controller.BuscarRelacoes("TAG24"));
        var json = JsonSerializer.SerializeToElement(badRequest.Value);

        Assert.Equal(new[] { "erro" }, json.EnumerateObject().Select(p => p.Name).ToArray());
        var erro = json.GetProperty("erro").GetString()!;
        Assert.Contains("mais de um cliente", erro);
        Assert.Contains("clienteId", erro);
        Assert.DoesNotContain("C1", erro);
        Assert.DoesNotContain("C2", erro);
    }

    /// <summary>
    /// A ambiguidade conta as duas tabelas juntas: processo so de C1 e equipamento so de C2
    /// tambem e "mais de um cliente".
    /// </summary>
    [Fact]
    public async Task P35_SemClienteId_ProcessoDeUmClienteEquipamentoDeOutro_Retorna400()
    {
        await using var banco = await BancoAsync();
        var processo = await AdicionarProcessoAsync(banco.Context, "C1", "Processo C1");
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C2", "Bomba C2");
        banco.Context.ProcessoTagsIa.Add(new ProcessoTagIa { ProcessoIaId = processo.Id, ClienteId = "C1", TagName = "TAG24" });
        banco.Context.EquipamentoTagsIa.Add(new EquipamentoTagIa { EquipamentoIaId = equipamento.Id, ClienteId = "C2", TagName = "TAG24" });
        await banco.Context.SaveChangesAsync();
        var controller = new TagRelacoesController(banco.Context);

        Assert.IsType<BadRequestObjectResult>(controller.BuscarRelacoes("TAG24"));
    }

    /// <summary>
    /// Compatibilidade: sem clienteId e com a tag em um cliente so, a resposta e a mesma
    /// de antes da correcao, com os mesmos campos.
    /// </summary>
    [Fact]
    public async Task P35_SemClienteId_TagEmUmCliente_RespostaIgualAAnterior()
    {
        await using var banco = await BancoComTag24EmDoisClientesAsync();
        var outra = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1 reserva");
        banco.Context.EquipamentoTagsIa.Add(new EquipamentoTagIa { EquipamentoIaId = outra.Id, ClienteId = "C1", TagName = "TAG99", PapelDaTag = "vazao" });
        await banco.Context.SaveChangesAsync();
        var controller = new TagRelacoesController(banco.Context);

        var json = Ler(controller.BuscarRelacoes("TAG99"));

        Assert.Equal(
            new[] { "tagName", "processos", "equipamentos" },
            json.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("TAG99", json.GetProperty("tagName").GetString());
        Assert.Empty(Nomes(json, "processos"));
        Assert.Equal(new[] { "Bomba C1 reserva" }, Nomes(json, "equipamentos"));

        var item = json.GetProperty("equipamentos")[0];
        Assert.Equal(
            new[] { "id", "nome", "papelDaTag" },
            item.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("vazao", item.GetProperty("papelDaTag").GetString());
    }

    /// <summary>Compatibilidade: tag com processo e equipamento do mesmo cliente, sem clienteId.</summary>
    [Fact]
    public async Task P35_SemClienteId_ProcessoEEquipamentoDoMesmoCliente_RespostaIgualAAnterior()
    {
        await using var banco = await BancoAsync();
        var processo = await AdicionarProcessoAsync(banco.Context, "C1", "Processo C1");
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        banco.Context.ProcessoTagsIa.Add(new ProcessoTagIa { ProcessoIaId = processo.Id, ClienteId = "C1", TagName = "TAG24" });
        banco.Context.EquipamentoTagsIa.Add(new EquipamentoTagIa { EquipamentoIaId = equipamento.Id, ClienteId = "C1", TagName = "TAG24" });
        await banco.Context.SaveChangesAsync();
        var controller = new TagRelacoesController(banco.Context);

        var json = Ler(controller.BuscarRelacoes("TAG24"));

        Assert.Equal(new[] { "Processo C1" }, Nomes(json, "processos"));
        Assert.Equal(new[] { "Bomba C1" }, Nomes(json, "equipamentos"));
    }

    /// <summary>Compatibilidade: tag sem nenhuma relacao, sem clienteId: 200 com listas vazias.</summary>
    [Fact]
    public async Task P35_SemClienteId_TagSemRelacoes_DevolveListasVaziasComoAntes()
    {
        await using var banco = await BancoComTag24EmDoisClientesAsync();
        var controller = new TagRelacoesController(banco.Context);

        var json = Ler(controller.BuscarRelacoes("TAG_INEXISTENTE"));

        Assert.Equal("TAG_INEXISTENTE", json.GetProperty("tagName").GetString());
        Assert.Empty(Nomes(json, "processos"));
        Assert.Empty(Nomes(json, "equipamentos"));
    }

    // ---------------- P36: POST /api/equipamentos/{id}/tags ----------------

    [Fact]
    public async Task P36_ClienteDiferenteDoEquipamento_Retorna400_ESemGravar()
    {
        await using var banco = await BancoAsync();
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        var controller = new EquipamentoController(banco.Context);

        var resultado = controller.VincularTag(
            equipamento.Id,
            new EquipamentoTagRequest { ClienteId = "C2", TagName = "TAG24", PapelDaTag = "pressao" });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(0, await banco.Context.EquipamentoTagsIa.CountAsync());
    }

    [Fact]
    public async Task P36_ClienteVazio_Retorna400_ESemGravar()
    {
        await using var banco = await BancoAsync();
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        var controller = new EquipamentoController(banco.Context);

        var resultado = controller.VincularTag(
            equipamento.Id,
            new EquipamentoTagRequest { ClienteId = "", TagName = "TAG24" });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(0, await banco.Context.EquipamentoTagsIa.CountAsync());
    }

    [Fact]
    public async Task P36_VinculoExistenteComClienteDiferente_Retorna400_ESemAlterar()
    {
        await using var banco = await BancoAsync();
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        banco.Context.EquipamentoTagsIa.Add(new EquipamentoTagIa
        {
            EquipamentoIaId = equipamento.Id,
            ClienteId = "C1",
            TagName = "TAG24",
            PapelDaTag = "original",
            Escopo = "original"
        });
        await banco.Context.SaveChangesAsync();
        var controller = new EquipamentoController(banco.Context);

        var resultado = controller.VincularTag(
            equipamento.Id,
            new EquipamentoTagRequest { ClienteId = "C2", TagName = "TAG24", PapelDaTag = "alterado", Escopo = "alterado" });

        Assert.IsType<BadRequestObjectResult>(resultado);
        var vinculo = await banco.Context.EquipamentoTagsIa.AsNoTracking().SingleAsync();
        Assert.Equal("original", vinculo.PapelDaTag);
        Assert.Equal("original", vinculo.Escopo);
    }

    [Fact]
    public async Task P36_MesmoCliente_GravaComoAntes()
    {
        await using var banco = await BancoAsync();
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        var controller = new EquipamentoController(banco.Context);

        var resultado = controller.VincularTag(
            equipamento.Id,
            new EquipamentoTagRequest { ClienteId = "C1", TagName = "TAG24", PapelDaTag = "pressao" });

        Assert.IsType<OkObjectResult>(resultado);
        var vinculo = await banco.Context.EquipamentoTagsIa.SingleAsync();
        Assert.Equal("C1", vinculo.ClienteId);
        Assert.Equal(equipamento.Id, vinculo.EquipamentoIaId);
    }

    [Fact]
    public async Task P36_VinculoExistenteMesmoCliente_AtualizaComoAntes()
    {
        await using var banco = await BancoAsync();
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        banco.Context.EquipamentoTagsIa.Add(new EquipamentoTagIa
        {
            EquipamentoIaId = equipamento.Id,
            ClienteId = "C1",
            TagName = "TAG24",
            PapelDaTag = "original"
        });
        await banco.Context.SaveChangesAsync();
        var controller = new EquipamentoController(banco.Context);

        var resultado = controller.VincularTag(
            equipamento.Id,
            new EquipamentoTagRequest { ClienteId = "C1", TagName = "TAG24", PapelDaTag = "alterado" });

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal("alterado", (await banco.Context.EquipamentoTagsIa.AsNoTracking().SingleAsync()).PapelDaTag);
    }

    [Fact]
    public async Task P36_EquipamentoInexistente_Retorna404ComoAntes()
    {
        await using var banco = await BancoAsync();
        var controller = new EquipamentoController(banco.Context);

        var resultado = controller.VincularTag(
            999,
            new EquipamentoTagRequest { ClienteId = "C1", TagName = "TAG24" });

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    // ---------------- P37: POST /api/processo-equipamentos ----------------

    [Fact]
    public async Task P37_ProcessoEEquipamentoDeClientesDiferentes_Retorna400_ESemGravar()
    {
        await using var banco = await BancoAsync();
        var processo = await AdicionarProcessoAsync(banco.Context, "C1", "Processo C1");
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C2", "Bomba C2");
        var controller = new ProcessoEquipamentoController(banco.Context);

        var resultado = controller.Vincular(new ProcessoEquipamentoRequest
        {
            ProcessoId = processo.Id,
            EquipamentoId = equipamento.Id
        });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(0, await banco.Context.ProcessoEquipamentosIa.CountAsync());
    }

    [Fact]
    public async Task P37_MesmoCliente_GravaComoAntes()
    {
        await using var banco = await BancoAsync();
        var processo = await AdicionarProcessoAsync(banco.Context, "C1", "Processo C1");
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        var controller = new ProcessoEquipamentoController(banco.Context);

        var resultado = controller.Vincular(new ProcessoEquipamentoRequest
        {
            ProcessoId = processo.Id,
            EquipamentoId = equipamento.Id,
            PapelNoProcesso = "recalque"
        });

        Assert.IsType<OkObjectResult>(resultado);
        var vinculo = await banco.Context.ProcessoEquipamentosIa.SingleAsync();
        Assert.Equal(processo.Id, vinculo.ProcessoIaId);
        Assert.Equal(equipamento.Id, vinculo.EquipamentoIaId);
    }

    [Fact]
    public async Task P37_ProcessoInexistente_Retorna404ComoAntes()
    {
        await using var banco = await BancoAsync();
        var equipamento = await AdicionarEquipamentoAsync(banco.Context, "C1", "Bomba C1");
        var controller = new ProcessoEquipamentoController(banco.Context);

        var resultado = controller.Vincular(new ProcessoEquipamentoRequest
        {
            ProcessoId = 999,
            EquipamentoId = equipamento.Id
        });

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    // ---------------- apoio ----------------

    private static JsonElement Ler(IActionResult resultado)
    {
        var ok = Assert.IsType<OkObjectResult>(resultado);
        return JsonSerializer.SerializeToElement(ok.Value);
    }

    private static string[] Nomes(JsonElement json, string lista) =>
        json.GetProperty(lista).EnumerateArray()
            .Select(x => x.GetProperty("nome").GetString() ?? "")
            .ToArray();

    private static async Task<BancoTeste> BancoComTag24EmDoisClientesAsync()
    {
        var banco = await BancoAsync();

        foreach (var cliente in new[] { "C1", "C2" })
        {
            var processo = await AdicionarProcessoAsync(banco.Context, cliente, $"Processo {cliente}");
            var equipamento = await AdicionarEquipamentoAsync(banco.Context, cliente, $"Bomba {cliente}");

            banco.Context.ProcessoTagsIa.Add(new ProcessoTagIa
            {
                ProcessoIaId = processo.Id,
                ClienteId = cliente,
                TagName = "TAG24",
                PapelDaTag = "pressao",
                Principal = true
            });
            banco.Context.EquipamentoTagsIa.Add(new EquipamentoTagIa
            {
                EquipamentoIaId = equipamento.Id,
                ClienteId = cliente,
                TagName = "TAG24",
                PapelDaTag = "pressao"
            });
        }

        await banco.Context.SaveChangesAsync();
        return banco;
    }

    private static async Task<ProcessoIa> AdicionarProcessoAsync(AppDbContext context, string cliente, string nome)
    {
        var processo = new ProcessoIa { ClienteId = cliente, Nome = nome };
        context.ProcessosIa.Add(processo);
        await context.SaveChangesAsync();
        return processo;
    }

    private static async Task<EquipamentoIa> AdicionarEquipamentoAsync(AppDbContext context, string cliente, string nome)
    {
        var equipamento = new EquipamentoIa { ClienteId = cliente, Nome = nome };
        context.EquipamentosIa.Add(equipamento);
        await context.SaveChangesAsync();
        return equipamento;
    }

    private static async Task<BancoTeste> BancoAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new BancoTeste(connection, context);
    }

    private sealed class BancoTeste : IAsyncDisposable
    {
        public BancoTeste(SqliteConnection connection, AppDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public AppDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
