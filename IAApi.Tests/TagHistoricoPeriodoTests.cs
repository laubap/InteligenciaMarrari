using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using IAApi.Tests.Bridge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace IAApi.Tests;

/// <summary>
/// Tarefa 34.3 (P42): GET /api/tags/historico/{cliente}/{tag}?horas=N busca no
/// Bridge só as últimas N horas (BuscarHistoricoPorPeriodo), e não mais as 24 h
/// de Bridge:HorasHistorico. A referência é uma cópia congelada do controller de
/// antes (commit 45f17fa), rodada sobre os mesmos dados. A resposta (status e
/// JSON) tem de sair igual, inclusive com período vazio e horas inválidas.
/// Tudo offline: Bridge simulado em memória ou FakeBridgeHandler (nenhuma
/// requisição sai do processo).
/// </summary>
public sealed class TagHistoricoPeriodoTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string Cliente = "PK2";
    private const string Tag = "PK2.TAG31";

    // ---- Igualdade com o controller de antes, em todas as combinações -------

    public static TheoryData<string, int, string?> Combinacoes()
    {
        var dados = new TheoryData<string, int, string?>();
        foreach (var conjunto in new[] { "normal", "periodo-vazio", "so-nan-recente", "sem-dados", "lista-vazia" })
        foreach (var horas in new[] { 2, 1, 6, 24, 25, 0, -3, int.MaxValue })
        foreach (var horasHistorico in new string?[] { null, "12", "abc" })
            dados.Add(conjunto, horas, horasHistorico);
        return dados;
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void Resposta_IgualAoControllerDeAntes(string conjunto, int horas, string? horasHistorico)
    {
        var agora = Agora();
        var leituras = Conjunto(conjunto, agora);
        var listaVazia = conjunto == "lista-vazia";

        var bridgeAntes = new BridgeSimulado(leituras, horasHistorico, listaVazia);
        var antes = Resultado(new TagHistoricoControllerAntes(bridgeAntes).BuscarHistorico(Cliente, Tag, horas));

        var bridgeNovo = new BridgeSimulado(leituras, horasHistorico, listaVazia);
        var novo = Resultado(Controller(bridgeNovo, horasHistorico).BuscarHistorico(Cliente, Tag, horas));

        Assert.Equal(antes, novo);

        // O caminho de antes nunca chamou o período.
        Assert.Empty(bridgeAntes.Periodos);
        Assert.Equal(1, bridgeAntes.Chamadas24h);

        var limite = BridgeSimulado.Horas(horasHistorico);
        if (horas <= 0 || horas > limite)
        {
            // Horas inválidas ou acima do que o caminho de antes mostrava: caminho de antes.
            Assert.Empty(bridgeNovo.Periodos);
            Assert.Equal(1, bridgeNovo.Chamadas24h);
            return;
        }

        var periodo = Assert.Single(bridgeNovo.Periodos);
        Assert.Equal(TimeSpan.FromHours(horas), periodo.Fim - periodo.Inicio);

        // Só cai nas 24 h quando o período não tem leitura válida.
        var periodoTemDados = !listaVazia && leituras.Any(x =>
            x.DataHora >= agora.AddHours(-horas) && float.IsFinite(x.Valor));
        Assert.Equal(periodoTemDados ? 0 : 1, bridgeNovo.Chamadas24h);
    }

    [Fact]
    public void Periodo_EnviadoSemFuso_TerminandoAgora()
    {
        var bridge = new BridgeSimulado(Conjunto("normal", Agora()), null, false);

        var antes = DateTime.Now;
        Controller(bridge, null).BuscarHistorico(Cliente, Tag, 2);
        var depois = DateTime.Now;

        var periodo = Assert.Single(bridge.Periodos);
        Assert.Equal(DateTimeKind.Unspecified, periodo.Inicio.Kind);
        Assert.Equal(DateTimeKind.Unspecified, periodo.Fim.Kind);
        Assert.InRange(periodo.Fim, DateTime.SpecifyKind(antes, DateTimeKind.Unspecified), DateTime.SpecifyKind(depois, DateTimeKind.Unspecified));
        Assert.Equal(0, bridge.Chamadas24h);
    }

    [Fact]
    public void Periodo_RespostaTemSoAsHorasPedidas()
    {
        var bridge = new BridgeSimulado(Conjunto("normal", Agora()), null, false);

        var corpo = (OkObjectResult)Controller(bridge, null).BuscarHistorico(Cliente, Tag, 2);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(corpo.Value, Web));

        // 120 minutos, menos os NaN/infinitos do conjunto (filtrados como antes).
        var esperado = Conjunto("normal", Agora())
            .Count(x => x.DataHora >= DateTime.Now.AddHours(-2) && float.IsFinite(x.Valor));
        Assert.Equal(esperado, json.RootElement.GetProperty("quantidadeLeituras").GetInt32());
        Assert.InRange(esperado, 100, 120);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("conexao")]
    public void FalhaDeTransporte_NaoRepeteEm24h_MesmoErroDeAntes(string falha)
    {
        var leituras = Conjunto("normal", Agora());
        var bridgeAntes = new BridgeSimulado(leituras, null, false) { Falha = falha };
        var bridgeNovo = new BridgeSimulado(leituras, null, false) { Falha = falha };

        var antes = Resultado(new TagHistoricoControllerAntes(bridgeAntes).BuscarHistorico(Cliente, Tag, 2));
        var novo = Resultado(Controller(bridgeNovo, null).BuscarHistorico(Cliente, Tag, 2));

        Assert.Equal(antes, novo);
        Assert.StartsWith("400|", novo);
        Assert.Single(bridgeNovo.Periodos);
        Assert.Equal(0, bridgeNovo.Chamadas24h);
    }

    [Fact]
    public void ErroHttpDoBridgeNoPeriodo_CaiNas24h_MesmaRespostaDeAntes()
    {
        var leituras = Conjunto("normal", Agora());
        var bridgeAntes = new BridgeSimulado(leituras, null, false);
        var bridgeNovo = new BridgeSimulado(leituras, null, false) { Falha = "http-500-periodo" };

        var antes = Resultado(new TagHistoricoControllerAntes(bridgeAntes).BuscarHistorico(Cliente, Tag, 2));
        var novo = Resultado(Controller(bridgeNovo, null).BuscarHistorico(Cliente, Tag, 2));

        Assert.Equal(antes, novo);
        Assert.StartsWith("200|", novo);
        Assert.Single(bridgeNovo.Periodos);
        Assert.Equal(1, bridgeNovo.Chamadas24h);
    }

    [Fact]
    public void BridgeDevolveForaDoPeriodo_MesmoCorteDeAntes()
    {
        // Um IHistoricoService que ignora o período e devolve as 30 h: o
        // controller tem de aplicar o mesmo corte "agora − horas" de antes.
        var leituras = Conjunto("normal", Agora());
        var bridgeAntes = new BridgeSimulado(leituras, null, false);
        var bridgeNovo = new BridgeSimulado(leituras, null, false) { Falha = "ignora-periodo" };

        var antes = Resultado(new TagHistoricoControllerAntes(bridgeAntes).BuscarHistorico(Cliente, Tag, 2));
        var novo = Resultado(Controller(bridgeNovo, null).BuscarHistorico(Cliente, Tag, 2));

        Assert.Equal(antes, novo);
        Assert.Equal(0, bridgeNovo.Chamadas24h);
    }

    // ---- Com o BridgeHistoricoService real (transporte falso) ---------------

    [Fact]
    public void BridgeReal_UmaRequisicaoDe2h_SemHoras_SemFuso_MesmoJsonDeAntes()
    {
        var leituras = Conjunto("normal", Agora());
        var handlerAntes = BridgeHttp(leituras);
        var handlerNovo = BridgeHttp(leituras);

        var antes = Resultado(new TagHistoricoControllerAntes(BridgeReal(handlerAntes)).BuscarHistorico(Cliente, Tag, 2));
        var novo = Resultado(Controller(BridgeReal(handlerNovo), null).BuscarHistorico(Cliente, Tag, 2));

        Assert.Equal(antes, novo);
        Assert.StartsWith("200|", novo);

        Assert.Equal("?tagName=PK2.TAG31&horas=24", Assert.Single(handlerAntes.Requests).Uri.Query);

        var requisicao = Assert.Single(handlerNovo.Requests);
        Assert.Equal("/historico", requisicao.Uri.AbsolutePath);
        var consulta = Consulta(requisicao.Uri);
        Assert.Equal(new[] { "tagName", "inicio", "fim" }, consulta.Keys);
        var semFuso = new Regex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{7}$");
        Assert.Matches(semFuso, consulta["inicio"]);
        Assert.Matches(semFuso, consulta["fim"]);
        Assert.Equal(TimeSpan.FromHours(2), Data(consulta["fim"]) - Data(consulta["inicio"]));
    }

    [Fact]
    public void BridgeReal_PeriodoVazio_CaiNas24h_MesmoJsonDeAntes()
    {
        var leituras = Conjunto("periodo-vazio", Agora());
        var handlerAntes = BridgeHttp(leituras);
        var handlerNovo = BridgeHttp(leituras);

        var antes = Resultado(new TagHistoricoControllerAntes(BridgeReal(handlerAntes)).BuscarHistorico(Cliente, Tag, 2));
        var novo = Resultado(Controller(BridgeReal(handlerNovo), null).BuscarHistorico(Cliente, Tag, 2));

        Assert.Equal(antes, novo);
        Assert.Contains("\"quantidadeLeituras\":20", novo);
        Assert.Equal(2, handlerNovo.Calls);
        Assert.Contains("&inicio=", handlerNovo.Requests[0].Uri.Query);
        Assert.Equal("?tagName=PK2.TAG31&horas=24", handlerNovo.Requests[1].Uri.Query);
    }

    [Fact]
    public void BridgeReal_Timeout_UmaRequisicao_MesmoErroDeAntes()
    {
        FakeBridgeHandler Lento() => new(_ => throw new TaskCanceledException("timeout simulado"));
        var handlerAntes = Lento();
        var handlerNovo = Lento();

        var antes = Resultado(new TagHistoricoControllerAntes(BridgeReal(handlerAntes)).BuscarHistorico(Cliente, Tag, 2));
        var novo = Resultado(Controller(BridgeReal(handlerNovo), null).BuscarHistorico(Cliente, Tag, 2));

        Assert.Equal(antes, novo);
        Assert.Contains("Tempo limite excedido", novo);
        Assert.Equal(1, handlerNovo.Calls);
    }

    [Fact]
    public void BridgeReal_Status500NoPeriodo_CaiNas24h_MesmoJsonDeAntes()
    {
        var leituras = Conjunto("normal", Agora());
        var handlerAntes = BridgeHttp(leituras);
        var handlerNovo = new FakeBridgeHandler(request =>
            request.RequestUri!.Query.Contains("&inicio=")
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("open operation 00:00:03") }
                : Responder(leituras, request));

        var antes = Resultado(new TagHistoricoControllerAntes(BridgeReal(handlerAntes)).BuscarHistorico(Cliente, Tag, 2));
        var novo = Resultado(Controller(BridgeReal(handlerNovo), null).BuscarHistorico(Cliente, Tag, 2));

        Assert.Equal(antes, novo);
        Assert.StartsWith("200|", novo);
        Assert.Equal(2, handlerNovo.Calls);
    }

    // ---- Apoio -------------------------------------------------------------

    private static TagHistoricoController Controller(IHistoricoService historico, string? horasHistorico)
        => new(historico, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Bridge:HorasHistorico"] = horasHistorico })
            .Build());

    private static string Resultado(IActionResult resultado)
        => resultado is ObjectResult objeto
            ? $"{objeto.StatusCode}|{JsonSerializer.Serialize(objeto.Value, Web)}"
            : resultado.GetType().Name;

    private static DateTime Agora() => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

    /// <summary>
    /// Uma leitura por minuto, 30 s antes de cada minuto cheio contado a partir
    /// de <paramref name="agora"/>: nenhuma cai em cima de um corte de hora,
    /// então os poucos milissegundos entre as duas execuções não mudam o resultado.
    /// </summary>
    private static List<LeituraBruta> Conjunto(string nome, DateTime agora)
    {
        LeituraBruta Leitura(int minutos, float valor)
            => new() { DataHora = agora.AddMinutes(-minutos).AddSeconds(-30), Valor = valor };

        float Valor(int k)
            => k % 17 == 0 ? float.NaN
             : k % 23 == 0 ? float.PositiveInfinity
             : k % 29 == 0 ? float.NegativeInfinity
             : 50f + (k % 41) * 0.731f - (k % 7) * 1.25f;

        return nome switch
        {
            // 30 h: mais do que as 24 h do caminho de antes.
            "normal" => Enumerable.Range(0, 30 * 60).Select(k => Leitura(k, Valor(k))).ToList(),
            // Só entre 3 h e 10 h atrás: período de 1 h ou 2 h vazio; de 6 h ou 24 h, não.
            "periodo-vazio" => Enumerable.Range(3 * 60, 7 * 60).Select(k => Leitura(k, Valor(k))).ToList(),
            // Últimas 2 h só com NaN; antes disso, valores válidos.
            "so-nan-recente" => Enumerable.Range(0, 30 * 60).Select(k => Leitura(k, k < 2 * 60 ? float.NaN : Valor(k))).ToList(),
            // Nada nas últimas 25 h.
            "sem-dados" or "lista-vazia" => Enumerable.Range(25 * 60, 60).Select(k => Leitura(k, Valor(k))).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(nome))
        };
    }

    /// <summary>
    /// Bridge em memória com o comportamento do BridgeHistoricoService:
    /// BuscarHistorico devolve as últimas Bridge:HorasHistorico; o período
    /// devolve [inicio, fim]; trecho sem leitura válida lança
    /// InvalidOperationException. Os NaN/infinitos são mantidos de propósito
    /// (pior caso) para conferir o filtro do controller.
    /// </summary>
    private sealed class BridgeSimulado(List<LeituraBruta> leituras, string? horasHistorico, bool listaVaziaEmVezDeErro) : IHistoricoService
    {
        public int Chamadas24h { get; private set; }
        public List<(DateTime Inicio, DateTime Fim)> Periodos { get; } = [];
        public string? Falha { get; init; }

        public static int Horas(string? texto)
            => int.TryParse(texto ?? "24", NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h > 0 ? h : 24;

        public List<LeituraBruta> BuscarHistorico(string clienteId, string tagName, string tipoTag)
        {
            Chamadas24h++;
            FalharSePreciso(periodo: false);
            var agora = DateTime.Now;
            return Trecho(agora.AddHours(-Horas(horasHistorico)), agora);
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(string clienteId, string tagName, DateTime inicio, DateTime fim)
        {
            Periodos.Add((inicio, fim));
            FalharSePreciso(periodo: true);
            return Falha == "ignora-periodo"
                ? Trecho(DateTime.MinValue, DateTime.MaxValue)
                : Trecho(inicio, fim);
        }

        private List<LeituraBruta> Trecho(DateTime inicio, DateTime fim)
        {
            var trecho = leituras
                .Where(x => x.DataHora >= inicio && x.DataHora <= fim)
                .OrderBy(x => x.DataHora)
                .Select(x => new LeituraBruta { DataHora = x.DataHora, Valor = x.Valor })
                .ToList();

            if (!trecho.Any(x => float.IsFinite(x.Valor)) && !listaVaziaEmVezDeErro)
                throw new InvalidOperationException("Nenhum histórico foi encontrado para a tag 'PK2.TAG31' do cliente 'PK2'.");

            return listaVaziaEmVezDeErro && !trecho.Any(x => float.IsFinite(x.Valor)) ? [] : trecho;
        }

        private void FalharSePreciso(bool periodo)
        {
            switch (Falha)
            {
                case "timeout":
                    throw new Exception("Tempo limite excedido ao consultar o Bridge.", new TaskCanceledException());
                case "conexao":
                    throw new Exception("Não foi possível conectar ao Bridge em 'http://bridge.test'. Verifique se o Bridge está rodando.",
                        new HttpRequestException("recusada"));
                case "http-500-periodo" when periodo:
                    throw new InvalidOperationException("O Bridge respondeu com o status 500 (Internal Server Error). Resposta: x");
            }
        }
    }

    /// <summary>Bridge HTTP falso: responde por horas ou por inicio/fim, como o Bridge.</summary>
    private static FakeBridgeHandler BridgeHttp(List<LeituraBruta> leituras)
        => new(request => Responder(leituras, request));

    private static HttpResponseMessage Responder(List<LeituraBruta> leituras, HttpRequestMessage request)
    {
        var consulta = Consulta(request.RequestUri!);
        DateTime inicio, fim;
        if (consulta.TryGetValue("horas", out var horas))
        {
            fim = DateTime.Now;
            inicio = fim.AddHours(-int.Parse(horas, CultureInfo.InvariantCulture));
        }
        else
        {
            inicio = Data(consulta["inicio"]);
            fim = Data(consulta["fim"]);
        }

        var corpo = leituras
            .Where(x => x.DataHora >= inicio && x.DataHora <= fim)
            .Select(x => new
            {
                dataHora = x.DataHora.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                tagName = consulta["tagName"],
                valor = float.IsFinite(x.Valor) ? (object)x.Valor : "NaN"
            });

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(corpo)) };
    }

    private static Dictionary<string, string> Consulta(Uri uri)
        => uri.Query.TrimStart('?').Split('&')
            .Select(par => par.Split('=', 2))
            .ToDictionary(par => Uri.UnescapeDataString(par[0]), par => Uri.UnescapeDataString(par[1]));

    private static DateTime Data(string texto)
        => DateTime.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static BridgeHistoricoService BridgeReal(FakeBridgeHandler handler)
    {
        var bridge = new BridgeHistoricoService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Bridge:BaseUrl"] = "http://bridge.test" })
            .Build());
        var campo = typeof(BridgeHistoricoService).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((HttpClient)campo.GetValue(bridge)!).Dispose();
        campo.SetValue(bridge, new HttpClient(handler)); // Só o transporte do teste; a construção de produção fica igual.
        return bridge;
    }

    // ---- Referência congelada: TagHistoricoController do commit 45f17fa ----

    private sealed class TagHistoricoControllerAntes(IHistoricoService historicoService) : ControllerBase
    {
        private readonly IHistoricoService _historicoService = historicoService;

        public IActionResult BuscarHistorico(
            string clienteId,
            string tagName,
            int horas = 6)
        {
            try
            {
                const string tipoTag = "perfil-comportamental";

                var historico = _historicoService
                    .BuscarHistorico(
                        clienteId,
                        tagName,
                        tipoTag)
                    .Where(x =>
                        !float.IsNaN(x.Valor) &&
                        !float.IsInfinity(x.Valor))
                    .OrderBy(x => x.DataHora)
                    .ToList();

                if (historico.Count == 0)
                {
                    return NotFound(new
                    {
                        erro = "Nenhum histórico encontrado para esta tag."
                    });
                }

                var inicio =
                    DateTime.Now.AddHours(-horas);

                var leituras = historico
                    .Where(x =>
                        x.DataHora >= inicio)
                    .Select(x => new
                    {
                        clienteId,
                        tagName,
                        dataHora = x.DataHora,
                        valor = x.Valor
                    })
                    .ToList();

                if (leituras.Count == 0)
                {
                    leituras = historico
                        .TakeLast(20)
                        .Select(x => new
                        {
                            clienteId,
                            tagName,
                            dataHora = x.DataHora,
                            valor = x.Valor
                        })
                        .ToList();
                }

                return Ok(new
                {
                    clienteId,
                    tagName,
                    horas,
                    quantidadeLeituras = leituras.Count,
                    historico = leituras
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    erro = ex.Message
                });
            }
        }
    }
}
