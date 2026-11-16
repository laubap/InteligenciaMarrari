using System.Net;
using System.Text;
using System.Text.Json;
using IAApi.Application.Llm.Contracts;
using IAApi.Infrastructure.Llm.Configuration;
using IAApi.Infrastructure.Llm.Providers;
using IAApi.Services.AnomalyDetection.Interpretation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IAApi.Tests.Security;

/// <summary>
/// P39: nenhum stack trace na resposta HTTP; resposta bruta da LLM so em Debug;
/// erro HTTP da LLM registrado so com o codigo, sem o corpo.
/// Tudo em memoria: o HttpClient usa um handler falso (http://llm.test), sem rede.
/// </summary>
public sealed class SensitiveDataExposureTests
{
    private const string Marcador = "DADO-SENSIVEL-TAG24-123,45";

    // ---- AnomaliaController -------------------------------------------------

    [Fact]
    public async Task DetectarTag_ComErro_DevolveSoMensagemGenerica_SemStackTrace()
    {
        // PredictionService nulo: a chamada lanca dentro do try, como um erro real.
        var controller = new AnomaliaController(null!, null!, null!, null!);

        var resultado = await controller.DetectarTag(
            new AnomaliaRequest { ClienteId = "C1", TagName = "TAG-X" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        var propriedades = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "erro" }, propriedades);
        Assert.Equal(AnomaliaController.MensagemErroAnalise, json.RootElement.GetProperty("erro").GetString());
        var texto = json.RootElement.GetRawText();
        Assert.DoesNotContain("Exception", texto);
        Assert.DoesNotContain(" at ", texto);
    }

    // ---- LM Studio ----------------------------------------------------------

    [Fact]
    public async Task LmStudio_ErroHttp_RegistraSoOCodigo_SemOCorpo()
    {
        var logger = new LoggerDeTeste<LmStudioLlmProvider>();
        var provider = new LmStudioLlmProvider(
            ClienteFalso(HttpStatusCode.InternalServerError, $"{{\"error\":\"{Marcador}\"}}"),
            Opcoes(),
            logger);

        var resposta = await provider.GerarAsync(Pedido());

        Assert.False(resposta.Sucesso);
        Assert.Contains("HTTP 500", resposta.Erro);
        Assert.DoesNotContain(Marcador, resposta.Erro);
        Assert.Contains(logger.Registros, r => r.Nivel == LogLevel.Error && r.Texto.Contains("500"));
        Assert.DoesNotContain(logger.Registros, r => r.Texto.Contains(Marcador));
    }

    [Fact]
    public async Task LmStudio_RespostaSemEscolha_NaoPoeOCorpoNoErro()
    {
        var logger = new LoggerDeTeste<LmStudioLlmProvider>();
        var provider = new LmStudioLlmProvider(
            ClienteFalso(HttpStatusCode.OK, $"{{\"model\":\"m\",\"choices\":[],\"extra\":\"{Marcador}\"}}"),
            Opcoes(),
            logger);

        var resposta = await provider.GerarAsync(Pedido());

        Assert.False(resposta.Sucesso);
        Assert.DoesNotContain(Marcador, resposta.Erro);
        Assert.DoesNotContain(logger.Registros, r => r.Texto.Contains(Marcador));
    }

    [Fact]
    public async Task LmStudio_RespostaValida_ContinuaDevolvendoOConteudo()
    {
        var conteudo = "{\"resumo\":\"ok\"}";
        var corpo = JsonSerializer.Serialize(new
        {
            model = "m",
            choices = new[] { new { message = new { role = "assistant", content = conteudo }, finish_reason = "stop" } },
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        });
        var provider = new LmStudioLlmProvider(
            ClienteFalso(HttpStatusCode.OK, corpo),
            Opcoes(),
            new LoggerDeTeste<LmStudioLlmProvider>());

        var resposta = await provider.GerarAsync(Pedido());

        Assert.True(resposta.Sucesso);
        Assert.Equal(conteudo, resposta.Conteudo);
    }

    // ---- Ollama (nao registrado na DI, mesmo ajuste) ------------------------

    [Fact]
    public async Task Ollama_ErroHttp_RegistraSoOCodigo_SemOCorpo()
    {
        var logger = new LoggerDeTeste<OllamaLlmProvider>();
        var provider = new OllamaLlmProvider(
            ClienteFalso(HttpStatusCode.BadGateway, Marcador),
            Opcoes(),
            logger);

        var resposta = await provider.GerarAsync(Pedido());

        Assert.False(resposta.Sucesso);
        Assert.Contains("HTTP 502", resposta.Erro);
        Assert.DoesNotContain(Marcador, resposta.Erro);
        Assert.Contains(logger.Registros, r => r.Nivel == LogLevel.Error && r.Texto.Contains("502"));
        Assert.DoesNotContain(logger.Registros, r => r.Texto.Contains(Marcador));
    }

    // ---- Interpretacao: resposta bruta so em Debug --------------------------

    [Fact]
    public async Task Interpretacao_RespostaBrutaDaLlm_SoEmDebug()
    {
        var logger = new LoggerDeTeste<LocalLlmAnomalyInterpretationService>();
        var service = new LocalLlmAnomalyInterpretationService(
            new ProvedorFalso(new LlmResponse { Sucesso = true, Conteudo = Marcador, Modelo = "m" }),
            new RuleBasedAnomalyInterpretationService(),
            logger);

        await service.InterpretarAsync(new AnomalyInterpretationContext
        {
            ClienteId = "C1",
            TagName = "TAG-X",
            Severidade = "Normal"
        });

        var comMarcador = logger.Registros.Where(r => r.Texto.Contains(Marcador)).ToList();
        Assert.NotEmpty(comMarcador);
        Assert.All(comMarcador, r => Assert.Equal(LogLevel.Debug, r.Nivel));
    }

    // ---- Apoio --------------------------------------------------------------

    private static LlmRequest Pedido() => new()
    {
        SystemPrompt = "sistema",
        UserPrompt = "usuario"
    };

    private static IOptions<LlmOptions> Opcoes() => Options.Create(new LlmOptions
    {
        Habilitado = true,
        Modelo = "m",
        BaseUrl = "http://llm.test/v1/",
        TimeoutSegundos = 5
    });

    private static HttpClient ClienteFalso(HttpStatusCode status, string corpo) =>
        new(new HandlerFalso(status, corpo)) { BaseAddress = new Uri("http://llm.test/v1/") };

    private sealed class HandlerFalso(HttpStatusCode status, string corpo) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ProvedorFalso(LlmResponse resposta) : ILlmProvider
    {
        public string Nome => "falso";

        public Task<LlmResponse> GerarAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(resposta);
    }

    private sealed record Registro(LogLevel Nivel, string Texto);

    private sealed class LoggerDeTeste<T> : ILogger<T>
    {
        public List<Registro> Registros { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Inclui o texto da excecao, que o log de console tambem mostra.
            Registros.Add(new Registro(logLevel, formatter(state, exception) + " " + exception));
        }
    }
}
