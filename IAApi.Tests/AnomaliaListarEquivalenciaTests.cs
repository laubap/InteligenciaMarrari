using System.Data.Common;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IAApi.Tests;

/// <summary>
/// Tarefas 34.1 e 34.2 (P42): GET /api/anomalias sem N+1 e com filtros opcionais.
/// A referencia e uma copia congelada do codigo de antes (commit 7177744:
/// AnomaliaController.Listar e FeedbackAnomaliaAnaliseService.GerarAnaliseFeedbacks),
/// executada no mesmo banco. O JSON e serializado com as opcoes padrao do
/// AddControllers (JsonSerializerDefaults.Web; o Program.cs nao as altera).
/// Banco SQLite em memoria; nada de Bridge, PSi4 ou banco real.
/// </summary>
public sealed class AnomaliaListarEquivalenciaTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SemParametros_JsonIdenticoAoDeAntes()
    {
        await using var banco = await BancoAsync(comEmpates: true);

        var antes = Json(LegadoListar(banco.Context));
        var depois = Json(await ListarAsync(banco));

        Assert.Equal(antes, depois);

        // A base cobre os casos que importam para o FeedbackSemelhante.
        var linhas = JsonDocument.Parse(depois).RootElement.EnumerateArray().ToList();
        Assert.Equal(Fixture.TotalAnomalias, linhas.Count);
        var comFeedback = linhas.Count(l => l.GetProperty("feedbackSemelhante").ValueKind != JsonValueKind.Null);
        Assert.InRange(comFeedback, 30, linhas.Count - 30);
        Assert.Contains(linhas, l =>
            l.GetProperty("feedbackSemelhante").ValueKind != JsonValueKind.Null &&
            l.GetProperty("feedbackSemelhante").GetProperty("motivoMaisComum").GetString() == "Motivo não informado");
    }

    [Fact]
    public async Task Desempate_DoMotivo_SegueAOrdemDoFeedback_ComoAntes()
    {
        // Dois motivos empatados: vence o do feedback de menor Id, mesmo
        // quando a anomalia dele tem Id maior (ordem do join diferente).
        await using var banco = await BancoVazioAsync();
        var ctx = banco.Context;
        var a = new MotivoAnomaliaIA { ClienteId = "C1", Nome = "motivo_a" };
        var b = new MotivoAnomaliaIA { ClienteId = "C1", Nome = "motivo_b" };
        ctx.AddRange(a, b);
        var anomalias = Enumerable.Range(0, 4).Select(i => Anomalia("C1", "T1", 10 + i, 1.0, i)).ToList();
        ctx.AddRange(anomalias);
        await ctx.SaveChangesAsync();
        ctx.AddRange(
            Feedback(anomalias[3], b),
            Feedback(anomalias[0], a),
            Feedback(anomalias[2], a),
            Feedback(anomalias[1], b));
        await ctx.SaveChangesAsync();

        var antes = Json(LegadoListar(ctx));
        var depois = Json(await ListarAsync(banco));

        Assert.Equal(antes, depois);
        Assert.Contains("\"motivoMaisComum\":\"motivo b\"", depois);
    }

    [Fact]
    public async Task SemParametros_NumeroDeComandosSqlNaoCresceComAsLinhas()
    {
        await using var banco = await BancoAsync(comEmpates: true);

        banco.Comandos.Zerar();
        await ListarAsync(banco);
        var depois = banco.Comandos.Total;

        banco.Comandos.Zerar();
        LegadoListar(banco.Context);
        var antes = banco.Comandos.Total;

        Assert.Equal(2, depois);
        Assert.Equal(1 + Fixture.TotalAnomalias, antes);
    }

    [Fact]
    public async Task ListaVazia_DevolveArrayVazio_ComUmaConsulta()
    {
        await using var banco = await BancoVazioAsync();

        banco.Comandos.Zerar();
        var json = Json(await ListarAsync(banco));
        var comandos = banco.Comandos.Total;

        Assert.Equal("[]", json);
        Assert.Equal(Json(LegadoListar(banco.Context)), json);
        Assert.Equal(1, comandos);
    }

    [Fact]
    public async Task GerarAnaliseFeedbacks_CaminhoDaPredicao_IgualAoDeAntes()
    {
        await using var banco = await BancoAsync(comEmpates: true);
        var servico = new FeedbackAnomaliaAnaliseService(banco.Context);

        foreach (var x in banco.Context.AnomaliasDetectadas.AsNoTracking().ToList())
        {
            foreach (var (cliente, tag) in Fixture.Chaves)
            {
                var antes = LegadoGerarAnaliseFeedbacks(banco.Context, cliente, tag, x.Score, x.ValorRecebido);
                var depois = servico.GerarAnaliseFeedbacks(cliente, tag, x.Score, x.ValorRecebido);
                Assert.Equal(JsonSerializer.Serialize(antes, Web), JsonSerializer.Serialize(depois, Web));
            }
        }
    }

    // ---- 34.2: parametros opcionais ---------------------------------------

    [Theory]
    [InlineData("C1")]
    [InlineData("C2")]
    [InlineData("c1")] // comparacao exata, como no filtro do LiveMonitor e do export
    [InlineData("NAO_EXISTE")]
    public async Task ClienteId_IgualAoDeAntesFiltradoPorCliente(string cliente)
    {
        await using var banco = await BancoAsync(comEmpates: false);

        var esperado = Filtrar(LegadoListar(banco.Context), cliente: cliente);
        var obtido = Json(await ListarAsync(banco, clienteId: cliente));

        Assert.Equal(esperado, obtido);
    }

    [Fact]
    public async Task ClienteId_ComLinhas_UsaDuasConsultas()
    {
        await using var banco = await BancoAsync(comEmpates: false);

        banco.Comandos.Zerar();
        var json = Json(await ListarAsync(banco, clienteId: "C1"));

        Assert.NotEqual("[]", json);
        Assert.Equal(2, banco.Comandos.Total);
    }

    [Fact]
    public async Task Desde_Inclusivo_IgualAoDeAntesFiltradoPorData()
    {
        await using var banco = await BancoAsync(comEmpates: false);
        // Exatamente o DataDeteccao de uma linha: ela tem de vir (inclusivo).
        var desde = Fixture.Base.AddSeconds(7 * 150 + 0.25);
        Assert.Contains(banco.Context.AnomaliasDetectadas.AsNoTracking().ToList(), x => x.DataDeteccao == desde);

        var esperado = Filtrar(LegadoListar(banco.Context), desde: desde);
        var obtido = Json(await ListarAsync(banco, desde: desde));

        Assert.Equal(esperado, obtido);
        Assert.Equal(Fixture.TotalAnomalias - 150, JsonDocument.Parse(obtido).RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(100000)]
    public async Task Limite_AsMaisRecentes_IgualAoInicioDaListaDeAntes(int limite)
    {
        await using var banco = await BancoAsync(comEmpates: false);

        var esperado = Filtrar(LegadoListar(banco.Context), limite: limite);
        var obtido = Json(await ListarAsync(banco, limite: limite));

        Assert.Equal(esperado, obtido);
    }

    [Fact]
    public async Task TresParametros_Combinados()
    {
        await using var banco = await BancoAsync(comEmpates: false);
        var desde = Fixture.Base.AddSeconds(7 * 40);

        var esperado = Filtrar(LegadoListar(banco.Context), cliente: "C2", desde: desde, limite: 25);
        var obtido = Json(await ListarAsync(banco, clienteId: "C2", desde: desde, limite: 25));

        Assert.Equal(esperado, obtido);
        Assert.Equal(25, JsonDocument.Parse(obtido).RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Limite_MenorQueUm_Devolve400_SemConsultar(int limite)
    {
        await using var banco = await BancoAsync(comEmpates: false);

        banco.Comandos.Zerar();
        var resultado = await Controller(banco).Listar(limite: limite);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(0, banco.Comandos.Total);
    }

    // ---- Pelo HTTP: formatador e binding reais do ASP.NET ------------------
    // Kestrel em 127.0.0.1 (porta livre), so com o AnomaliaController e o
    // SQLite em memoria. Nao usa o Program.cs (nada de Bridge nem hosted service).

    [Fact]
    public async Task Http_SemParametros_CorpoIdenticoAoDeAntes()
    {
        await using var banco = await BancoAsync(comEmpates: true);
        await using var servidor = await Servidor.IniciarAsync(banco);

        var resposta = await servidor.Http.GetAsync("/api/anomalias");
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(LegadoListar(banco.Context), servidor.OpcoesJsonMvc), corpo);
    }

    [Fact]
    public async Task Http_Parametros_ChegamAoFiltro()
    {
        await using var banco = await BancoAsync(comEmpates: false);
        await using var servidor = await Servidor.IniciarAsync(banco);
        var desde = Fixture.Base.AddSeconds(7 * 40);

        var corpo = await servidor.Http.GetStringAsync(
            "/api/anomalias?clienteId=C2&desde=" + Uri.EscapeDataString(desde.ToString("yyyy-MM-ddTHH:mm:ss")) + "&limite=25");

        Assert.Equal(Filtrar(LegadoListar(banco.Context), cliente: "C2", desde: desde, limite: 25, opcoes: servidor.OpcoesJsonMvc), corpo);
    }

    [Theory]
    [InlineData("limite=0")]
    [InlineData("limite=abc")]
    [InlineData("desde=ontem")]
    public async Task Http_ParametroInvalido_Devolve400(string consulta)
    {
        await using var banco = await BancoAsync(comEmpates: false);
        await using var servidor = await Servidor.IniciarAsync(banco);

        var resposta = await servidor.Http.GetAsync("/api/anomalias?" + consulta);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    private sealed class Servidor : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Http { get; }

        // Opcoes que o formatador JSON do MVC usa de fato (com o encoder que ele escolhe).
        public JsonSerializerOptions OpcoesJsonMvc =>
            _app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MvcOptions>>().Value
                .OutputFormatters.OfType<Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonOutputFormatter>()
                .Single().SerializerOptions;

        private Servidor(WebApplication app, HttpClient http)
        {
            _app = app;
            Http = http;
        }

        public static async Task<Servidor> IniciarAsync(Banco banco)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddScoped(_ =>
                new AnomaliaController(null!, banco.Context, new FeedbackAnomaliaAnaliseService(banco.Context), null!));
            builder.Services.AddControllers()
                .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new SoAnomaliaController()))
                .AddControllersAsServices();

            var app = builder.Build();
            app.MapControllers();
            await app.StartAsync();

            var endereco = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Servidor(app, new HttpClient { BaseAddress = new Uri(endereco) });
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class SoAnomaliaController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Clear();
            feature.Controllers.Add(typeof(AnomaliaController).GetTypeInfo());
        }
    }

    // ---- Referencia congelada (codigo de antes, commit 7177744) -----------

    private static List<object> LegadoListar(AppDbContext context)
    {
        // AnomaliaController.Listar, sem alteracoes (versao async trocada por sincrona).
        var registros = context.AnomaliasDetectadas
            .OrderByDescending(x => x.DataDeteccao)
            .ToList();

        var anomalias = registros.Select(x => new
        {
            x.Id,
            x.ClienteId,
            x.TagName,
            x.TipoTag,
            Valor = x.ValorRecebido,
            x.Score,
            x.EhAnomalia,
            x.Mensagem,
            x.Criticidade,
            RiscoProcesso = x.ScoreRiscoProcesso,
            x.ClassificacaoRisco,
            x.TendenciaRisco,
            x.TendenciaValor,
            x.DataDeteccao,

            FeedbackSemelhante = LegadoGerarAnaliseFeedbacks(
                context,
                x.ClienteId,
                x.TagName,
                x.Score,
                x.ValorRecebido
            )
        }).ToList();

        return anomalias.Cast<object>().ToList();
    }

    private static FeedbackSemelhanteDto? LegadoGerarAnaliseFeedbacks(
        AppDbContext _context,
        string clienteId,
        string tagName,
        double score,
        double valorAtual)
    {
        var feedbacks = _context.FeedbackAnomaliasIa
            .Include(x => x.MotivoAnomaliaIA)
            .Join(
                _context.AnomaliasDetectadas,
                feedback => feedback.AnomaliaId,
                anomalia => anomalia.Id,
                (feedback, anomalia) => new
                {
                    feedback.ClienteId,
                    feedback.TagName,
                    Motivo = feedback.MotivoAnomaliaIA != null
                        ? feedback.MotivoAnomaliaIA.Nome
                        : "Motivo não informado",
                    feedback.Descricao,
                    feedback.AcaoTomada,
                    anomalia.Score,
                    Valor = anomalia.ValorRecebido
                }
            )
            .Where(x => x.ClienteId == clienteId && x.TagName == tagName)
            .ToList();

        if (feedbacks.Count == 0)
            return null;

        var similares = feedbacks
            .Where(x =>
                Math.Abs(x.Score - score) <= 2 ||
                Math.Abs(x.Valor - valorAtual) <= Math.Abs(valorAtual * 0.15))
            .ToList();

        if (similares.Count == 0)
            return null;

        var motivoMaisComum = similares
            .GroupBy(x => x.Motivo)
            .OrderByDescending(x => x.Count())
            .First();

        var percentual = Math.Round(
            (double)motivoMaisComum.Count() / similares.Count * 100,
            1
        );

        return new FeedbackSemelhanteDto
        {
            TotalOcorrencias = similares.Count,
            MotivoMaisComum = motivoMaisComum.Key
                .Replace("_", " ")
                .Replace("falha mecanica", "falha mecânica")
                .Replace("falha sensor", "falha de sensor"),
            Percentual = percentual,
            Mensagem = "Esse foi o motivo mais frequente registrado pelos operadores em casos semelhantes."
        };
    }

    // ---- Apoio --------------------------------------------------------------

    private static string Json(object? valor) => JsonSerializer.Serialize(valor, Web);

    private static string Json(IActionResult resultado)
    {
        var ok = Assert.IsType<OkObjectResult>(resultado);
        return Json(ok.Value);
    }

    // Filtra a lista de antes pelas mesmas regras dos parametros novos,
    // preservando a ordem e o objeto de cada linha.
    private static string Filtrar(List<object> antes, string? cliente = null, DateTime? desde = null, int? limite = null,
        JsonSerializerOptions? opcoes = null)
    {
        var linhas = antes.Where(l =>
        {
            var e = JsonSerializer.SerializeToElement(l, Web);
            return (cliente is null || e.GetProperty("clienteId").GetString() == cliente) &&
                   (desde is null || e.GetProperty("dataDeteccao").GetDateTime() >= desde);
        });
        if (limite is not null) linhas = linhas.Take(limite.Value);
        return JsonSerializer.Serialize(linhas.ToList(), opcoes ?? Web);
    }

    private static AnomaliaController Controller(Banco banco) =>
        new(null!, banco.Context, new FeedbackAnomaliaAnaliseService(banco.Context), null!);

    private static Task<IActionResult> ListarAsync(Banco banco, string? clienteId = null, DateTime? desde = null, int? limite = null) =>
        Controller(banco).Listar(clienteId, desde, limite);

    private static AnomaliaDetectada Anomalia(string cliente, string tag, double valor, double score, int segundos) => new()
    {
        ClienteId = cliente,
        TagName = tag,
        TipoTag = "temperatura",
        ValorRecebido = valor,
        Score = score,
        EhAnomalia = score >= 3,
        Mensagem = $"msg {cliente}/{tag}",
        Criticidade = score >= 3 ? "Alta" : null,
        ScoreRiscoProcesso = (int)(score * 10),
        ClassificacaoRisco = score >= 3 ? "Alto" : "Baixo",
        TendenciaRisco = "Estavel",
        TendenciaValor = "Subindo",
        DataDeteccao = Fixture.Base.AddSeconds(segundos)
    };

    private static FeedbackAnomaliaIa Feedback(AnomaliaDetectada anomalia, MotivoAnomaliaIA? motivo,
        string? cliente = null, string? tag = null) => new()
    {
        AnomaliaId = anomalia.Id,
        ClienteId = cliente ?? anomalia.ClienteId,
        TagName = tag ?? anomalia.TagName,
        MotivoAnomaliaIAId = motivo?.Id,
        Descricao = "d",
        AcaoTomada = "a",
        DataRegistro = Fixture.Base
    };

    private static class Fixture
    {
        public static readonly DateTime Base = new(2026, 11, 16, 15, 0, 0);
        public const int TotalAnomalias = 240;
        public static readonly (string, string)[] Chaves =
            [("C1", "T1"), ("C1", "T2"), ("C1", "T3"), ("C2", "T1"), ("C2", "T2"), ("c1", "T1"), ("C2", "SEM_FEEDBACK")];
    }

    private static async Task<Banco> BancoAsync(bool comEmpates)
    {
        var banco = await BancoVazioAsync();
        var ctx = banco.Context;
        var rnd = new Random(34);

        var motivos = new[] { "falha_mecanica", "falha_sensor", "ruido_de_processo", "ajuste_operador" }
            .Select(n => new MotivoAnomaliaIA { ClienteId = "C1", Nome = n })
            .ToList();
        ctx.AddRange(motivos);

        var anomalias = new List<AnomaliaDetectada>();
        for (var i = 0; i < Fixture.TotalAnomalias; i++)
        {
            var (cliente, tag) = Fixture.Chaves[rnd.Next(Fixture.Chaves.Length)];
            var valor = Math.Round(rnd.NextDouble() * 80 - 10, 3); // inclui negativos e perto de 0
            var score = Math.Round(rnd.NextDouble() * 40, 4);
            // Com empates: varios DataDeteccao repetidos entre clientes diferentes.
            var segundos = comEmpates && i % 5 == 0 ? 7 * (i - 1) : 7 * i;
            var a = Anomalia(cliente, tag, valor, score, segundos);
            a.DataDeteccao = a.DataDeteccao.AddTicks(i % 3 == 0 ? 2_500_000 : 0); // com e sem fracao
            anomalias.Add(a);
        }
        // Insercao embaralhada: a ordem do Id nao segue a do DataDeteccao.
        ctx.AddRange(anomalias.OrderBy(_ => rnd.Next()));
        await ctx.SaveChangesAsync();

        var feedbacks = new List<FeedbackAnomaliaIa>();
        var comFeedback = anomalias.Where(x => x.TagName != "SEM_FEEDBACK").ToList();
        for (var i = 0; i < 60; i++)
        {
            var a = comFeedback[rnd.Next(comFeedback.Count)];
            var motivo = rnd.Next(6) switch { 0 => null, var k => motivos[(k - 1) % motivos.Count] };
            // Algumas com cliente/tag do feedback diferente do da anomalia.
            var f = rnd.Next(10) == 0
                ? Feedback(a, motivo, tag: "T3")
                : Feedback(a, motivo);
            feedbacks.Add(f);
        }
        // Feedback de anomalia inexistente: o INNER JOIN o descarta.
        feedbacks.Add(new FeedbackAnomaliaIa { AnomaliaId = 999_999, ClienteId = "C1", TagName = "T1", MotivoAnomaliaIAId = motivos[0].Id });
        ctx.AddRange(feedbacks);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        return banco;
    }

    private static async Task<Banco> BancoVazioAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var comandos = new ContadorDeComandos();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(comandos)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new Banco(connection, context, comandos);
    }

    private sealed record Banco(SqliteConnection Connection, AppDbContext Context, ContadorDeComandos Comandos) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class ContadorDeComandos : DbCommandInterceptor
    {
        public int Total { get; private set; }
        public void Zerar() => Total = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Total++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Total++;
            return ValueTask.FromResult(result);
        }
    }
}
